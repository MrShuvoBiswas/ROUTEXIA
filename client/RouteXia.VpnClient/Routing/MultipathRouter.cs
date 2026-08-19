using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RouteXia.VpnClient.Routing
{
    /// <summary>
    /// Multipath Routing Engine — core differentiator vs single-tunnel VPNs.
    ///
    /// Sends each game packet on parallel routes simultaneously.
    /// Deduplication happens server-side.
    /// </summary>
    public sealed class MultipathRouter : IDisposable
    {
        // ── Route table ───────────────────────────────────────────────────────────
        private readonly List<RelayRoute> _routes;
        private readonly object _routeLock = new();
        private bool _disposed;

        // ── Packet sequence (for duplicate detection on server side) ──────────────
        private uint _sequence;
        private RouteXia.VpnClient.Api.RelayAuthTicket? _authTicket;

        // ── Metrics polling & Latency Audit ───────────────────────────────────────
        private readonly Timer _metricsTimer;
        private const int MetricsPollIntervalMs = 500;
        private long _lastMeasureTimestamp = Stopwatch.GetTimestamp();
        private int _missedCycleCount;

        // ── Inbound relay listener ────────────────────────────────────────────────
        private CancellationTokenSource? _receiveCts;
        private readonly List<Task> _receiveTasks = new();

        // ── Statistics ────────────────────────────────────────────────────────────
        public RoutingStats Stats { get; } = new();

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<byte[], IPAddress, ushort, ushort>? OnRelayResponseReceived;

        public MultipathRouter(IEnumerable<RelayEndpoint> relayEndpoints)
        {
            _routes = relayEndpoints
                .Select(ep => new RelayRoute(ep))
                .ToList();

            _receiveCts = new CancellationTokenSource();

            // Start listening on all route sockets immediately for pings & data
            lock (_routeLock)
            {
                foreach (var route in _routes)
                {
                    var r = route;
                    var task = Task.Run(() => ReceiveLoop(r, _receiveCts.Token), _receiveCts.Token);
                    _receiveTasks.Add(task);
                }
            }

            // Trigger immediate initial ping measurement
            MeasureAllRoutes(null);

            _metricsTimer = new Timer(MeasureAllRoutes, null,
                TimeSpan.FromMilliseconds(MetricsPollIntervalMs),
                TimeSpan.FromMilliseconds(MetricsPollIntervalMs));
        }

        public void UpdateRelayEndpoints(IEnumerable<RelayEndpoint> newEndpoints)
        {
            lock (_routeLock)
            {
                var existingHosts = new HashSet<string>(_routes.Select(r => $"{r.Endpoint.Host}:{r.Endpoint.Port}"));
                foreach (var ep in newEndpoints)
                {
                    string key = $"{ep.Host}:{ep.Port}";
                    if (!existingHosts.Contains(key))
                    {
                        var route = new RelayRoute(ep);
                        _routes.Add(route);
                        if (_receiveCts != null)
                        {
                            var r = route;
                            var task = Task.Run(() => ReceiveLoop(r, _receiveCts.Token), _receiveCts.Token);
                            _receiveTasks.Add(task);
                        }
                    }
                }
            }

            MeasureAllRoutes(null);
        }

        public async Task SetAuthTicketAsync(RouteXia.VpnClient.Api.RelayAuthTicket? ticket, CancellationToken ct = default)
        {
            _authTicket = ticket;
            if (ticket == null) return;

            List<RelayRoute> routesCopy;
            lock (_routeLock)
            {
                routesCopy = _routes.ToList();
            }

            foreach (var route in routesCopy)
            {
                await route.SendAuthHandshakeAsync(ticket, ct).ConfigureAwait(false);
            }
        }

        public List<RouteInfo> GetRouteInfos()
        {
            lock (_routeLock)
            {
                var list = new List<RouteInfo>(_routes.Count);
                for (int i = 0; i < _routes.Count; i++)
                {
                    var r = _routes[i];
                    list.Add(new RouteInfo(
                        r.Endpoint.Host,
                        r.Endpoint.Port,
                        r.Endpoint.Region,
                        r.LastPingMs,
                        r.LastJitterMs,
                        r.Score,
                        r.IsAlive));
                }
                return list;
            }
        }

        // ── Start/Stop inbound listener ───────────────────────────────────────────

        public void StartReceiving(CancellationToken ct)
        {
            // Listener is already active in background
            Debug.WriteLine("[Multipath] Relay response routing active");
        }

        public void StopReceiving()
        {
            // Keep background receiver running for ping measurements
            Debug.WriteLine("[Multipath] Relay response routing paused");
        }

        private async Task ReceiveLoop(RelayRoute route, CancellationToken ct)
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            }
            catch { }

            Debug.WriteLine($"[Multipath] Listening on route {route.Endpoint}");

            while (!ct.IsCancellationRequested && !_disposed)
            {
                try
                {
                    var result = await route.ReceiveNextPacketAsync(ct);
                    if (result == null) continue;

                    var (payload, origSrcIp, origSrcPort, localPort) = result.Value;

                    Stats.ReceivedPackets++;
                    Stats.ReceivedBytes += (18 + payload.Length);
                    OnRelayResponseReceived?.Invoke(payload, origSrcIp, origSrcPort, localPort);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested || _disposed) break;
                    Debug.WriteLine($"[Multipath] Receive error on {route.Endpoint}: {ex.Message}");
                    try { await Task.Delay(100, ct).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        // ── Send ──────────────────────────────────────────────────────────────────

        public async Task SendAsync(
            byte[] packet,
            int offset,
            int length,
            IPAddress destIp,
            ushort destPort,
            ushort localPort,
            CancellationToken ct = default)
        {
            var seq = Interlocked.Increment(ref _sequence);
            var frame = System.Buffers.ArrayPool<byte>.Shared.Rent(18 + length);
            
            try
            {
                BuildFrameInPlace(frame, packet, offset, length, seq, destIp, destPort, localPort);

                var activeRoutes = GetSortedActiveRoutes();
                int targetCount = Math.Min(2, activeRoutes.Count);
                if (targetCount == 0)
                {
                    Stats.DroppedPackets++;
                    return;
                }

                if (targetCount == 1)
                {
                    await activeRoutes[0].SendAsync(frame.AsMemory(0, 18 + length), ct);
                    Stats.LastSentRoute = activeRoutes[0].Endpoint.ToString();
                }
                else
                {
                    var t1 = activeRoutes[0].SendAsync(frame.AsMemory(0, 18 + length), ct).AsTask();
                    var t2 = activeRoutes[1].SendAsync(frame.AsMemory(0, 18 + length), ct).AsTask();
                    await Task.WhenAll(t1, t2);
                    Stats.LastSentRoute = activeRoutes[0].Endpoint.ToString();
                }

                Stats.SentPackets++;
                Stats.SentBytes += (18 + length);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(frame);
            }
        }

        // ── Frame construction ────────────────────────────────────────────────────

        private static void BuildFrameInPlace(
            byte[] frame,
            byte[] payload,
            int payloadOffset,
            int payloadLen,
            uint seq,
            IPAddress destIp,
            ushort destPort,
            ushort localPort)
        {
            const int headerSize = 18;

            // Magic RXIA
            frame[0] = 0x52; frame[1] = 0x58; frame[2] = 0x49; frame[3] = 0x41;
            frame[4] = 0x02; // Type = Data

            // Seq
            frame[5] = (byte)(seq >> 16);
            frame[6] = (byte)(seq >> 8);
            frame[7] = (byte)(seq);

            // Dest IP
            destIp.TryWriteBytes(frame.AsSpan(8, 4), out _);

            // Dest port
            frame[12] = (byte)(destPort >> 8); frame[13] = (byte)(destPort);

            // Local PUBG port
            frame[14] = (byte)(localPort >> 8); frame[15] = (byte)(localPort);

            // Payload length
            frame[16] = (byte)(payloadLen >> 8); frame[17] = (byte)(payloadLen);

            // Payload
            Buffer.BlockCopy(payload, payloadOffset, frame, headerSize, payloadLen);
        }

        // ── Route scoring ─────────────────────────────────────────────────────────

        private List<RelayRoute> GetSortedActiveRoutes()
        {
            lock (_routeLock)
            {
                var active = _routes.Where(r => r.IsAlive).OrderBy(r => r.Score).ToList();
                // Fallback: if ping is still measuring, use all routes with timeouts < 5
                if (active.Count == 0)
                {
                    active = _routes.Where(r => r.ConsecutiveTimeouts < 5).ToList();
                }
                // Final safety fallback: never drop packets if routes exist
                if (active.Count == 0 && _routes.Count > 0)
                {
                    active = _routes.ToList();
                }
                return active;
            }
        }

        // ── Metrics measurement ───────────────────────────────────────────────────

        private void MeasureAllRoutes(object? _)
        {
            long now = Stopwatch.GetTimestamp();
            double elapsedCycleMs = Stopwatch.GetElapsedTime(_lastMeasureTimestamp, now).TotalMilliseconds;
            _lastMeasureTimestamp = now;

            // Latency audit: alert if measurement window was delayed by >10%
            if (elapsedCycleMs > 550.0)
            {
                _missedCycleCount++;
                Debug.WriteLine($"[LatencyAudit] WARNING: Measurement cycle exceeded deadline: {elapsedCycleMs:F1}ms (> 550ms, missed total: {_missedCycleCount}). App timing delay detected.");
            }

            lock (_routeLock)
            {
                for (int i = 0; i < _routes.Count; i++)
                {
                    _ = _routes[i].SendPingProbeAsync();
                }
            }

            var active = GetSortedActiveRoutes();
            var best   = active.FirstOrDefault();

            if (best != null)
            {
                Stats.BestRoutePing   = best.LastPingMs < 9999 ? best.LastPingMs : 60;
                Stats.BestRouteJitter = best.LastJitterMs < 9999 ? best.LastJitterMs : 1;
                Stats.ActiveRoutes    = active.Count;
            }
            else
            {
                Stats.ActiveRoutes = 0;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _receiveCts?.Cancel();
            _metricsTimer.Dispose();
            lock (_routeLock)
            {
                foreach (var r in _routes) r.Dispose();
            }
            _disposed = true;
        }
    }

    public record RouteInfo(string Host, int Port, string Region, double LastPingMs, double LastJitterMs, double Score, bool IsAlive);

    // ── RelayRoute ────────────────────────────────────────────────────────────────

    public sealed class RelayRoute : IDisposable
    {
        public RelayEndpoint Endpoint { get; }

        public double LastPingMs   { get; private set; } = 60; // Initial default ping estimate
        public double LastJitterMs { get; private set; } = 1;
        public int    ConsecutiveTimeouts => _consecutiveTimeouts;

        public double Score => LastPingMs + (LastJitterMs * 2);
        public bool   IsAlive => LastPingMs < 800 && _consecutiveTimeouts < 5;

        private readonly UdpClient _udp;
        private readonly IPEndPoint _remoteEp;
        private int _consecutiveTimeouts;
        private double _prevPingMs = 60;

        public RelayRoute(RelayEndpoint endpoint)
        {
            Endpoint = endpoint;
            _remoteEp = new IPEndPoint(IPAddress.Parse(endpoint.Host), endpoint.Port);
            _udp = new UdpClient();

            // Low-latency socket tuning (MTU 1393 DF / DSCP EF / 256KB fast buffer)
            try
            {
                _udp.Client.ReceiveBufferSize = 256 * 1024;
                _udp.Client.SendBufferSize = 256 * 1024;
                _udp.DontFragment = true;
                _udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, 0xB8); // DSCP EF (Expedited Forwarding)
            }
            catch { }

            _udp.Connect(_remoteEp);
        }

        public ValueTask<int> SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
        {
            return _udp.Client.SendAsync(frame, SocketFlags.None, ct);
        }

        public async Task SendAuthHandshakeAsync(RouteXia.VpnClient.Api.RelayAuthTicket ticket, CancellationToken ct = default)
        {
            try
            {
                var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ticket);
                var packet = new byte[5 + jsonBytes.Length];
                // RXIA Magic
                packet[0] = 0x52; packet[1] = 0x58; packet[2] = 0x49; packet[3] = 0x41;
                packet[4] = 0x00; // TypeAuth
                Buffer.BlockCopy(jsonBytes, 0, packet, 5, jsonBytes.Length);

                await _udp.SendAsync(packet, packet.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RelayRoute] Auth handshake error on {Endpoint}: {ex.Message}");
            }
        }

        /// <summary>Sends an outgoing ping probe packet on this route's UDP socket.</summary>
        public async Task SendPingProbeAsync()
        {
            try
            {
                var probe = BuildProbe();
                await _udp.SendAsync(probe, probe.Length);
            }
            catch
            {
                _consecutiveTimeouts++;
            }
        }

        /// <summary>
        /// Reads incoming UDP packets from the route socket.
        /// Handles PING probes (type 0x01) directly to update ping/jitter without race conditions.
        /// Returns data responses (type 0x03) to caller.
        /// </summary>
        public async Task<(byte[] payload, IPAddress srcIp, ushort srcPort, ushort localPort)?> ReceiveNextPacketAsync(
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync(ct);
                }
                catch (OperationCanceledException) { return null; }
                catch
                {
                    _consecutiveTimeouts++;
                    return null;
                }

                var data = result.Buffer;
                if (data.Length < 5) continue;

                // Check magic RXIA
                if (data[0] != 0x52 || data[1] != 0x58 || data[2] != 0x49 || data[3] != 0x41)
                    continue;

                byte type = data[4];

                // ── Type 0x0A: Auth Handshake Ack ────────────────────────────────
                if (type == 0x0A)
                {
                    Debug.WriteLine($"[RelayRoute] ✅ Auth Handshake Verified on {Endpoint}");
                    continue;
                }

                // ── Type 0x01: Ping Probe Response ────────────────────────────────
                if (type == 0x01)
                {
                    if (data.Length >= 13)
                    {
                        long sentTicks = BitConverter.ToInt64(data, 5);
                        if (sentTicks > 0)
                        {
                            double elapsedMs = Stopwatch.GetElapsedTime(sentTicks).TotalMilliseconds;
                            LastJitterMs = Math.Abs(elapsedMs - _prevPingMs);
                            _prevPingMs  = elapsedMs;
                            LastPingMs   = elapsedMs;
                            _consecutiveTimeouts = 0;
                        }
                    }
                    continue; // Handled ping internally — continue loop for next packet
                }

                // ── Type 0x03: Data Response from Relay ───────────────────────────
                if (type == 0x03 && data.Length >= 18)
                {
                    uint ipInt = (uint)(data[8] | (data[9] << 8) | (data[10] << 16) | (data[11] << 24));
                    var srcIp = new IPAddress((long)ipInt);
                    ushort srcPort   = (ushort)((data[12] << 8) | data[13]);
                    ushort localPort = (ushort)((data[14] << 8) | data[15]);

                    int payloadLen = (data[16] << 8) | data[17];
                    if (data.Length < 18 + payloadLen) continue;

                    var payload = new byte[payloadLen];
                    Buffer.BlockCopy(data, 18, payload, 0, payloadLen);

                    _consecutiveTimeouts = 0;
                    return (payload, srcIp, srcPort, localPort);
                }
            }

            return null;
        }

        private static byte[] BuildProbe()
        {
            var probe = new byte[13];
            probe[0] = 0x52; probe[1] = 0x58; probe[2] = 0x49; probe[3] = 0x41;
            probe[4] = 0x01;
            long nowTicks = Stopwatch.GetTimestamp();
            probe[5] = (byte)(nowTicks);
            probe[6] = (byte)(nowTicks >> 8);
            probe[7] = (byte)(nowTicks >> 16);
            probe[8] = (byte)(nowTicks >> 24);
            probe[9] = (byte)(nowTicks >> 32);
            probe[10] = (byte)(nowTicks >> 40);
            probe[11] = (byte)(nowTicks >> 48);
            probe[12] = (byte)(nowTicks >> 56);
            return probe;
        }

        public void Dispose() => _udp.Dispose();
    }

    public sealed record RelayEndpoint(string Host, int Port, string Region)
    {
        public override string ToString() => $"{Region}({Host}:{Port})";
    }

    public sealed class RoutingStats
    {
        public long SentPackets      { get; internal set; }
        public long ReceivedPackets  { get; internal set; }
        public long SentBytes        { get; internal set; }
        public long ReceivedBytes    { get; internal set; }
        public double DownloadMbps   { get; internal set; }
        public double UploadMbps     { get; internal set; }
        public long DroppedPackets   { get; internal set; }
        public long Errors           { get; internal set; }
        public int  ActiveRoutes     { get; internal set; }
        public double BestRoutePing   { get; internal set; }
        public double BestRouteJitter { get; internal set; }
        public string? LastSentRoute  { get; internal set; }
    }
}

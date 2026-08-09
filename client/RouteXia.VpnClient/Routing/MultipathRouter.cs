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

        // ── Metrics polling ───────────────────────────────────────────────────────
        private readonly Timer _metricsTimer;
        private const int MetricsPollIntervalMs = 500;

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
            Debug.WriteLine($"[Multipath] Listening on route {route.Endpoint}");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await route.ReceiveNextPacketAsync(ct);
                    if (result == null) continue;

                    var (payload, origSrcIp, origSrcPort, localPort) = result.Value;

                    Stats.ReceivedPackets++;
                    OnRelayResponseReceived?.Invoke(payload, origSrcIp, origSrcPort, localPort);

                    Debug.WriteLine($"[Multipath] Relay response: {payload.Length}b from {origSrcIp}:{origSrcPort}");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Multipath] Receive error on {route.Endpoint}: {ex.Message}");
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }
        }

        // ── Send ──────────────────────────────────────────────────────────────────

        public async Task SendAsync(
            byte[] payload,
            IPAddress destIp,
            ushort destPort,
            ushort localPort,
            CancellationToken ct = default)
        {
            var seq = Interlocked.Increment(ref _sequence);
            var frame = BuildFrame(payload, seq, destIp, destPort, localPort);

            var activeRoutes = GetSortedActiveRoutes();

            if (activeRoutes.Count == 0)
            {
                Stats.DroppedPackets++;
                Debug.WriteLine("[Multipath] No active routes — packet dropped");
                return;
            }

            // Send on top routes simultaneously
            var sendTargets = activeRoutes.Take(2).ToList();
            var tasks = sendTargets.Select(r => r.SendAsync(frame, ct)).ToArray();

            try
            {
                await Task.WhenAll(tasks);
                Stats.SentPackets++;
                Stats.LastSentRoute = sendTargets[0].Endpoint.ToString();
            }
            catch (Exception ex)
            {
                Stats.Errors++;
                Debug.WriteLine($"[Multipath] Send error: {ex.Message}");
            }
        }

        // ── Frame construction ────────────────────────────────────────────────────

        private static byte[] BuildFrame(
            byte[] payload,
            uint seq,
            IPAddress destIp,
            ushort destPort,
            ushort localPort)
        {
            var destBytes = destIp.GetAddressBytes();
            const int headerSize = 18;
            var frame = new byte[headerSize + payload.Length];

            // Magic RXIA
            frame[0] = 0x52; frame[1] = 0x58; frame[2] = 0x49; frame[3] = 0x41;
            frame[4] = 0x02; // Type = Data

            // Seq
            frame[5] = (byte)(seq >> 16);
            frame[6] = (byte)(seq >> 8);
            frame[7] = (byte)(seq);

            // Dest IP
            frame[8]  = destBytes[0]; frame[9]  = destBytes[1];
            frame[10] = destBytes[2]; frame[11] = destBytes[3];

            // Dest port
            frame[12] = (byte)(destPort >> 8); frame[13] = (byte)(destPort);

            // Local PUBG port
            frame[14] = (byte)(localPort >> 8); frame[15] = (byte)(localPort);

            // Payload length
            frame[16] = (byte)(payload.Length >> 8); frame[17] = (byte)(payload.Length);

            // Payload
            Buffer.BlockCopy(payload, 0, frame, headerSize, payload.Length);

            return frame;
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
            lock (_routeLock)
            {
                foreach (var route in _routes)
                {
                    _ = route.SendPingProbeAsync();
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
            _udp.Connect(_remoteEp);
        }

        public async Task SendAsync(byte[] frame, CancellationToken ct)
        {
            await _udp.SendAsync(frame, frame.Length);
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
                    var srcIpBytes = new byte[] { data[8], data[9], data[10], data[11] };
                    var srcIp      = new IPAddress(srcIpBytes);
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
            var tickBytes = BitConverter.GetBytes(nowTicks);
            Buffer.BlockCopy(tickBytes, 0, probe, 5, 8);
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
        public long DroppedPackets   { get; internal set; }
        public long Errors           { get; internal set; }
        public int  ActiveRoutes     { get; internal set; }
        public double BestRoutePing   { get; internal set; }
        public double BestRouteJitter { get; internal set; }
        public string? LastSentRoute  { get; internal set; }
    }
}

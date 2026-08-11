using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RouteXia.VpnClient.Interception
{
    /// <summary>
    /// WinDivert-based packet interceptor — real network interception engine for RouteXia.
    /// Intercepts outbound UDP traffic from PUBG PC and injects spoofed responses back into PUBG's local socket.
    /// </summary>
    public sealed class WinDivertInterceptor : IDisposable
    {
        // ── WinDivert handles ─────────────────────────────────────────────────────
        private IntPtr _captureHandle = WinDivertNative.INVALID_HANDLE_VALUE;
        private IntPtr _injectHandle  = WinDivertNative.INVALID_HANDLE_VALUE;

        // ── State ─────────────────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _captureLoop;
        private bool _disposed;

        // Map local UDP port -> (real local IP, captured WINDIVERT_ADDRESS metadata)
        private readonly ConcurrentDictionary<ushort, (IPAddress localIp, WINDIVERT_ADDRESS addr)> _localEndpoints = new();

        public bool IsRunning { get; private set; }

        // ── Stats ─────────────────────────────────────────────────────────────────
        public long PacketsCaptured  { get; private set; }
        public long PacketsInjected  { get; private set; }

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action<byte[], int, int, IPAddress, ushort, ushort>? OnPubgPacketCaptured;
        public event Action<IPAddress, ushort>? OnServerDiscovered;

        // WinDivert filter: Intercepts all outbound game UDP packets (ports 7000-65535).
        // Excludes 127.0.0.1 loopback traffic.
        private const string CaptureFilter =
            "outbound and udp and " +
            "ip.DstAddr != 127.0.0.1 and " +
            "udp.DstPort >= 7000 and udp.DstPort <= 65535";

        // Optimal WireGuard / Game MTU payload limit (1393 bytes) to avoid IP fragmentation
        public const int MaxPayloadSize = 1393;
        private const int MaxPacketSize  = 65535;

        // ── Start / Stop ──────────────────────────────────────────────────────────

        public void Start(System.Collections.Generic.IEnumerable<string>? excludedIps = null)
        {
            if (IsRunning) return;

            string filter = CaptureFilter;
            if (excludedIps != null)
            {
                foreach (var ip in excludedIps)
                {
                    filter += $" and ip.DstAddr != {ip}";
                }
            }

            _captureHandle = WinDivertNative.WinDivertOpen(
                filter,
                WinDivertNative.WINDIVERT_LAYER_NETWORK,
                priority: 0,
                flags: WinDivertNative.WINDIVERT_FLAG_DEFAULT);

            if (_captureHandle == WinDivertNative.INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"WinDivert capture handle failed. Win32 error: {err}. " +
                    "Ensure WinDivert.dll + WinDivert64.sys are present and app is running as Administrator.");
            }

            _injectHandle = WinDivertNative.WinDivertOpen(
                "false", // Never auto-captures — used only for sending
                WinDivertNative.WINDIVERT_LAYER_NETWORK,
                priority: 1,
                flags: WinDivertNative.WINDIVERT_FLAG_DEFAULT);

            if (_injectHandle == WinDivertNative.INVALID_HANDLE_VALUE)
            {
                WinDivertNative.WinDivertClose(_captureHandle);
                _captureHandle = WinDivertNative.INVALID_HANDLE_VALUE;
                throw new InvalidOperationException("WinDivert inject handle failed.");
            }

            _cts = new CancellationTokenSource();
            _captureLoop = Task.Run(() => CaptureLoop(_cts.Token));

            IsRunning = true;
            Debug.WriteLine("[WinDivert] Interceptor active — capturing PUBG UDP traffic");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;

            _cts?.Cancel();

            if (_captureHandle != WinDivertNative.INVALID_HANDLE_VALUE)
            {
                var handle = _captureHandle;
                _captureHandle = WinDivertNative.INVALID_HANDLE_VALUE;
                WinDivertNative.WinDivertClose(handle);
            }
            if (_injectHandle != WinDivertNative.INVALID_HANDLE_VALUE)
            {
                var handle = _injectHandle;
                _injectHandle = WinDivertNative.INVALID_HANDLE_VALUE;
                WinDivertNative.WinDivertClose(handle);
            }

            try { _captureLoop?.Wait(TimeSpan.FromMilliseconds(500)); } catch { }

            _localEndpoints.Clear();
            Debug.WriteLine("[WinDivert] Interceptor stopped");
        }

        // ── Capture loop ──────────────────────────────────────────────────────────

        private void CaptureLoop(CancellationToken ct)
        {
            var packetBuf = new byte[MaxPacketSize];
            var addr      = new WINDIVERT_ADDRESS();

            Debug.WriteLine("[WinDivert] Capture loop running...");

            while (!ct.IsCancellationRequested && IsRunning)
            {
                if (_captureHandle == WinDivertNative.INVALID_HANDLE_VALUE) break;

                uint recvLen = 0;

                bool ok = WinDivertNative.WinDivertRecv(
                    _captureHandle,
                    packetBuf,
                    (uint)packetBuf.Length,
                    ref recvLen,
                    ref addr);

                if (!ok)
                {
                    if (ct.IsCancellationRequested || !IsRunning || _captureHandle == WinDivertNative.INVALID_HANDLE_VALUE) break;
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[WinDivert] Recv error: {err}");
                    break;
                }

                PacketsCaptured++;
                ParseAndDispatch(packetBuf, recvLen, addr);
            }
        }

        private void ParseAndDispatch(byte[] packet, uint length, WINDIVERT_ADDRESS addr)
        {
            if (length < 28) return;

            int ipHdrLen = (packet[0] & 0x0F) * 4;
            if (ipHdrLen < 20 || length < (uint)(ipHdrLen + 8)) return;

            byte protocol = packet[9];
            if (protocol != 17) return; // UDP only

            // Read source IP (PUBG's real local IP address on physical NIC)
            var srcIp = new IPAddress(new byte[] { packet[12], packet[13], packet[14], packet[15] });

            // Read destination IP (game server IP)
            var destIp = new IPAddress(new byte[] { packet[16], packet[17], packet[18], packet[19] });

            // Read UDP ports
            ushort srcPort  = (ushort)((packet[ipHdrLen]     << 8) | packet[ipHdrLen + 1]);
            ushort destPort = (ushort)((packet[ipHdrLen + 2] << 8) | packet[ipHdrLen + 3]);

            // Save local endpoint mapping (srcPort -> real local IP & interface metadata)
            _localEndpoints[srcPort] = (srcIp, addr);

            int payloadOffset = ipHdrLen + 8;
            int payloadLen    = (int)length - payloadOffset;
            if (payloadLen <= 0) return;

            // MTU Clamping: cap payload at 1393 bytes
            if (payloadLen > MaxPayloadSize) payloadLen = MaxPayloadSize;

            OnServerDiscovered?.Invoke(destIp, destPort);
            OnPubgPacketCaptured?.Invoke(packet, payloadOffset, payloadLen, destIp, destPort, srcPort);

            Debug.WriteLineIf(payloadLen > 0,
                $"[WinDivert] Captured {payloadLen}b UDP from local {srcIp}:{srcPort} → game {destIp}:{destPort}");
        }

        // ── Inject response from relay back to PUBG ───────────────────────────────

        /// <summary>
        /// Injects a response packet back to PUBG's real local socket.
        /// Spoofs source IP/port as original game server so PUBG recognizes it.
        /// </summary>
        public void InjectToGame(
            byte[] payload,
            IPAddress spoofedSrcIp,
            ushort spoofedSrcPort,
            ushort localDstPort)
        {
            if (_injectHandle == WinDivertNative.INVALID_HANDLE_VALUE) return;

            // Find PUBG's real local IP and interface metadata captured from outbound packet
            IPAddress localDstIp = IPAddress.Loopback;
            var addr = new WINDIVERT_ADDRESS
            {
                Layer = (byte)WinDivertNative.WINDIVERT_LAYER_NETWORK,
                Flags = 0x00 // Inbound direction
            };

            if (_localEndpoints.TryGetValue(localDstPort, out var capturedEndpoint))
            {
                localDstIp  = capturedEndpoint.localIp;
                addr.IfIdx  = capturedEndpoint.addr.IfIdx;
                addr.SubIfIdx = capturedEndpoint.addr.SubIfIdx;
            }

            // Build raw IPv4 + UDP packet with PUBG's actual local IP as destination
            var packet = BuildUdpPacket(payload, spoofedSrcIp, spoofedSrcPort, localDstIp, localDstPort);
            if (packet == null) return;

            // Calculate checksums
            WinDivertNative.WinDivertHelperCalcChecksums(packet, (uint)packet.Length, ref addr, 0);

            uint sentLen = 0;
            bool ok = WinDivertNative.WinDivertSend(
                _injectHandle,
                packet,
                (uint)packet.Length,
                ref sentLen,
                ref addr);

            if (ok)
            {
                PacketsInjected++;
                Debug.WriteLine($"[WinDivert] Injected {payload.Length}b from {spoofedSrcIp}:{spoofedSrcPort} → local {localDstIp}:{localDstPort}");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[WinDivert] Inject to {localDstIp}:{localDstPort} failed, win32 error: {err}");
            }
        }

        // ── Packet builder ────────────────────────────────────────────────────────

        private static byte[]? BuildUdpPacket(
            byte[] payload,
            IPAddress srcIp,
            ushort srcPort,
            IPAddress dstIp,
            ushort dstPort)
        {
            if (!srcIp.AddressFamily.Equals(System.Net.Sockets.AddressFamily.InterNetwork) ||
                !dstIp.AddressFamily.Equals(System.Net.Sockets.AddressFamily.InterNetwork))
                return null;

            // Cap payload at MTU 1393
            int len = Math.Min(payload.Length, MaxPayloadSize);

            const int ipHdrLen  = 20;
            const int udpHdrLen = 8;
            int totalLen = ipHdrLen + udpHdrLen + len;
            var pkt = new byte[totalLen];

            // ── IPv4 header ───────────────────────────────────────────────────────
            pkt[0]  = 0x45;                        // Version=4, IHL=5 (20 bytes)
            pkt[1]  = 0xB8;                        // DSCP: Expedited Forwarding (EF / 0x2E) for lowest queue latency
            pkt[2]  = (byte)(totalLen >> 8);       // Total length (big-endian)
            pkt[3]  = (byte)(totalLen);
            pkt[4]  = 0x00; pkt[5] = 0x00;        // ID
            pkt[6]  = 0x40; pkt[7] = 0x00;        // Flags=DF (Don't Fragment) - MTU 1393
            pkt[8]  = 0x40;                        // TTL = 64 (optimal game hop limit)
            pkt[9]  = 0x11;                        // Protocol = UDP (17)
            pkt[10] = 0x00; pkt[11] = 0x00;       // Checksum (WinDivert recalculates)

            // Source IP (spoofed game server IP)
            var srcBytes = srcIp.GetAddressBytes();
            pkt[12] = srcBytes[0]; pkt[13] = srcBytes[1];
            pkt[14] = srcBytes[2]; pkt[15] = srcBytes[3];

            // Destination IP (PUBG's actual local IP address on NIC)
            var dstBytes = dstIp.GetAddressBytes();
            pkt[16] = dstBytes[0]; pkt[17] = dstBytes[1];
            pkt[18] = dstBytes[2]; pkt[19] = dstBytes[3];

            // ── UDP header ────────────────────────────────────────────────────────
            pkt[20] = (byte)(srcPort >> 8);  pkt[21] = (byte)srcPort;  // Src port
            pkt[22] = (byte)(dstPort >> 8);  pkt[23] = (byte)dstPort;  // Dst port
            int udpLen = udpHdrLen + len;
            pkt[24] = (byte)(udpLen >> 8);   pkt[25] = (byte)udpLen;   // UDP length
            pkt[26] = 0x00; pkt[27] = 0x00;                            // Checksum

            // ── Payload ───────────────────────────────────────────────────────────
            Buffer.BlockCopy(payload, 0, pkt, ipHdrLen + udpHdrLen, len);

            return pkt;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}

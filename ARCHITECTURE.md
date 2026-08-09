# RouteXia — Architecture (v2)
> Updated after competitive analysis vs ExitLag / NoPing

## Overview

RouteXia is a **multipath game accelerator** for PUBG PC, inspired by ExitLag and NoPing.  
It intercepts PUBG traffic at the **process level** (not port-only) and sends each packet via  
**2–3 parallel relay routes** simultaneously. The fastest copy wins; duplicates are discarded.  
A **kill-switch** blocks traffic if the tunnel drops, preventing ISP fallback and ping spikes.

---

## Architecture Comparison

| Feature | ExitLag / NoPing | RouteXia v2 |
|---|---|---|
| Traffic intercept | WFP (user-mode) | ✅ WFP (user-mode) |
| Filter granularity | Process + port + IP | ✅ Process + port + IP |
| Multipath | 3–5 parallel routes | ✅ 2–3 parallel routes |
| Route selection | AI/ML every 200ms | ✅ Score-based every 500ms |
| Kill-switch | Yes | ✅ Yes (netsh firewall rules) |
| Packet dedup | Yes (seq numbers) | ✅ Yes (RXIA header) |
| Server language | Proprietary | ✅ Go |
| Encryption | Proprietary | ✅ ChaCha20-Poly1305 |

---

## Data Flow

```
┌─────────────────────────────────────────────────────────┐
│  PUBG PC (TslGame.exe — Steam / Krafton Launcher)       │
└────────────────────────┬────────────────────────────────┘
                         │ UDP game packets
                         ▼
┌─────────────────────────────────────────────────────────┐
│  WFP Filter (RouteXia.WfpFilter)          [USER MODE]   │
│  ✓ Detects PUBG by process name (TslGame.exe)           │
│  ✓ No kernel driver required                            │
│  ✓ Raises FlowDetected event to Multipath Router        │
└────────────────────────┬────────────────────────────────┘
                         │ FlowDetected event
                         ▼
┌─────────────────────────────────────────────────────────┐
│  Multipath Router (RouteXia.VpnClient.Routing)          │
│  ✓ Wraps packet in RXIA frame (magic + seq + payload)   │
│  ✓ Sends via Route A (Singapore VPS)  ─────────────┐   │
│  ✓ Sends via Route B (India VPS)      ─────────────┤   │
│  ✓ Measures ping/jitter every 500ms                │   │
│  ✓ Scores routes, selects best 2                   │   │
└────────────────────────────────────────────────────┼───┘
                                                     │ Parallel UDP
                         ┌───────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────┐
│  RouteXia Relay Servers (Go)                            │
│  Singapore VPS  │  India VPS  │  Dubai VPS              │
│  ✓ Receive RXIA frames from both routes                 │
│  ✓ Dedup by sequence number — drop duplicates           │
│  ✓ Forward first-arriving copy to PUBG server           │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
               [PUBG Game Servers]
            (Asia / Middle East region)
```

---

## Kill-Switch Flow

```
Tunnel alive → Normal multipath routing
      │
      ▼ (tunnel drops)
KillSwitchManager detects → Adds Windows Firewall block rule for TslGame.exe
      │
      ▼ PUBG traffic blocked (no ISP leak, no ping spike)
      │
      ▼ (tunnel reconnects)
KillSwitchManager → Removes firewall rule → Routing resumes
```

---

## Component Map

```
client/
├── RouteXia.App/              # WPF UI — server selection, real-time stats
├── RouteXia.VpnClient/        # Core service
│   ├── Connection/            # ConnectionManager — establishes relay connections
│   ├── Crypto/                # ChaCha20-Poly1305 + Curve25519 key exchange
│   ├── Protocol/              # RXIA packet frame format
│   ├── Routing/               # MultipathRouter — parallel routes + scoring ⭐NEW
│   └── KillSwitch/            # KillSwitchManager — firewall block on tunnel drop ⭐NEW
└── RouteXia.WfpFilter/        # WFP process-level PUBG detection ⭐NEW (replaces NDIS)
    ├── Native/WfpNative.cs    # P/Invoke to fwpuclnt.dll
    └── WfpFilterEngine.cs     # Process poller + WFP session

server/
├── main.go                    # Go UDP relay + dedup cache ⭐NEW (replaces udp-proxy.js)
└── go.mod

scripts/
└── install-relay.sh           # VPS deploy script (Ubuntu 22.04)

docs/
├── ARCHITECTURE.md            # This file
├── BACKEND_SERVER_GUIDE.md    # Go server deployment guide
└── deep-research-report.md    # ExitLag/NoPing competitive research
```

---

## RXIA Packet Frame

```
Offset  Size  Field
──────  ────  ─────────────────────────────────
0       4     Magic: 0x52 0x58 0x49 0x41 ("RXIA")
4       4     Sequence number (uint32, big-endian)
8       2     Payload length (uint16, big-endian)
10      N     Payload (raw PUBG UDP data)
```

The sequence number allows relay servers to discard duplicate frames that arrive from multiple routes.

---

## Route Scoring

```
Score = Ping_ms + (Jitter_ms × 2)
```

- Lower score = better route
- Measured every 500ms via lightweight UDP echo probes
- Top 2 routes used for packet sending
- Route with `ping > 800ms` or `3+ consecutive timeouts` is marked dead
- Hot standby: route 3 takes over if route 1 or 2 dies

---

## Security

| Layer | Mechanism |
|---|---|
| Encryption | ChaCha20-Poly1305 (per-session key) |
| Key exchange | Curve25519 ECDH |
| Anti-replay | Sequence number validation on server |
| Kill-switch | Windows Firewall block rule on tunnel drop |
| Process safety | WFP — no process injection, BattlEye-safe |

---

## Performance Targets

| Metric | Target |
|---|---|
| Latency overhead | < 5ms |
| Packet loss in tunnel | < 0.1% |
| Route metric poll interval | 500ms |
| Kill-switch activation time | < 2 seconds |
| CPU usage | < 2% |
| Memory | < 50MB |

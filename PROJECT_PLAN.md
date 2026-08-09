# RouteXia — PUBG PC Network Optimizer (India)
## Full Technical Plan (A2Z) — v1.0

> Route optimization tool for Indian PUBG PC players. Uses WireGuard-based static CIDR routing (not full-tunnel) so only game traffic is optimized — streaming/browsing/downloads stay on the direct ISP path.

---

## 1. Product Summary

| | |
|---|---|
| **Product** | Windows desktop app + relay network that reduces PUBG PC ping for Indian players by routing game traffic through optimized relay servers |
| **Target user** | Competitive/casual PUBG PC players in India, streamers (bandwidth-sensitive) |
| **Core mechanism** | WireGuard tunnel with `AllowedIPs` scoped to known PUBG server CIDR ranges only (split tunnel by design, not by runtime process detection) |
| **NOT doing** | Boosting raw bandwidth/speed. This is a **routing** optimization, not a speed increase. Must be marketed honestly. |
| **Monetization** | Freemium — free trial (3 days) → subscription (₹99–199/month) |
| **V1 scope** | PUBG PC only, India region only |

---

## 2. Full Architecture

```
┌─────────────────────────────────────────────┐
│              WINDOWS CLIENT (C#/WPF)          │
│  ┌─────────────┐  ┌──────────────────────┐   │
│  │ UI Layer     │  │ WireGuard-NT wrapper │   │
│  │ (WPF-UI)     │  │ (tunnel.dll P/Invoke)│   │
│  └─────────────┘  └──────────────────────┘   │
│  ┌─────────────────────────────────────────┐ │
│  │ Config: AllowedIPs = static PUBG CIDR    │ │
│  │ list (pulled from backend, cached local) │ │
│  └─────────────────────────────────────────┘ │
└───────────────────┬───────────────────────────┘
                     │ WireGuard tunnel (UDP)
                     ▼
┌─────────────────────────────────────────────┐
│         RELAY VPS (Mumbai / Singapore)        │
│  WireGuard server endpoint                    │
│  Routes only tunneled traffic → PUBG servers  │
└───────────────────┬───────────────────────────┘
                     ▼
              PUBG Game Servers
              (Krafton/Tencent infra)

┌─────────────────────────────────────────────┐
│              BACKEND (NestJS)                 │
│  Auth (Logto) · Subscription (Razorpay)       │
│  Relay list API · CIDR list API · Telemetry   │
│  PostgreSQL                                   │
└─────────────────────────────────────────────┘
```

**Key design decision:** Route injection happens **before game launch**, based on a pre-built static CIDR range (not per-match dynamic IP detection). This avoids mid-match source-IP changes that would break the game's active session (verified risk — do not build dynamic per-match routing).

---

## 3. Tech Stack

| Layer | Choice | Why |
|---|---|---|
| Windows client UI | C# / WPF + **WPF-UI** (NuGet) | Fluent Design 2, dark theme, Mica/Acrylic, native Win11 feel out of box |
| Tunnel engine | **WireGuard-NT** (official driver, MIT license) | Kernel-mode, low overhead, no custom driver needed |
| Charts (ping graph) | **LiveCharts2** | WPF-native, animated, dark-theme friendly |
| Client-backend comm | REST (HTTPS) via `HttpClient` | Simple, matches NestJS REST API |
| Backend framework | **NestJS** | Already your stack |
| Database | **PostgreSQL** | Already your stack |
| Auth | **Logto** (self-hosted, reuse `auth.xiaterminal.com`) | Already set up |
| Payments | **Razorpay Subscriptions** | India-native, UPI support |
| Relay OS | Ubuntu 24.04 LTS | Standard, WireGuard native support |
| Infra automation | n8n (relay health checks, alerts) | Already your stack |
| Code signing | Windows Authenticode cert (Sectigo/DigiCert, ~₹8–15k/yr) | Mandatory — unsigned .exe triggers SmartScreen distrust |

---

## 4. Phase 0 — Research & CIDR Data Collection (2–3 weeks)

**Goal:** Build the static PUBG server IP range database. This is the foundation everything else depends on.

### Tasks
1. Write a small logger tool (PowerShell or Python) that runs `netstat -ano` / uses `GetExtendedUdpTable` while PUBG.exe is in a live match, capturing all remote IPs the process talks to.
2. Play 30–50 matches across different times/days, log every session's server IP.
3. For each unique IP, run `whois`/ASN lookup (e.g. via `ipinfo.io` API) to identify the hosting provider and announced CIDR block.
4. Cross-reference — group IPs by ASN/provider, build a CIDR range list (e.g. `103.x.x.0/24`, `52.x.x.0/22`) or auto pubg game detected.
5. Store this as versioned JSON, served via backend endpoint `GET /v1/routes/pubg-cidr` so client always pulls latest without app update.
6. Spin up 2 test VPS (Mumbai + Singapore), measure actual ping improvement per major ISP (Jio, Airtel, BSNL) using this route.

**Decision gate:** If no ISP shows meaningful (10ms+) improvement, revisit before building further.

### Deliverable
- `pubg-cidr-ranges.json` — versioned list of CIDR blocks
- Latency benchmark report (per ISP, per region)

---

## 5. Phase 1 — Windows Client (4–6 weeks)

### 5.1 Project structure
```
RouteXia.Client/
├── RouteXia.App/              # WPF UI project
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── ConnectView.xaml
│   │   ├── ServerListView.xaml
│   │   └── SettingsView.xaml
│   ├── ViewModels/            # MVVM pattern
│   ├── Resources/
│   │   ├── Fonts/              # Rajdhani, JetBrains Mono (embedded)
│   │   ├── Styles/              # ButtonStyle.xaml, ColorPalette.xaml
│   │   └── Animations/         # PulseRing.xaml storyboard
│   └── App.xaml
├── RouteXia.Tunnel/            # WireGuard-NT wrapper layer
│   ├── WireGuardInterop.cs     # P/Invoke bindings
│   ├── TunnelManager.cs        # connect/disconnect/status
│   └── ConfigBuilder.cs        # builds .conf from CIDR list + relay endpoint
├── RouteXia.Core/               # Shared models, API client
│   ├── ApiClient.cs
│   ├── Models/
│   └── Services/
│       ├── AuthService.cs
│       ├── RelayService.cs
│       └── TelemetryService.cs
└── RouteXia.Client.sln
```

### 5.2 Design tokens (for AI code generation reference)

```
Colors:
  --bg-base:      #0A0E14
  --bg-panel:     #10151D
  --bg-card:      #0D1420
  --border:       #1A2230
  --accent:       #00C2FF
  --accent-deep:  #0B5ED7
  --text-primary: #E8EDF5
  --text-muted:   #6B7684
  --status-good:  #2ED573
  --status-warn:  #FFB020
  --status-bad:   #FF4757

Fonts:
  Display/Numbers: Rajdhani (600/700 weight)
  Body:            Inter (400/500)
  Data/Ping:       JetBrains Mono (500/700)

Signature motion:
  Radar-pulse ring around connect button (3 concentric circles,
  staggered scale+fade animation, loop forever) — ties to "ping" concept
```

### 5.3 Core features (MVP)
- [ ] Connect/disconnect toggle with animated pulse ring
- [ ] Live ping display (before/after comparison)
- [ ] Relay server selector (Mumbai / Singapore cards)
- [ ] Ping stability graph (last 60 seconds, LiveCharts2)
- [ ] Login (Logto OAuth flow via embedded WebView2)
- [ ] Subscription status check + trial countdown
- [ ] Auto-update (Squirrel.Windows or Velopack)
- [ ] System tray minimize

### 5.4 Key implementation notes
- WireGuard-NT tunnel config built dynamically: `AllowedIPs` = CIDR list pulled from backend on app start, cached locally for offline fallback
- Connect action = load config → activate tunnel via `wireguard-nt` driver → poll relay for handshake confirmation → update UI state
- Ping measurement: ICMP ping to relay + measure actual in-game latency via a lightweight local proxy check (avoid relying solely on ICMP since UDP game traffic behaves differently)
- Code signing required before any public distribution build

---

## 6. Phase 2 — Backend (2–3 weeks, parallel with Phase 1)

### 6.1 Project structure
```
RouteXia-backend/
├── src/
│   ├── auth/                 # Logto integration, guards
│   ├── users/                 # user profile, trial status
│   ├── subscriptions/         # Razorpay webhook handling
│   ├── relays/                 # relay server list, health status
│   ├── routes/                 # PUBG CIDR range endpoint
│   ├── telemetry/              # anonymized ping/latency logs
│   └── main.ts
├── prisma/ (or typeorm)
│   └── schema.prisma
└── package.json
```

### 6.2 Database schema (core tables)

```sql
users
  id, logto_sub, email, created_at, trial_ends_at, subscription_status

subscriptions
  id, user_id, razorpay_subscription_id, status, current_period_end

relay_servers
  id, name, region, endpoint_ip, wg_public_key, is_active, avg_latency_ms

pubg_cidr_ranges
  id, cidr_block, source_asn, version, updated_at

telemetry_sessions
  id, user_id, relay_id, connected_at, disconnected_at,
  avg_ping_ms, min_ping_ms, max_ping_ms, jitter_ms
```

### 6.3 Key API endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/v1/auth/callback` | Logto OAuth callback |
| GET | `/v1/relays` | List active relay servers + current load |
| GET | `/v1/routes/pubg-cidr` | Versioned CIDR range list for client |
| GET | `/v1/subscription/status` | Trial/subscription check |
| POST | `/v1/subscription/webhook` | Razorpay webhook (payment events) |
| POST | `/v1/telemetry/session` | Log session ping stats (for monitoring relay health) |

### 6.4 n8n automation
- Relay health check every 5 min (ping each relay, alert via Telegram/WhatsApp if down)
- Daily telemetry summary → identify underperforming relays
- CIDR range update pipeline (when Phase 0 research finds new ranges)

---

## 7. Phase 3 — Relay Infrastructure

-aws server**Singapore** (PUBG SEA server region)
- Provider: AWS Lightsail 
- Each relay: Ubuntu 24.04 + WireGuard server config + basic firewall (only WG port + SSH)
- Bandwidth note: since only game traffic (not streaming) is tunneled, per-user bandwidth is low (few hundred kbps) — a single mid-tier VPS can serve hundreds of concurrent users

---

## 8. Phase 4 — Closed Beta (3–4 weeks)

- Recruit 20–30 testers from ITS SAIKO community
- Distribute signed .exe directly (no store needed yet)
- Collect telemetry: ping consistency, disconnects, relay load
- **Critical check:** confirm no anti-cheat (BattlEye) flags/bans occur during beta — this is a launch blocker if it happens

---

## 9. Phase 5 — Public Launch

- Landing page (can build with your existing frontend-design workflow)
- Payment live via Razorpay
- Auto-update pipeline live
- Marketing through PUBG India streamer network / your own channel

---

## 11. Risk Flags (check before public launch)

1. **Anti-cheat compatibility** — BattlEye may flag third-party network tools. Must verify clean status through beta before scaling.
2. **Legal/compliance** — VPN-adjacent service in India; consult on IT rules/data retention before scaling beyond beta.
3. **Honest marketing** — never claim "speed boost," only "route optimization." Last-mile ISP bottlenecks (e.g. certain regional broadband providers) cannot be fixed by this tool — set expectations clearly in-app and in marketing.

---



## Appendix A: Reference — WireGuard client config template

```ini
[Interface]
PrivateKey = <client_private_key>
Address = 10.66.0.2/32
DNS = 1.1.1.1

[Peer]
PublicKey = <relay_public_key>
Endpoint = <relay_ip>:51820
AllowedIPs = 103.x.x.0/24, 52.x.x.0/22   # PUBG CIDR ranges from Phase 0 research
PersistentKeepalive = 25
```

This file (`PROJECT_PLAN.md`) is written to be fed directly into an AI coding assistant (Claude Code, etc.) as project context — each phase section can be given as a standalone prompt to generate the corresponding module.

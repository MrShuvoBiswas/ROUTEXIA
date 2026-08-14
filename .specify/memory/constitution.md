<!--
SYNC IMPACT REPORT
==================
Version change: [unversioned template] -> 1.0.0
Added sections:
  - Core Principles (I-VII fully defined)
  - Performance & Reliability Standards
  - Technology Stack Constraints
  - Governance
Modified principles: N/A (initial authoring from blank template)
Removed sections: N/A
Deferred TODOs:
  - RATIFICATION_DATE: Set to today (2026-08-14); original adoption date unknown - marked as initial ratification.
-->

# RouteXia Constitution

## Core Principles

### I. Game-Agnostic Routing Core (NON-NEGOTIABLE)

The core routing engine - including WFP interception, multipath routing, route scoring, packet
encapsulation (RXIA Frame), kill-switch, and dedup logic - MUST contain zero game-specific
knowledge. Game identity (process names, server IP/CIDR ranges, port lists) MUST reside
exclusively in a separate profile/config layer loaded at runtime. No game name, process name,
IP address, or port range belonging to any specific game title may be hardcoded in any file
under `RouteXia.VpnClient`, `RouteXia.WfpFilter`, or the Go relay server.

**Rationale**: The routing engine must remain reusable across any game or application profile
without source changes. Hardcoding PUBG-specific values is the single greatest architectural
risk to long-term extensibility.

### II. WFP Process-Level Interception (NON-NEGOTIABLE)

Traffic interception MUST be performed at the process level using Windows Filtering Platform
(WFP) user-mode APIs (`fwpuclnt.dll`). Port-only or IP-only filtering is prohibited as the
primary interception method. No kernel-mode driver, NDIS filter, or process injection technique
may be used. The WFP filter MUST accept a process name from the active game profile at runtime.

**Rationale**: Process-level WFP interception ensures anti-cheat safety (BattlEye, EasyAntiCheat),
avoids kernel-mode code-signing requirements, and provides the precision needed to route only
game traffic without affecting other network activity.

### III. Multipath Parallel Routing with RXIA Frame

Every intercepted game packet MUST be encapsulated in an RXIA Frame (4-byte magic `RXIA` +
4-byte uint32 sequence number big-endian + 2-byte uint16 payload length big-endian + raw
payload) before transmission. Each packet MUST be duplicated and sent simultaneously to at
least 2 active relay servers (up to 3). The relay server MUST forward only the first-arriving
copy to the game server and MUST discard subsequent duplicates identified by sequence number.
No WireGuard, OpenVPN, or existing VPN tunnel protocol may be used as the transport - the
RXIA protocol over raw UDP is the sole relay transport.

**Rationale**: Multipath parallel send is the core value proposition - it guarantees that the
fastest network path wins on every packet, neutralising ISP routing variance, peering issues,
and transient congestion without relying on a single route.

### IV. Dynamic Route Scoring (Score = Ping + Jitter x 2)

Route scoring MUST use the formula `Score = Ping_ms + (Jitter_ms * 2)`. Scores MUST be
recalculated every 500ms via lightweight UDP echo probes to each relay. The top 2-3 routes by
lowest score MUST be selected for live packet duplication. A route MUST be marked dead and
removed from the active set if it exceeds 800ms ping OR accumulates 3 consecutive probe
timeouts. A hot-standby route (rank 3 or next available) MUST automatically promote to active
when a primary route is marked dead. The scoring formula and poll interval MUST NOT be
game-specific and MUST NOT be altered without a constitution amendment.

**Rationale**: The scoring formula is deliberately simple and auditable. Jitter is weighted 2x
because a high-variance route causes stuttering even at acceptable average ping. The 500ms
interval balances responsiveness to network changes against probe overhead.

### V. Kill-Switch via Windows Firewall (NON-NEGOTIABLE)

A kill-switch MUST be implemented using Windows Firewall outbound block rules targeting the
game process executable path (sourced from the active game profile). The kill-switch MUST
activate within 2 seconds of tunnel drop detection. It MUST prevent any game traffic from
reaching the game server via the raw ISP path while the tunnel is down (no ISP fallback, no
latency spike). The kill-switch MUST be automatically removed when the tunnel reconnects. The
kill-switch implementation MUST NOT depend on any hardcoded process name.

**Rationale**: Without a kill-switch, tunnel drops cause immediate ISP fallback which manifests
as severe latency spikes in-game. The kill-switch is the safety guarantee that makes multipath
routing a reliable product.

### VI. Game Profile / Config Layer Separation

All game-specific data - including but not limited to process executable names, server IP
addresses, CIDR ranges, UDP port ranges, and profile display names - MUST be declared in
dedicated profile configuration files (e.g., JSON or YAML). The core routing, WFP filter,
scoring, and kill-switch components MUST consume this data through a well-defined interface or
dependency injection boundary. Profiles MUST be loadable, switchable, and extendable at runtime
without recompilation.

**Rationale**: This principle is the operational enforcement of Principle I. It enables RouteXia
to support any game through configuration alone, enables user-created profiles, and prevents
accidental coupling of game-specific knowledge into core logic during development.

### VII. Tech Stack Constraint (WPF / .NET + Go Relay)

The client application and core libraries MUST be implemented in C# targeting .NET (WPF for UI).
The relay server MUST be implemented in Go. No alternative UI frameworks (WinForms, MAUI,
Electron) and no alternative relay languages may be introduced without a constitution amendment.
Encryption MUST use ChaCha20-Poly1305 with Curve25519 ECDH key exchange. No third-party VPN
SDK, WireGuard library, or OpenVPN dependency may be introduced.

**Rationale**: The WPF/.NET + Go split provides a strong Windows desktop experience with a
high-performance, cross-platform relay binary. ChaCha20-Poly1305 provides authenticated
encryption with strong performance on modern CPUs without AES hardware dependency.

## Performance & Reliability Standards

- Latency overhead introduced by RouteXia MUST be less than 5ms under normal operating conditions.
- Packet loss within the RouteXia tunnel MUST remain below 0.1%.
- Route metric polling interval MUST be 500ms (configurable only via explicit constitution amendment).
- Kill-switch activation MUST occur within 2 seconds of tunnel drop.
- Client CPU usage MUST remain below 2% during active game sessions.
- Client memory usage MUST remain below 50MB during active game sessions.
- Relay server MUST handle dedup cache efficiently - expired sequence numbers MUST be evicted
  to prevent unbounded memory growth (eviction policy MUST be defined at implementation time).

## Technology Stack Constraints

| Layer | Technology | Constraint |
|---|---|---|
| Client UI | WPF / C# / .NET | MUST; no alternative UI framework |
| Core Service | C# / .NET | MUST; RouteXia.VpnClient |
| WFP Interception | C# P/Invoke to fwpuclnt.dll | MUST; no kernel driver |
| Relay Server | Go | MUST; no alternative language |
| Encryption | ChaCha20-Poly1305 | MUST; Curve25519 ECDH key exchange |
| Transport | Raw UDP (RXIA Protocol) | MUST; no WireGuard/OpenVPN |
| Game Config | Profile files (JSON/YAML) | MUST; never hardcoded in core |
| Kill-Switch | Windows Firewall (netsh / API) | MUST; process name from profile |

No dependency that introduces a kernel-mode component, a VPN tunnel protocol, or a game-engine
SDK may be added to `RouteXia.VpnClient` or `RouteXia.WfpFilter` without explicit amendment.

## Governance

This constitution supersedes all other documented practices, architectural guidelines, and
README instructions for the RouteXia project. In cases of conflict, this document is authoritative.

**Amendment Procedure**:
1. Any proposed change to a principle must be raised as a documented amendment request,
   identifying the principle(s) affected and the rationale for change.
2. Amendments classified as MAJOR (principle removal, redefinition of a NON-NEGOTIABLE rule)
   require explicit acknowledgement that existing code must be migrated.
3. Amendments classified as MINOR (new principle or materially expanded guidance) must include
   an impact assessment on existing components.
4. PATCH amendments (clarifications, wording) may be applied directly with documented rationale.
5. All amendments must update `LAST_AMENDED_DATE` and increment `CONSTITUTION_VERSION` per
   semantic versioning rules defined herein.

**Compliance**:
- Every feature specification, implementation plan, and code review MUST verify compliance
  with Principles I, II, III, V, and VI as a hard gate - non-compliance blocks merge.
- Principles IV and VII are soft-gate: violations require documented justification.
- Use `.specify/memory/constitution.md` as the runtime governance reference in all agent sessions.

**Version**: 1.0.0 | **Ratified**: 2026-08-14 | **Last Amended**: 2026-08-14

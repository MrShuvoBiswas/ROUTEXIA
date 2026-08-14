# Data Model: WPF UI Redesign — Premium Gaming Interface

**Feature**: 001-wpf-ui-redesign
**Date**: 2026-08-14

---

## Entities

### 1. `ConnectionState` (Enum — Extended)

**Location**: `client/RouteXia.App/ViewModels/ConnectViewModel.cs`

**Current values** (3): `Disconnected`, `Connecting`, `Connected`

**New values** (add 2):

| Value | Description | Visual Treatment |
|---|---|---|
| `Disconnected` | No tunnel, idle | Neutral grey icon, "DISCONNECTED" label, muted palette |
| `Connecting` | Handshake in progress | Animated spinner ring, accent color, "CONNECTING…" label |
| `Connected` | Tunnel established | Green glow badge, region name, live ping readout |
| `Optimizing` | Route scoring in flight | Subtle pulsing accent dot, "OPTIMIZING…" sub-label, no full load state |
| `KillSwitchActive` | Tunnel dropped, firewall rule active | Red `StatusBadBrush` badge, "KILL-SWITCH ACTIVE" label, reconnect CTA |

**State transitions**:
```
Disconnected → Connecting → Connected → Optimizing → Connected (loop)
Connected → KillSwitchActive (on tunnel drop)
KillSwitchActive → Connecting (on reconnect attempt)
Connecting → Disconnected (on connect failure)
```

---

### 2. `RouteSnapshot` (New — ViewModel-level record)

**Location**: New nested class/record in `ConnectViewModel.cs`

**Purpose**: Represents a single timed measurement for one relay route, used to drive the latency graph.

| Field | Type | Description |
|---|---|---|
| `RelayName` | `string` | Display name of the relay (e.g., "Singapore", "India") — sourced from profile config |
| `RelayId` | `string` | Internal ID matching the relay config key |
| `PingMs` | `double` | Last measured round-trip latency in milliseconds |
| `JitterMs` | `double` | Last measured jitter in milliseconds |
| `Score` | `double` | Calculated score: `PingMs + (JitterMs * 2)` |
| `IsActivePrimary` | `bool` | True if this relay is the current primary route |
| `IsAlive` | `bool` | False if the route is marked dead (ping > 800ms or 3 consecutive timeouts) |
| `SampledAt` | `DateTimeOffset` | Timestamp of this measurement |

**Graph ring buffer**: `ConnectViewModel` holds a `Dictionary<string, Queue<RouteSnapshot>>` (keyed by
`RelayId`) with a max capacity of 120 entries per route (60 seconds at 500ms intervals). Oldest entries
are dequeued when capacity is exceeded.

---

### 3. `GameDetectionResult` (New — ViewModel-level record)

**Location**: New record in `ConnectViewModel.cs`

**Purpose**: Communicates the outcome of a game process scan to the UI.

| Field | Type | Description |
|---|---|---|
| `IsGameRunning` | `bool` | True if a matching game process was found |
| `DisplayName` | `string?` | Game display name from active profile config (null if not running) |
| `ProcessName` | `string?` | Detected process name (for debug/diagnostics display only) |
| `IconPath` | `string?` | Path to game icon asset from profile; null falls back to generic icon |

**ViewModel observable properties** (expose `GameDetectionResult` as flat bindable props):
- `IsGameDetected` → `bool`
- `DetectedGameName` → `string` (e.g., "PUBG PC" or "Waiting for game…")
- `DetectedGameIconPath` → `string?`

---

### 4. `AccountInfo` (Existing — extended surface in `AuthViewModel`)

**Location**: `client/RouteXia.App/ViewModels/AuthViewModel.cs`

**Existing properties** already available for binding (no new backend work needed):

| Property | Type | Used In |
|---|---|---|
| `HasSubscription` | `bool` | Sidebar widget — show/hide plan info |
| `SubscriptionTitle` | `string` | Sidebar widget — "Active Pro Plan" / "No subscription" |
| `DaysLeftText` | `string` | Sidebar widget — "N Days Left" |
| `PlanBadgeText` | `string` | Sidebar badge — "FREE TRIAL" / "PREMIUM" |
| `SubscriptionSubtitle` | `string` | Sidebar widget subtitle |
| `UserEmail` | `string` | Sidebar widget — user identifier |

**New property needed**: `IsExpiryWarning` → `bool` — true if `DaysLeft <= 7`. This drives the
amber warning indicator in the subscription widget.

---

### 5. `RelayRegionPreference` (New — `SettingsViewModel`)

**Location**: `client/RouteXia.App/ViewModels/SettingsViewModel.cs`

**Purpose**: Represents a user's per-relay-region enable/disable preference.

| Field | Type | Description |
|---|---|---|
| `RegionId` | `string` | Matches relay config key (e.g., "SG", "IN", "AE") |
| `DisplayName` | `string` | Human-readable name (e.g., "Singapore", "India", "Dubai") |
| `IsEnabled` | `bool` | User toggle — whether this relay region participates in routing |
| `IsPrimaryPreferred` | `bool` | True if user has pinned this as preferred primary region |

**Collection**: `ObservableCollection<RelayRegionPreference>` added to `SettingsViewModel`, populated
from relay server configuration at app startup. At least one region must remain enabled (validated
before save).

---

### 6. `DesignToken` (Conceptual — ResourceDictionary entries, not a C# class)

**Location**: `client/RouteXia.App/Resources/Styles/`

**Spacing tokens** (new — to add to `ColorPalette.xaml` or new `SpacingTokens.xaml`):

| Key | Value | Use |
|---|---|---|
| `Sp1` | `Thickness(8)` | Tight spacing (badges, inner padding) |
| `Sp2` | `Thickness(16)` | Standard component padding |
| `Sp3` | `Thickness(24)` | Section separation |
| `Sp4` | `Thickness(32)` | Page margin |
| `Sp5` | `Thickness(40)` | Large page margin (existing `Margin="40,28"` → `Sp5` + `Sp3`) |

**Typography tokens** (new named styles to add to `TextStyles.xaml`):

| Key | Font | Size | Weight | Use |
|---|---|---|---|---|
| `HeadingXLStyle` | Rajdhani | 28 | Bold | App logo / screen titles |
| `HeadingLStyle` | Rajdhani | 22 | Bold | Page headings |
| `HeadingMStyle` | Rajdhani | 18 | Bold | Section headings / panel titles |
| `HeadingSmStyle` | Rajdhani | 14 | SemiBold | Card headings |
| `BodyStyle` | Inter | 13 | Regular | Body text (already default TextBlock style) |
| `CaptionStyle` | Inter | 11 | Regular | Muted labels / subtitles |
| `BadgeLabelStyle` | Rajdhani | 11 | SemiBold | Badges / status pills |
| `MonoDataStyle` | JetBrains Mono | 13 | Regular | Already exists — retain |
| `PingNumberStyle` | Rajdhani | 48 | Bold | Already exists — retain |

---

## State Transitions

### Connection Flow (extended)

```
[App Launch]
     │
     ▼
Disconnected ──(user clicks Boost)──► Connecting
     ▲                                    │
     │ (connect failure)                  │ (handshake OK)
     └────────────────────────────────────▼
                                      Connected ◄──┐
                                          │        │ (route rescored, stays connected)
                                          │ (scoring starts)
                                          ▼
                                      Optimizing ──► Connected (loop every 500ms)
                                          │
                                          │ (tunnel drop detected)
                                          ▼
                                   KillSwitchActive ──(reconnect attempt)──► Connecting
```

### Game Detection Flow

```
[_gameProcessTimer tick (every 2s)]
     │
     ├─ Process found matching active profile → GameDetectionResult{IsGameRunning=true, DisplayName=profile.Name}
     │         → IsGameDetected = true, DetectedGameName = profile.Name
     │
     └─ No matching process → GameDetectionResult{IsGameRunning=false}
               → IsGameDetected = false, DetectedGameName = "Waiting for game…"
```

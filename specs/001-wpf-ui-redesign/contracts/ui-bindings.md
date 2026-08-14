# UI Contracts: WPF UI Redesign — Premium Gaming Interface

**Feature**: 001-wpf-ui-redesign
**Date**: 2026-08-14

This document defines the ViewModel-to-View binding contracts. These are the observable properties
that views MUST bind against. Implementation MUST NOT introduce view-layer logic beyond these bindings.

---

## ConnectViewModel Binding Contract

### Connection State

| Property | Type | Description |
|---|---|---|
| `ConnectionState` | `ConnectionState` (enum) | Current state — drives all status area DataTriggers |
| `IsConnected` | `bool` | True when state is Connected or Optimizing |
| `IsConnecting` | `bool` | True when state is Connecting |
| `IsDisconnected` | `bool` | True when state is Disconnected |
| `IsOptimizing` | `bool` | True when state is Optimizing |
| `IsKillSwitchActive` | `bool` | True when state is KillSwitchActive |

### Live Ping Display (existing — retain and extend)

| Property | Type | Description |
|---|---|---|
| `CurrentPingMs` | `string` | Current best-route ping display (e.g., "42 ms") |
| `CurrentJitterMs` | `string` | Current best-route jitter display (e.g., "3 ms") |
| `ActiveRouteLabel` | `string` | Primary relay name (e.g., "Singapore") |

### Route Graph Data (new)

| Property | Type | Description |
|---|---|---|
| `RouteHistory` | `ObservableCollection<RouteSnapshot>` | Latest snapshots for all active routes — graph component consumes this |
| `RouteGraphPoints` | `Dictionary<string, IEnumerable<Point>>` | Pre-computed canvas points per relay, updated every 500ms — optional optimization for complex graphs |

### Game Detection (new)

| Property | Type | Description |
|---|---|---|
| `IsGameDetected` | `bool` | True when a game process matching the active profile is running |
| `DetectedGameName` | `string` | Game display name from profile config; "Waiting for game…" when idle |
| `DetectedGameIconPath` | `string?` | Absolute path to game icon; null → view falls back to generic icon |

---

## AuthViewModel Binding Contract

### Account / Subscription Widget

| Property | Type | Description |
|---|---|---|
| `IsAuthenticated` | `bool` | Controls sidebar widget visibility (login prompt vs. account widget) |
| `UserEmail` | `string` | Displayed in sidebar widget |
| `HasSubscription` | `bool` | True → show plan info; False → show "No plan" warning |
| `SubscriptionTitle` | `string` | "Active Pro Plan" or "No subscription" |
| `DaysLeftText` | `string` | "N Days Left" |
| `PlanBadgeText` | `string` | "FREE TRIAL" or "PREMIUM" |
| `IsExpiryWarning` | `bool` | **NEW** — true if DaysLeft ≤ 7 → amber warning badge |

---

## SettingsViewModel Binding Contract

### Relay Region Preferences (new)

| Property | Type | Description |
|---|---|---|
| `RelayRegions` | `ObservableCollection<RelayRegionPreference>` | All configured relay regions with user toggles |
| `CanSaveRelayPreferences` | `bool` | False if all regions are disabled — blocks save and shows validation message |
| `SaveRelayPreferencesCommand` | `ICommand` | Persists relay preferences; disabled when `CanSave` is false |

---

## Design Token Contract (ResourceDictionary)

All XAML views MUST reference colors and spacing using the following resource keys.
No inline hex values or numeric `Margin`/`Padding` values are permitted outside `ColorPalette.xaml`
and `SpacingTokens.xaml` (exception: `Margin="0"` is acceptable).

### Color Token Keys (existing — must be enforced)

| Key | Value | Usage |
|---|---|---|
| `BgBaseBrush` | `#0A0E14` | Window/page background |
| `BgPanelBrush` | `#10151D` | Panel backgrounds |
| `BgCardBrush` | `#0D1420` | Card backgrounds |
| `BgSidebarBrush` | `#0B0F15` | Sidebar background |
| `BorderBrush` | `#1A2230` | All border strokes |
| `AccentBrush` | `#00C2FF` | Primary accent — active states, highlights |
| `AccentDeepBrush` | `#0B5ED7` | Gradient endpoint, deep accent |
| `TextPrimaryBrush` | `#E8EDF5` | Primary text |
| `TextMutedBrush` | `#6B7684` | Secondary/muted text |
| `StatusGoodBrush` | `#2ED573` | Connected / success |
| `StatusWarnBrush` | `#FFB020` | Warning / expiry alert |
| `StatusBadBrush` | `#FF4757` | Error / kill-switch active |
| `AccentGradientBrush` | `#00C2FF → #0B5ED7` | Gradient fills on primary actions |

### Spacing Token Keys (new — to add)

| Key | Thickness | Usage |
|---|---|---|
| `Sp1` | `8,8,8,8` | Tight / badge padding |
| `Sp2` | `16,16,16,16` | Component inner padding |
| `Sp3` | `24,24,24,24` | Section separation |
| `Sp4` | `32,32,32,32` | Wide margins |
| `Sp5` | `40,40,40,40` | Page content margins |

### Typography Token Keys (named TextBlock Styles)

| Key | Font | Size | Weight |
|---|---|---|---|
| `HeadingXLStyle` | Rajdhani | 28 | Bold |
| `HeadingLStyle` | Rajdhani | 22 | Bold |
| `HeadingMStyle` | Rajdhani | 18 | Bold |
| `HeadingSmStyle` | Rajdhani | 14 | SemiBold |
| `BodyStyle` | Inter | 13 | Regular |
| `CaptionStyle` | Inter | 11 | Regular |
| `BadgeLabelStyle` | Rajdhani | 11 | SemiBold |

---

## Visual State Contract per ConnectionState

| State | Status Icon | Status Label | Accent Color | Animation |
|---|---|---|---|---|
| `Disconnected` | Power icon (grey) | "DISCONNECTED" | `TextMutedBrush` | None |
| `Connecting` | Spinner ring | "CONNECTING…" | `AccentBrush` | Rotating arc (150ms/revolution) |
| `Connected` | Checkmark + glow | "CONNECTED · {region}" | `StatusGoodBrush` | Pulse ring (3s period) |
| `Optimizing` | Animated dot | "OPTIMIZING" (sub-label) | `AccentBrush` | 3-dot chase (500ms cycle) |
| `KillSwitchActive` | Shield-X icon | "KILL-SWITCH ACTIVE" | `StatusBadBrush` | Steady (no animation) |

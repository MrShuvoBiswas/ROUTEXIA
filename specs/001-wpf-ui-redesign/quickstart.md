# Quickstart & Validation Guide: WPF UI Redesign

**Feature**: 001-wpf-ui-redesign
**Date**: 2026-08-14

This guide describes how to build, run, and manually validate the UI redesign feature end-to-end.

---

## Prerequisites

- Windows 10/11 with .NET 8 SDK installed (`dotnet --version` should return 8.x)
- Visual Studio 2022 or `dotnet build` CLI
- The solution file: `client/RouteXia.sln`
- Local relay server NOT required for UI validation (mock data mode is used)

---

## Build & Run

```powershell
# From repo root
cd client
dotnet build RouteXia.sln --configuration Debug
dotnet run --project RouteXia.App/RouteXia.App.csproj
```

Or open `client/RouteXia.sln` in Visual Studio and press **F5**.

---

## Validation Scenarios

### VS-001: Design Token Audit (SC-006, SC-007)

**Purpose**: Verify zero inline color literals and 8px spacing grid compliance.

**Steps**:
1. Run the following in PowerShell from repo root:
   ```powershell
   # Check for inline hex colors in view XAML files (should return 0 results)
   Get-ChildItem "client\RouteXia.App\Views" -Filter "*.xaml" -Recurse |
     Select-String -Pattern '#[0-9A-Fa-f]{3,8}' |
     Where-Object { $_.Line -notmatch 'x:Key|ColorPalette' }
   ```
2. Open `client/RouteXia.App/Resources/Styles/ColorPalette.xaml` — verify all spacing token
   keys (`Sp1`–`Sp5`) are present.
3. Open `client/RouteXia.App/Resources/Styles/TextStyles.xaml` — verify all 7 named styles
   (`HeadingXLStyle` through `BadgeLabelStyle`) are present.

**Expected outcome**: Zero hex color literals in view XAML; all token keys present.

---

### VS-002: Connection State Visual States (FR-003, SC-001)

**Purpose**: Verify all 5 connection states have distinct visual treatments.

**Steps** (requires the app running in Debug mode with test commands):
1. Launch app → verify **Disconnected** state: grey status icon, "DISCONNECTED" label, muted palette.
2. Click "Boost" button → verify **Connecting** state: animated spinner ring visible, label reads "CONNECTING…", accent color active.
3. Wait for connection to establish → verify **Connected** state: green glow badge, region name shown, live ping number visible.
4. Observe the route scoring cycle → verify **Optimizing** sub-state appears briefly (pulsing dot) between Connected refreshes.
5. In Debug mode, trigger kill-switch simulation:
   ```csharp
   // In ConnectViewModel, invoke: SetState(ConnectionState.KillSwitchActive)
   ```
   → Verify **KillSwitchActive** state: red badge, "KILL-SWITCH ACTIVE" label, reconnect button visible.

**Expected outcome**: Each state is visually distinguishable without reading the label text.

---

### VS-003: Real-Time Latency Graph (FR-002, SC-002)

**Purpose**: Verify graph updates with route data at ≤600ms latency from measurement.

**Steps**:
1. Connect to relay (or simulate connection with mock data).
2. Open the Boost view — verify the latency graph is visible.
3. Observe graph for 30 seconds — verify:
   - New data points appear at 500ms intervals (graph scrolls or extends).
   - Each active relay route is shown as a distinct color-coded line.
   - The primary route line is visually emphasized (brighter/thicker).
4. Disconnect — verify graph shows placeholder "Connect to see live stats" state.

**Expected outcome**: Graph updates continuously; routes are color-differentiated; placeholder shown when disconnected.

---

### VS-004: Game Detection Indicator (FR-004, FR-005, SC-003)

**Purpose**: Verify game detection appears within 5 seconds of game launch.

**Steps**:
1. Launch app — verify indicator shows "Waiting for game…" with idle state icon.
2. Launch PUBG PC (or simulate by starting a process named as configured in the active game profile).
3. Wait up to 5 seconds — verify indicator transitions to "Game Detected — [GameName from profile]".
4. Verify the displayed game name matches `GameDefinition.Name` from the config (not hardcoded text).
5. Close the game — verify indicator returns to "Waiting for game…" within one poll cycle (≤5s).

**Expected outcome**: Detection updates correctly; name is profile-sourced; idle state restores on exit.

---

### VS-005: Hover & Transition Performance (FR-008, SC-004)

**Purpose**: Verify all interactive elements respond to hover within 150ms.

**Steps**:
1. Move cursor over: nav buttons (Boost, Protection, Settings, Account, Help).
2. Move cursor over: action cards, stat cards, the connect button.
3. Move cursor over: relay region items in Settings.
4. For each: verify color/glow transition is smooth and completes quickly (subjective: no visible "snap" or lag).

**Expected outcome**: All hover transitions appear instant to human perception (<150ms is imperceptible).

---

### VS-006: Account & Subscription Widget (FR-007)

**Purpose**: Verify subscription status displays correctly in the sidebar.

**Steps**:
1. Log in with a subscribed account → verify sidebar footer shows:
   - Plan badge: "FREE TRIAL" or "PREMIUM"
   - "N Days Left" text
   - User email/username
2. Log in with an account with ≤7 days remaining → verify amber warning indicator is visible.
3. Log out → verify sidebar shows login prompt, not account widget.

**Expected outcome**: Subscription state is accurately reflected; expiry warning appears at ≤7 days.

---

### VS-007: Settings — Relay Region Preferences (FR-006)

**Purpose**: Verify relay region preferences can be toggled and saved.

**Steps**:
1. Open Settings panel → verify all configured relay regions are listed with toggles.
2. Disable one region → verify toggle state updates.
3. Attempt to disable all regions → verify a validation message appears and save is blocked.
4. Close and reopen app → verify the enabled/disabled states persisted.

**Expected outcome**: Region toggles work; at-least-one validation fires correctly; state persists.

---

### VS-008: Minimum Window Size (FR-009, SC-005)

**Purpose**: Verify all primary controls remain accessible at 1080×680.

**Steps**:
1. Drag the window to its minimum size (1080×680 or set via `MainWindow.Width = 1080; Height = 680`).
2. Verify the following remain visible and accessible (no clipping, no overlap):
   - Sidebar with all nav items
   - Boost/Connect button
   - Status area
   - Game detection indicator
   - Latency graph (may be shorter, but must be present)

**Expected outcome**: All primary UI elements accessible at minimum size.

---

## Reference

- UI binding contracts: [ui-bindings.md](contracts/ui-bindings.md)
- Data model: [data-model.md](data-model.md)
- Research decisions: [research.md](research.md)
- Spec: [spec.md](spec.md)

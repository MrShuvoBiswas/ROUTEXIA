# Tasks: WPF UI Redesign — Premium Gaming Interface

**Feature**: `001-wpf-ui-redesign`
**Branch**: `001-wpf-ui-redesign`
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md)
**Contracts**: [contracts/ui-bindings.md](contracts/ui-bindings.md) | **Validation**: [quickstart.md](quickstart.md)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the design token foundation — every subsequent phase depends on these resources.

- [x] T001 Create `client/RouteXia.App/Resources/Styles/SpacingTokens.xaml` with `Thickness` tokens `Sp1` (8), `Sp2` (16), `Sp3` (24), `Sp4` (32), `Sp5` (40) as `StaticResource` keys
- [x] T002 Register `SpacingTokens.xaml` in `client/RouteXia.App/App.xaml` `MergedDictionaries` list (after `ColorPalette.xaml`)
- [x] T003 [P] Extend `client/RouteXia.App/Resources/Styles/TextStyles.xaml` — add 5 named styles: `HeadingXLStyle` (Rajdhani Bold 28), `HeadingLStyle` (Rajdhani Bold 22), `HeadingMStyle` (Rajdhani Bold 18), `HeadingSmStyle` (Rajdhani SemiBold 14), `CaptionStyle` (Inter 11 TextMuted), `BadgeLabelStyle` (Rajdhani SemiBold 11)
- [x] T004 [P] Create `client/RouteXia.App/Resources/Animations/StatusTransitions.xaml` with `Storyboard` resources: `ConnectingSpinnerStoryboard` (360° rotation, 1.2s linear infinite), `OptimizingPulseStoryboard` (opacity 1→0.3→1, 0.6s ease), `ConnectedGlowStoryboard` (opacity 0.6→1→0.6, 3s ease infinite)

**Checkpoint**: Design token layer complete — `SpacingTokens.xaml` and updated `TextStyles.xaml` registered in app. Build compiles without errors.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: ViewModel extensions and data model additions that all UI phases depend on.

> ⚠️ **CRITICAL**: No user story view work can begin until this phase is complete.

- [x] T005 Add `Optimizing` and `KillSwitchActive` values to the `ConnectionState` enum in `client/RouteXia.App/ViewModels/ConnectViewModel.cs` (lines 19-24); add matching bool computed properties `IsOptimizing` and `IsKillSwitchActive` following the existing `IsConnected`/`IsConnecting` pattern
- [x] T006 Add `RouteSnapshot` record to `client/RouteXia.App/ViewModels/ConnectViewModel.cs` with fields: `RelayName (string)`, `RelayId (string)`, `PingMs (double)`, `JitterMs (double)`, `Score (double)`, `IsActivePrimary (bool)`, `IsAlive (bool)`, `SampledAt (DateTimeOffset)`
- [x] T007 Add `RouteHistory` as `ObservableCollection<RouteSnapshot>` property to `ConnectViewModel`; add private `Dictionary<string, Queue<RouteSnapshot>>` ring buffer (max 120 entries per relay key) in `client/RouteXia.App/ViewModels/ConnectViewModel.cs`; expose public `AddRouteSnapshot(RouteSnapshot snap)` method that enqueues and notifies
- [x] T008 Add `IsGameDetected (bool)`, `DetectedGameName (string)`, `DetectedGameIconPath (string?)` observable properties to `ConnectViewModel` in `client/RouteXia.App/ViewModels/ConnectViewModel.cs`; extend the existing `_gameProcessTimer` callback to set these from `CurrentGame.Name` and a profile icon path (fallback to null when no game running)
- [ ] T009 Add `IsExpiryWarning` computed property to `client/RouteXia.App/ViewModels/AuthViewModel.cs` — returns `true` when `_apiClient.CurrentSubscription?.DaysLeft <= 7 && HasSubscription`; fire `OnPropertyChanged` from `HasSubscription` setter
- [ ] T010 Add `RelayRegionPreference` record class to `client/RouteXia.App/ViewModels/SettingsViewModel.cs` with properties: `RegionId (string)`, `DisplayName (string)`, `IsEnabled (bool, INotifyPropertyChanged)`, `IsPrimaryPreferred (bool, INotifyPropertyChanged)`
- [ ] T011 Add `RelayRegions (ObservableCollection<RelayRegionPreference>)` property to `SettingsViewModel` in `client/RouteXia.App/ViewModels/SettingsViewModel.cs`; populate with hardcoded relay entries: Singapore (SG), India (IN), Dubai (AE) all enabled by default; add `CanSaveRelayPreferences (bool)` computed property (false if all disabled); add `SaveRelayPreferencesCommand (ICommand)` that validates `CanSaveRelayPreferences` before persisting

**Checkpoint**: Build compiles. All new ViewModel properties exist and are bindable. Run VS-002 state enum check — `ConnectionState.Optimizing` and `ConnectionState.KillSwitchActive` are accessible in code.

---

## Phase 3: User Story 4 — Connection Status States (Priority: P1) ★ MVP

**Goal**: Deliver 5 visually distinct connection state treatments in the `ConnectView` status area.

**Independent Test**: VS-002 in `quickstart.md` — trigger each of 5 states, verify distinct visual treatment.

### Implementation for User Story 4

- [x] T012 [US4] In `client/RouteXia.App/Views/ConnectView.xaml`, replace the existing connection status `Border`/`TextBlock` block with a new `Grid x:Name="StatusArea"` containing 5 state layers (one per `ConnectionState`), each controlled by a `DataTrigger` on `{Binding ConnectionState}`; layers: `DisconnectedState`, `ConnectingState`, `ConnectedState`, `OptimizingState`, `KillSwitchState`
- [x] T013 [US4] Implement `DisconnectedState` layer in `client/RouteXia.App/Views/ConnectView.xaml`: grey power icon (`SymbolIcon Symbol="Power24"`, `TextMutedBrush`), label "DISCONNECTED" (`BadgeLabelStyle`, `TextMutedBrush`), prominent "BOOST" connect button (`ConnectButtonStyle`); `Visibility` driven by `DataTrigger ConnectionState=Disconnected`
- [x] T014 [US4] Implement `ConnectingState` layer in `client/RouteXia.App/Views/ConnectView.xaml`: animated spinner arc `Path` with `ConnectingSpinnerStoryboard` from `StatusTransitions.xaml`, label "CONNECTING…" (`BadgeLabelStyle`, `AccentBrush`); wire storyboard `BeginStoryboard` to `DataTrigger EnterActions` and `RemoveStoryboard` to `ExitActions`
- [x] T015 [US4] Implement `ConnectedState` layer in `client/RouteXia.App/Views/ConnectView.xaml`: green glow `Ellipse` (filled `StatusGoodBrush` with `ConnectedGlowStoryboard`), checkmark icon, label "CONNECTED" + `{Binding ActiveRouteLabel}` region name + live `{Binding CurrentPingMs}` ping display (`PingNumberStyle`); `Visibility` driven by `DataTrigger ConnectionState=Connected`
- [x] T016 [US4] Implement `OptimizingState` layer in `client/RouteXia.App/Views/ConnectView.xaml`: 3-dot animated indicator using `OptimizingPulseStoryboard` (staggered opacity delays on each dot), sub-label "OPTIMIZING" in `AccentBrush`; overlay on top of `ConnectedState` (z-order: Optimizing sub-label below main ping readout, not full replacement); `Visibility` driven by `DataTrigger ConnectionState=Optimizing`
- [x] T017 [US4] Implement `KillSwitchState` layer in `client/RouteXia.App/Views/ConnectView.xaml`: shield-X icon (`SymbolIcon Symbol="ShieldDismiss24"`, `StatusBadBrush`), label "KILL-SWITCH ACTIVE" (`BadgeLabelStyle`, `StatusBadBrush`), reconnect `Button` with text "Reconnect" styled with `StatusBadBrush` border; `Visibility` driven by `DataTrigger ConnectionState=KillSwitchActive`
- [x] T018 [US4] Audit all inline `Margin`, `Padding`, `Background`, and `Foreground` values in `client/RouteXia.App/Views/ConnectView.xaml` for the modified status area block; replace any hex color literals with `StaticResource` token keys and any numeric spacing with `StaticResource Sp*` tokens

**Checkpoint**: VS-002 complete — all 5 states are visually distinct. App compiles and runs. No hex literals in modified XAML block.

---

## Phase 4: User Story 1 — First Launch Gaming Identity (Priority: P1)

**Goal**: Enforce the design system across the full app shell (`MainWindow.xaml`, nav sidebar, top bar).

**Independent Test**: Launch app fresh → inspect that no WPF default surfaces (white/grey) are visible, all headings use Rajdhani, all hover states animate.

### Implementation for User Story 1

- [x] T019 [P] [US1] Audit `client/RouteXia.App/Views/MainWindow.xaml` — replace all inline hex literals (`#132636`, `#111F2B`, `#0F1723`, `#243244`, `#E6…`) with the appropriate `StaticResource` token keys per `contracts/ui-bindings.md`; run PowerShell audit from VS-001 after
- [x] T020 [P] [US1] Audit `client/RouteXia.App/Resources/Styles/ButtonStyle.xaml` — replace any inline hex values with token keys; ensure all button hover `Trigger` blocks use `AccentBrush`, `CardHoverBrush`, `NavActiveBgBrush` from palette; verify `Duration="0:0:0.15"` on all `ColorAnimation` elements
- [x] T021 [P] [US1] Audit `client/RouteXia.App/Resources/Styles/CardStyles.xaml` — replace any inline hex values; verify all `Border` styles reference token keys only
- [x] T022 [US1] In `client/RouteXia.App/Views/MainWindow.xaml`, update the sidebar `ROUTEXIA` logo `TextBlock` to use `HeadingXLStyle`; update the hardcoded "PUBG SG" badge `TextBlock` — this badge must be data-driven: bind its text to a ViewModel property (e.g., `ConnectViewModel.ActiveRouteLabel`) and hide it when disconnected; replace the `#132636` badge background with `NavActiveBgBrush`
- [x] T023 [US1] Add hover `ControlTemplate.Triggers` to the `NavButtonStyle` in `client/RouteXia.App/Resources/Styles/ButtonStyle.xaml` that animate `Background` from `BgSidebarBrush` to `NavHoverBgBrush` over 150ms; add an active state trigger (`Tag="active"`) that sets `Background="NavActiveBgBrush"` and `Foreground="AccentBrush"` with a left accent stripe (2px `Rectangle` in `AccentBrush`)
- [x] T024 [US1] In `client/RouteXia.App/Views/MainWindow.xaml`, update the top bar `TextBlock` "PUBG optimizer ready" — remove hardcoded game reference; bind text to `ConnectViewModel.DetectedGameName` (e.g., shows "PUBG PC optimizer ready" when game is detected, "RouteXia ready" when idle); update top bar status icon `Foreground` to bind to connection state color via a `DataTrigger`

**Checkpoint**: VS-001 token audit script returns 0 hex literals in view XAML files. Hover transitions visible on all nav buttons. Top bar text is dynamic.

---

## Phase 5: User Story 3 — Real-Time Route Visualization (Priority: P2)

**Goal**: Live latency graph in the Boost view showing route ping over 60-second rolling window.

**Independent Test**: VS-003 in `quickstart.md` — connect, observe graph updating at 500ms intervals, verify color-coded route lines with primary route emphasis.

### Implementation for User Story 3

- [x] T025 [US3] Create `client/RouteXia.App/Views/LatencyGraphControl.xaml` as a WPF `UserControl` with a `Canvas x:Name="GraphCanvas"` backed by a `Polyline` per relay route; accept `RouteHistory (ObservableCollection<RouteSnapshot>)` as a dependency property; subscribe to `CollectionChanged` to redraw
- [x] T026 [US3] Implement `client/RouteXia.App/Views/LatencyGraphControl.xaml.cs` — `RedrawGraph()` method: iterate `ConnectViewModel.RouteHistory`, group by `RelayId`, map `(index, PingMs)` to canvas `Point` objects clamped to canvas height, assign a distinct `Stroke` color per relay from a fixed palette (`AccentBrush` for primary, `StatusWarnBrush` for secondary, `TextMutedBrush` for standby); emphasize `IsActivePrimary` route with `StrokeThickness="2.5"` vs `1.5` for others
- [x] T027 [US3] Add Y-axis reference lines to `LatencyGraphControl.xaml`: horizontal dashed `Line` elements at 50ms, 100ms, 200ms canvas positions; label each with a small `TextBlock` (CaptionStyle, TextMutedBrush); add route name labels at the right edge of each `Polyline`
- [x] T028 [US3] Add placeholder state to `LatencyGraphControl.xaml`: when `RouteHistory` is empty or `IsConnected=false`, show a centered `TextBlock` ("Connect to see live stats", `CaptionStyle`, `TextMutedBrush`) and hide the graph canvas
- [x] T029 [US3] Embed `LatencyGraphControl` in `client/RouteXia.App/Views/ConnectView.xaml` within the main Boost view content area (below the status area); bind `RouteHistory="{Binding RouteHistory}"` and `IsConnected="{Binding IsConnected}"`; size the graph to approximately 320px height within a `Border` using `InfoCardStyle`
- [x] T030 [US3] In `client/RouteXia.App/ViewModels/ConnectViewModel.cs`, wire the `_statsTimer` callback (already exists) to call `AddRouteSnapshot()` for each active relay using data from `MultipathRouter` route metrics (existing `_router` field); ensure the ring buffer is cleared on disconnect

**Checkpoint**: VS-003 — graph visible when connected, updates at 500ms intervals, distinct colored lines per route, primary route emphasized, placeholder when disconnected.

---

## Phase 6: User Story 2 — Game Detection Indicator (Priority: P2)

**Goal**: Game detection indicator in sidebar showing auto-detected game name from profile config.

**Independent Test**: VS-004 in `quickstart.md` — launch game, detection appears within 5s with profile-sourced name.

### Implementation for User Story 2

- [ ] T031 [US2] In `client/RouteXia.App/Views/MainWindow.xaml`, add a `GameDetectionIndicator` block to the sidebar (between nav items and the footer widget) as a `Border` with `InfoCardStyle`; bind `Visibility` to show always; contains: game icon `Image` (binds `DetectedGameIconPath`, fallback to generic icon when null), game name `TextBlock` (binds `DetectedGameName`), animated pulse dot (green `Ellipse` with `ConnectedGlowStoryboard` when `IsGameDetected=true`, grey static when false)
- [ ] T032 [US2] In `client/RouteXia.App/Views/MainWindow.xaml`, add a `DataTrigger` on `IsGameDetected=false` to the game detection block: set background to `BgCardBrush` with `TextMutedBrush` text (idle state); on `IsGameDetected=true`: set border to `StatusGoodBrush` (1px), icon + name to primary text — all via ResourceDictionary tokens, no inline values
- [ ] T033 [US2] In `client/RouteXia.App/Views/MainWindow.xaml`, add a generic game icon fallback: define an `Image Source` value converter or use a `DataTrigger` on `DetectedGameIconPath=null` to swap `Image Source` to a bundled `SymbolIcon Symbol="Games24"` placeholder; ensure the ViewModel's `DetectedGameIconPath` is populated from `GameDefinition` profile only

**Checkpoint**: VS-004 — game indicator shows idle state at launch, transitions to detected state within 5s of game launch, uses profile-sourced name, returns to idle on game exit.

---

## Phase 7: User Story 6 — Account & Subscription Widget (Priority: P3)

**Goal**: Subscription status widget in sidebar footer replacing the current static "Ready for match" card.

**Independent Test**: VS-006 in `quickstart.md` — login with subscribed account, verify plan badge, days left, username visible; verify amber warning when ≤7 days.

### Implementation for User Story 6

- [ ] T034 [US6] In `client/RouteXia.App/Views/MainWindow.xaml`, replace the static "Ready for match / SG route active" `Border` (lines 145-165) with a new `AccountWidget` `Border` (`InfoCardStyle`); structure: top row with `PlanBadgeText` badge + `UserEmail` truncated; middle row with `SubscriptionTitle`; bottom row with `DaysLeftText`; all text via `StaticResource` token styles
- [ ] T035 [US6] Add `DataTrigger IsExpiryWarning=true` to the `AccountWidget` border in `client/RouteXia.App/Views/MainWindow.xaml`: change border color to `StatusWarnBrush`, add a small amber warning icon (`SymbolIcon Symbol="Warning24"`, `StatusWarnBrush`) before `DaysLeftText`
- [ ] T036 [US6] Add `DataTrigger IsAuthenticated=false` to the `AccountWidget` in `client/RouteXia.App/Views/MainWindow.xaml`: replace the account info content with a "Sign In" inline prompt (`TextBlock "Not signed in"`, `CaptionStyle`) and a small "Sign In →" `Button` that navigates to `AuthView`; ensure the `AuthViewModel` instance is accessible from `MainWindow` code-behind

**Checkpoint**: VS-006 — widget shows plan badge + days left when subscribed; amber warning when ≤7 days; sign-in prompt when logged out.

---

## Phase 8: User Story 5 — Settings: Relay Region Preferences (Priority: P3)

**Goal**: Relay region preference section in Settings panel with per-region enable/disable toggles.

**Independent Test**: VS-007 in `quickstart.md` — toggle regions, attempt to disable all (blocked), close/reopen to verify persistence.

### Implementation for User Story 5

- [ ] T037 [US5] In `client/RouteXia.App/Views/SettingsView.xaml`, add a new `RELAY REGIONS` settings section above the existing `GAME OPTIMIZATION` section; section header using `SectionHeaderStyle`; contains an `ItemsControl` bound to `{Binding RelayRegions}` from `SettingsViewModel`
- [ ] T038 [US5] Define the `ItemsControl.ItemTemplate` in `client/RouteXia.App/Views/SettingsView.xaml` for each `RelayRegionPreference` row: left side `TextBlock` showing `DisplayName` (`BodyStyle`), right side `ToggleSwitch IsChecked="{Binding IsEnabled}"` + a star `Button` for `IsPrimaryPreferred` (filled `AccentBrush` when true, `TextMutedBrush` when false); rows separated by `SectionSeparatorStyle` `Rectangle`
- [ ] T039 [US5] Add a `Save Preferences` `Button` below the `ItemsControl` in `client/RouteXia.App/Views/SettingsView.xaml` bound to `{Binding SaveRelayPreferencesCommand}`; add a validation message `TextBlock` "At least one relay region must be enabled" bound to show when `CanSaveRelayPreferences=false`, hidden otherwise; style the button disabled state with `TextMutedBrush`
- [ ] T040 [US5] Implement persistence in `client/RouteXia.App/ViewModels/SettingsViewModel.cs` `SaveRelayPreferencesCommand` handler: serialize `RelayRegions` collection to JSON and write to `%LOCALAPPDATA%\RouteXia\relay-prefs.json`; load from same path on `SettingsViewModel` constructor initialization with fallback to all-enabled defaults

**Checkpoint**: VS-007 — region toggles work; at-least-one validation fires correctly; preferences persist across app restarts.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Token audit, minimum-size validation, and design consistency pass.

- [ ] T041 [P] Run PowerShell token audit (VS-001) across ALL view XAML files in `client/RouteXia.App/Views/` and `client/RouteXia.App/Resources/Styles/`; fix any remaining inline hex literals found; target: 0 results outside `ColorPalette.xaml`
- [ ] T042 [P] Verify 8px grid compliance (SC-007): spot-check `Margin` and `Padding` values in `MainWindow.xaml`, `ConnectView.xaml`, `SettingsView.xaml`; replace any values not divisible by 8 (tolerance: ±4px for optical alignment) with the nearest `Sp*` token
- [ ] T043 Set `client/RouteXia.App/App.xaml` default `Background` fallback on `Application` element to `#0A0E14` as a last-resort dark fallback in case `ColorPalette.xaml` fails to load
- [ ] T044 [P] Test minimum window size: resize `MainWindow` to exactly 1080×680; verify sidebar, status area, Boost button, game detection indicator, and latency graph are all visible and not clipped; fix any layout issues in `client/RouteXia.App/Views/MainWindow.xaml` or `ConnectView.xaml`
- [ ] T045 [P] Verify `LatencyGraphControl` cleans up: confirm `CollectionChanged` subscription is unsubscribed in `UserControl.Unloaded` event handler in `client/RouteXia.App/Views/LatencyGraphControl.xaml.cs` to prevent memory leaks
- [ ] T046 Run full `quickstart.md` validation suite (VS-001 through VS-008); document any remaining issues as follow-up work items; confirm all 7 success criteria (SC-001 through SC-007) are met

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup / Design Tokens)
  └─► Phase 2 (ViewModel Foundations)
        ├─► Phase 3 (US4: Connection States)     ← P1 MVP
        ├─► Phase 4 (US1: Gaming Identity)       ← P1 MVP
        ├─► Phase 5 (US3: Route Graph)           ← P2
        ├─► Phase 6 (US2: Game Detection)        ← P2
        ├─► Phase 7 (US6: Account Widget)        ← P3
        └─► Phase 8 (US5: Settings Regions)      ← P3
              All ↓
           Phase 9 (Polish & Audit)
```

### User Story Dependencies

| Story | Depends On | Can Parallelize With |
|---|---|---|
| US4 Connection States | Phase 1 + Phase 2 (T005) | US1 (different view sections) |
| US1 Gaming Identity | Phase 1 + Phase 2 (T005) | US4 (different view sections) |
| US3 Route Graph | Phase 1 + Phase 2 (T006, T007) | US2, US6, US5 |
| US2 Game Detection | Phase 1 + Phase 2 (T008) | US3, US6, US5 |
| US6 Account Widget | Phase 1 + Phase 2 (T009) | US3, US2, US5 |
| US5 Settings Regions | Phase 1 + Phase 2 (T010, T011) | US3, US2, US6 |

### Within Each Phase

- Token resources (T001–T004) should be committed before any view work begins
- ViewModel additions (T005–T011) can proceed in parallel across the 3 files
- View tasks within each story can begin once their ViewModel dependencies are committed

---

## Parallel Execution Examples

### Phase 1 & 2 Parallel (different files)

```
T001 SpacingTokens.xaml ─────────────────────────────┐
T003 TextStyles.xaml extensions ────────────────────┤  ← All 4 in parallel
T004 StatusTransitions.xaml ────────────────────────┤
T002 App.xaml registration (after T001) ────────────┘

T005 ConnectionState enum (ConnectViewModel.cs) ────┐
T009 IsExpiryWarning (AuthViewModel.cs) ────────────┤  ← All 7 in parallel (different files)
T010 RelayRegionPreference (SettingsViewModel.cs) ──┤
T011 RelayRegions collection (SettingsViewModel.cs) ┘
T006 RouteSnapshot record ──────────────────────────┐
T007 RouteHistory ring buffer ──────────────────────┤  ← Sequential (same file, ConnectViewModel.cs)
T008 GameDetection properties ──────────────────────┘
```

### After Phase 2: All Stories Can Run in Parallel

```
Developer A: T012–T018 (US4: Connection States in ConnectView.xaml)
Developer B: T019–T024 (US1: Gaming Identity in MainWindow.xaml + styles)
Developer C: T025–T030 (US3: LatencyGraphControl - new file)
```

---

## Implementation Strategy

### MVP First (P1 Stories Only — US4 + US1)

1. ✅ Complete Phase 1: Design Tokens (T001–T004)
2. ✅ Complete Phase 2 Minimum: T005 only (ConnectionState enum)
3. ✅ Complete Phase 3: US4 Connection States (T012–T018)
4. ✅ Complete Phase 4: US1 Gaming Identity shell (T019–T024)
5. **STOP & VALIDATE**: VS-001 token audit + VS-002 state visual check
6. Ship: polished, premium shell with proper connection states

### Incremental Delivery

1. MVP: Phases 1+2+3+4 → Premium shell + connection states
2. Add Phase 5 (US3): Route graph → live data visualization
3. Add Phase 6 (US2): Game detection → auto-detect indicator
4. Add Phase 7+8 (US6+US5): Account widget + region settings
5. Phase 9: Full audit + polish pass

---

## Notes

- `[P]` tasks operate on different files or independent sections — safe to run in parallel
- `[US*]` label maps each task to its user story for traceability to `spec.md`
- Every hex literal added to view XAML during implementation is a spec violation (SC-006) — check before committing
- The `LatencyGraphControl` is a new file with no existing code to break — lowest-risk parallel track
- `ConnectViewModel.cs` is large (1012 lines) — coordinate with teammates to avoid merge conflicts on T005–T008
- Commit after each phase checkpoint, not after every task

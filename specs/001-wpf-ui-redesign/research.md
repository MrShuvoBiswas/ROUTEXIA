# Research: WPF UI Redesign — Premium Gaming Interface

**Feature**: 001-wpf-ui-redesign
**Date**: 2026-08-14
**Status**: Complete — all unknowns resolved from codebase inspection

---

## Decision 1: Real-Time Latency Graph Approach in WPF

**Decision**: Use a custom WPF `Canvas`/`Polyline`-based graph component (no third-party charting library)
drawn via code-behind or a lightweight `DrawingVisual` approach, backed by a `Queue<double>` ring buffer
in the ViewModel that holds 60 seconds of ping samples per route (120 samples at 500ms intervals).

**Rationale**: The existing codebase uses zero third-party charting libraries. Adding LiveCharts2 or OxyPlot
would introduce a new dependency and a package-management decision. A bespoke Canvas + Polyline graph gives
full styling control (matching the dark token palette exactly), avoids license concerns, and keeps binary
size small. The graph's data model is simple: a fixed-length ring buffer of `(timestamp, pingMs)` tuples
per route. WPF's retained-mode rendering handles 120-point polylines with negligible CPU cost.

**Alternatives considered**:
- **LiveCharts2 for WPF** — powerful, but heavyweight (~2MB), complex theming API, and overkill for a
  line-only graph showing 2-3 routes.
- **OxyPlot** — mature but has WinForms aesthetic defaults that require extensive restyling to match dark
  gaming theme.
- **Canvas + DrawingVisual** — maximum performance, but more complex hit-testing code; unnecessary for
  a non-interactive display graph.

---

## Decision 2: ConnectionState Enum Extension

**Decision**: Extend the existing `ConnectionState` enum in `ConnectViewModel.cs` from 3 values
(`Disconnected`, `Connecting`, `Connected`) to 5: add `Optimizing` and `KillSwitchActive`.
Use WPF `DataTrigger` bindings in the `ConnectView.xaml` to drive visual state changes from this enum.

**Rationale**: The enum already exists and is databound throughout the view. Adding two values is
non-breaking (existing switch arms remain valid). This approach avoids introducing a separate
`StatusViewModel` and keeps the connection state machine in a single place.

**Alternatives considered**:
- **Separate `UIConnectionState` enum** — cleaner separation, but requires a mapping layer; unnecessary
  given the existing ViewModel is already the single source of truth.
- **VisualStateManager (VSM)** — WPF's first-class state management tool, but its XML-heavy definition
  in XAML is harder to maintain than DataTrigger for this scope.

---

## Decision 3: Design Token Enforcement — Linting Strategy

**Decision**: Enforce the "no inline color literals" rule via a code review gate (documented in
`quickstart.md` as a manual audit step). A Roslyn analyzer or XAML lint rule is deferred to a
future hardening task. The current scope (redesign, not CI pipeline) makes a manual audit sufficient.

**Rationale**: The spec's SC-006 requires zero ad-hoc color literals in view XAML. This can be
validated at PR review time by searching for hex patterns (`#[0-9A-Fa-f]{3,8}`) in XAML files
outside of `ColorPalette.xaml`. A full Roslyn custom analyzer is a separate investment.

**Alternatives considered**:
- **Roslyn XAML analyzer** — ideal long-term, but requires a separate NuGet package project; out of
  scope for this feature.
- **Pre-commit hook + grep** — lightweight, but fragile with multi-line hex values; adequate as a
  supplemental check.

---

## Decision 4: Spacing Grid — 8px Base

**Decision**: Codify an 8px base spacing grid via named `Thickness` resources in `ColorPalette.xaml`
or a new `SpacingTokens.xaml` dictionary. Token names follow the scale: `Sp1=8, Sp2=16, Sp3=24, Sp4=32`.
Existing inline `Margin` values in all views will be audited and migrated to use the nearest token.

**Rationale**: SC-007 requires all spacing to conform to an 8px grid. Without named tokens, developers
will continue writing ad-hoc `Margin="20,28"` values. Named `Thickness` resources make violations
immediately visible in code review.

**Alternatives considered**:
- **Inline enforcement only** — easier to start, but creates drift; tokens are the only durable solution.
- **4px grid** — finer control, but requires more tokens with diminishing visual benefit for a desktop app
  at 1080×680+.

---

## Decision 5: Game Detection Indicator Data Binding

**Decision**: Add `GameDetectionStatus` and `DetectedGameDisplayName` observable properties to
`ConnectViewModel`. These will be populated by the existing `_gameProcessTimer` (already polling at
regular intervals). No new service layer is required — the timer callback will be extended.

**Rationale**: `ConnectViewModel.cs` already has `private Timer? _gameProcessTimer` (line 83).
The game display name must come from `GameDefinition.Name` (the profile model), never from a hardcoded
string — this is already the pattern used by `SelectedGameTitle => CurrentGame.Name`.

**Alternatives considered**:
- **Dedicated `GameDetectionService`** — better separation, but adds architectural complexity for a
  timer-poll already present in the ViewModel.

---

## Decision 6: Account/Subscription UI Surface Placement

**Decision**: Add a subscription status widget to the sidebar footer (currently a static "Ready for match"
card in `MainWindow.xaml` lines 145-165). This widget will bind to `AuthViewModel.HasSubscription`,
`AuthViewModel.DaysLeftText`, and `AuthViewModel.PlanBadgeText` which already exist in `AuthViewModel.cs`.

**Rationale**: The `AuthViewModel` already exposes `HasSubscription`, `DaysLeftText`, `PlanBadgeText`,
`SubscriptionTitle`, and `SubscriptionSubtitle` (lines 70-78 of `AuthViewModel.cs`). No new backend work
is required — only the UI surface needs to be added/redesigned. The sidebar footer is the least disruptive
location and is consistent with ExitLag's pattern.

**Alternatives considered**:
- **Dedicated Account tab** — already exists as `BtnNavAccount`; the footer widget supplements it, providing
  at-a-glance status without a full tab switch.

---

## Decision 7: Typography Scale Formalization

**Decision**: The existing three fonts (Inter, Rajdhani, JetBrains Mono) and their named styles in
`TextStyles.xaml` are retained and extended. Missing named styles to add: `HeadingXLStyle` (Rajdhani
Bold 28), `HeadingLStyle` (Rajdhani Bold 22), `HeadingMStyle` (Rajdhani SemiBold 18), `BodyStyle`
(Inter 13 — already default), `CaptionStyle` (Inter 11 Muted), `StatValueStyle` (Rajdhani Bold 48 —
already `PingNumberStyle`), `BadgeLabelStyle` (Rajdhani SemiBold 11).

**Rationale**: The existing `TextStyles.xaml` defines 3 named styles. The redesign requires consistent
headings at multiple scales. Naming all styles prevents ad-hoc inline font settings.

**Alternatives considered**:
- **Single fluid scale** — CSS-equivalent, but WPF has no computed property system; explicit named styles
  are the WPF-idiomatic approach.

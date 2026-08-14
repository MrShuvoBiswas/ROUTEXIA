# Feature Specification: WPF UI Redesign — Premium Gaming Interface

**Feature Branch**: `001-wpf-ui-redesign`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Redesign ROUTEXIA's WPF UI into a polished, production-grade gaming utility interface."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — First Launch: Instant Gaming Identity (Priority: P1)

A gamer launches RouteXia for the first time after installation. Within seconds they see a dark, high-contrast
interface that unmistakably belongs in a gaming context — not a generic enterprise tool. The design system
communicates premium quality through consistent color tokens, typography, spacing, and micro-animations
before the user even interacts with a control.

**Why this priority**: First impression is irreversible. If the UI reads "cheap" or "generic", users
uninstall within 30 seconds regardless of routing quality. Visual identity is the product's first
credibility signal.

**Independent Test**: Can be tested by opening the app with no active game and no saved configuration
and evaluating whether every visible element conforms to the design system (color tokens, font scale,
spacing scale) and whether the overall aesthetic matches gaming-native premium apps like ExitLag or Discord.

**Acceptance Scenarios**:

1. **Given** the app is freshly installed with no configuration, **When** the main window opens,
   **Then** the entire window renders in the dark theme design system with no white/grey generic WPF
   default surfaces visible, and branded typography (Rajdhani or equivalent gaming font) is used for
   all headings.

2. **Given** the app is open, **When** the user hovers any interactive element (button, nav item, card),
   **Then** a smooth hover transition (color shift or glow) plays within 150ms, providing immediate
   tactile feedback without jarring animation.

3. **Given** the app is open, **When** no connection is active, **Then** the UI clearly communicates
   the "Disconnected" state with a distinct visual treatment (muted palette, appropriate status icon)
   that differs visually from the "Connected" state — distinguishable at a glance without reading text.

---

### User Story 2 — Game Detection: Automatic PUBG Recognition (Priority: P2)

A user launches PUBG PC (TslGame.exe). RouteXia automatically detects the running game process and
updates its UI to show a "Game Detected" indicator with the game name and icon. The user does not need
to manually select a game profile — detection is automatic and prominently surfaced.

**Why this priority**: Automatic game detection is a core differentiator. The UI must make this
capability visible and confidence-building. A user who doesn't notice detection happened won't trust
the product.

**Independent Test**: Can be tested by launching the app, then launching a game process that matches
a configured profile, and observing whether the game detection indicator appears and shows the correct
game name within 5 seconds.

**Acceptance Scenarios**:

1. **Given** no game is running, **When** the app is open on the Boost view, **Then** a game detection
   area shows a "Waiting for game..." or "No game detected" state with a subtle idle animation.

2. **Given** PUBG PC (TslGame.exe) starts, **When** RouteXia detects the process, **Then** the game
   detection indicator transitions to "Game Detected — PUBG PC" with the game's icon/logo and a green
   status highlight, within 5 seconds of game launch.

3. **Given** a game is detected, **When** the user closes the game, **Then** the game detection indicator
   returns to the idle state without requiring any manual action.

4. **Given** a game is detected, **When** the user reads the detection area, **Then** it shows the
   detected process name sourced from the active profile config — it never hardcodes "PUBG" or any
   game name in the UI code itself.

---

### User Story 3 — Real-Time Route Visualization (Priority: P2)

A connected user wants to understand what RouteXia is doing for them right now. The Boost view displays
a live latency graph showing route ping over a rolling time window, with labeled relay endpoints (e.g.,
"Singapore", "India") and a clear visual indicator of which route is currently the primary active path.

**Why this priority**: Visualization transforms a black-box routing service into a transparent tool
that builds trust. Users who can see their ping drop stay subscribed.

**Independent Test**: Can be tested in a connected state by observing whether the graph updates at
least every 500ms with new data points and whether the active route is visually distinguished from
standby routes.

**Acceptance Scenarios**:

1. **Given** a connection is active, **When** the Boost view is open, **Then** a live ping/latency
   graph updates with new data points at least every 500ms, showing a rolling window of at least the
   last 60 seconds.

2. **Given** multiple relay routes are active, **When** the graph renders, **Then** each route is
   represented by a distinct color-coded line (matching the route's regional label), and the currently
   primary route is visually emphasized (brighter line or highlighted label).

3. **Given** a route switches (primary route changes due to scoring), **When** the graph updates,
   **Then** the visual emphasis shifts to the new primary route label within one render cycle.

4. **Given** no connection is active, **When** the Boost view is open, **Then** the graph area shows
   a placeholder state ("Connect to see live stats") rather than an empty or broken chart.

---

### User Story 4 — Connection Status States (Priority: P1)

A user interacts with the Connect/Boost button. The UI communicates every state transition — from
Disconnected → Connecting → Connected → Optimizing — through distinct, unambiguous visual treatments.
There is never a moment where the user is unsure whether the app is working.

**Why this priority**: Ambiguous connection state is the most common user complaint in VPN/accelerator
apps. Clear state communication directly reduces support volume and churn.

**Independent Test**: Can be tested by triggering each state (disconnect, initiate connect, complete
connect, force error) and verifying that the status area displays a unique visual treatment for each.

**Acceptance Scenarios**:

1. **Given** the app is idle, **When** the Boost view is shown, **Then** the status area clearly
   shows "Disconnected" with a neutral/grey indicator and a prominent "Boost" or "Connect" call-to-action.

2. **Given** the user clicks Connect, **When** the connection is being established, **Then** the status
   area shows "Connecting…" with an animated progress indicator (spinner or pulsing ring) replacing
   the static icon.

3. **Given** the connection is established, **When** the status updates, **Then** the status area shows
   "Connected" with a green accent glow, the active relay region name, and the current ping reading.

4. **Given** the connection is active and route optimization is running, **When** the router is scoring
   and switching routes, **Then** a brief "Optimizing" state is shown with a subtle animated icon
   (no full loading state — routing continues uninterrupted).

5. **Given** the tunnel drops unexpectedly, **When** the kill-switch activates, **Then** the status
   area shows "Connection Lost — Kill-Switch Active" with a red/warning indicator and a reconnect prompt.

6. **Given** a connection error occurs, **When** the error state is shown, **Then** an actionable
   error message is displayed (not a raw exception) with a retry option visible.

---

### User Story 5 — Settings Panel: Relay Region Preference (Priority: P3)

A user wants to control which relay regions RouteXia uses. The Settings panel displays available relay
regions (e.g., Singapore, India, Dubai) with toggles or checkboxes, allows the user to set a preferred
primary region, and confirms that their preference is saved and applied to the next connection.

**Why this priority**: Region control is a power-user feature that differentiates from basic solutions.
It does not need to be perfect in v1 but must be present and functional.

**Independent Test**: Can be tested by opening Settings, changing relay region preferences, closing
and reopening the app, and verifying the preferences persisted and were applied.

**Acceptance Scenarios**:

1. **Given** the Settings panel is open, **When** the relay region section is shown, **Then** all
   configured relay regions (from profile config) are listed with individual enable/disable toggles.

2. **Given** the user enables a preferred region, **When** they save settings, **Then** on the next
   connection the selected region is prioritized as the first route candidate in scoring.

3. **Given** the user disables all regions, **When** they attempt to save, **Then** a validation
   message prevents saving and explains that at least one region must be active.

---

### User Story 6 — Account & Subscription Status (Priority: P3)

A logged-in user can see their subscription status, plan tier, and account identity in a clearly
designated area (sidebar footer or dedicated Account section) without navigating away from the main
Boost view.

**Why this priority**: Subscription visibility is a retention tool — users who know their plan is
active are less likely to churn. This is a low-complexity, high-value surface.

**Independent Test**: Can be tested by logging in with a subscribed account and verifying the account
area shows plan type, expiry/renewal indicator, and username.

**Acceptance Scenarios**:

1. **Given** a user is logged in with an active subscription, **When** the sidebar or Account area
   is visible, **Then** the plan tier (e.g., "Pro"), renewal date, and username/email are shown.

2. **Given** a user's subscription is expiring within 7 days, **When** the account area is shown,
   **Then** a warning indicator highlights the upcoming expiry with an option to renew.

3. **Given** a user is not logged in, **When** the Account area is shown, **Then** a login prompt
   is presented inline without a full-screen takeover.

---

### Edge Cases

- What happens when the route visualization graph has no data yet (e.g., first 500ms after connect)?
  → Show a loading/awaiting-data placeholder within the graph area, not an error.
- What happens when the detected game has no icon asset in the profile config?
  → Fall back to a generic "game controller" icon from the WPF UI icon set.
- What happens when the window is resized to minimum dimensions (1080×680)?
  → All primary UI elements (status, graph, connect button) must remain visible and usable; secondary
  panels may collapse or truncate.
- What happens when the design tokens (color/typography) fail to load from ResourceDictionary?
  → The app MUST NOT render with default WPF chrome; a fallback dark background (#0A0E14) should be
  hardcoded in App.xaml as a last-resort default.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The UI MUST implement a centralized design system defined entirely in ResourceDictionary
  files (color tokens, typography scale, spacing tokens, gradient brushes) — no ad-hoc inline color
  values in view XAML files (except values that reference a token).

- **FR-002**: The Boost/Connect view MUST display a real-time latency graph that updates at minimum
  every 500ms when a connection is active, showing at least 60 seconds of rolling history per active
  relay route.

- **FR-003**: The UI MUST present at least 5 visually distinct connection states: Disconnected,
  Connecting, Connected, Optimizing, and Error/Kill-Switch-Active — each with a unique combination
  of color, icon, and label.

- **FR-004**: The app MUST display a game detection indicator that shows the name of the currently
  detected game (sourced from the active profile) or an idle/no-game state when no matching game
  process is running.

- **FR-005**: The game detection indicator MUST source the game's display name from the active
  profile configuration — no game name or process name may be hardcoded in the view or viewmodel layer.

- **FR-006**: The Settings panel MUST display all relay regions from the active relay server configuration
  and allow the user to enable/disable individual regions and set a preferred primary region.

- **FR-007**: The sidebar or account section MUST display the logged-in user's subscription plan tier,
  renewal/expiry date, and identifier (username or email).

- **FR-008**: All interactive elements (nav items, buttons, cards) MUST respond to hover/focus states
  with a smooth visual transition completing within 150ms.

- **FR-009**: The app window MUST remain fully functional (all primary controls accessible) at the
  minimum window size of 1080×680 pixels.

- **FR-010**: The UI MUST use gaming-native typography: a display/heading font (Rajdhani or equivalent)
  for titles and stat values, and a readable sans-serif (Inter or equivalent) for body/label text.

- **FR-011**: The latency graph MUST visually distinguish each relay route by a unique color and MUST
  highlight the currently active/primary route differently from standby routes.

- **FR-012**: Every state transition in the connection flow (Disconnected → Connecting → Connected)
  MUST include a micro-animation (minimum: animated status icon; optional: transition fade between states).

### Key Entities

- **ConnectionState**: Represents one of {Disconnected, Connecting, Connected, Optimizing, Error,
  KillSwitchActive} — drives status area rendering.
- **RouteSnapshot**: A single timed measurement for one relay route — includes relay name, ping (ms),
  jitter (ms), score, and isActivePrimary flag.
- **GameDetectionResult**: Represents the outcome of a process scan — either {GameNotRunning} or
  {GameDetected, displayName: string, iconPath: string?}.
- **AccountInfo**: Holds the current user's display name, plan tier, and subscription expiry date.
- **RelayRegionPreference**: A per-region user preference record — region name, isEnabled, isPrimaryPreferred.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every connection state transition is visually distinguishable at a glance by 9 out of
  10 first-time users without reading any label text (validated by informal A/B review against a
  plain-state mockup).

- **SC-002**: The latency graph reflects new data within 600ms of a route measurement being produced
  (500ms poll interval + max 100ms UI render budget).

- **SC-003**: Game detection indicator updates to show the correct game name within 5 seconds of the
  game process starting.

- **SC-004**: All hover/focus transitions complete within 150ms — no interactive element changes state
  with a perceptible delay.

- **SC-005**: The UI renders correctly and all primary controls are accessible at minimum window size
  (1080×680) with no clipping or overlap of critical elements.

- **SC-006**: Zero ad-hoc color literal values (e.g., `#FF0000`, `Color.Red`) appear in view XAML
  files — all colors reference a named ResourceDictionary token.

- **SC-007**: The design system passes a visual consistency audit: all spacing values conform to an
  8px base grid (±1px tolerance for optical alignment), and all font sizes match the defined typography
  scale.

---

## Assumptions

- The existing WPF/WPF UI (Lepo.WPF.UI / FluentWindow) library is retained — the redesign works
  within the existing WPF stack and does not introduce a new UI framework.
- The existing ResourceDictionary structure (`ColorPalette.xaml`, `ButtonStyle.xaml`, etc.) in
  `RouteXia.App/Resources/Styles/` is the canonical location for design tokens; this spec assumes
  it will be extended, not replaced wholesale.
- The Rajdhani font already bundled in `Resources/Fonts/` is the designated display font and will
  be retained or upgraded to the full weight family.
- Game profile data (process names, display names, icon paths) is already read from a config layer —
  the UI spec assumes this data is available as a bindable property from the ViewModel layer.
- Real-time route data (ping, jitter, active route) is already produced by the `MultipathRouter` in
  `RouteXia.VpnClient` — the UI spec assumes ViewModel-level observables exposing this data exist
  or will be added as part of implementation.
- Subscription/account data is fetched from the backend API and exposed via an `AccountViewModel`
  or equivalent — implementation of the backend endpoint is out of scope for this UI spec.
- The minimum supported window size of 1080×680 is an existing constraint from `MainWindow.xaml`
  and will not change.
- Mobile, tablet, or web UI is out of scope for this feature.

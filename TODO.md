# CustomVPN Client — TODO / Improvement Roadmap

All items are client-side GUI/UX improvements only.
Server code is not to be touched.

---

## ?? Theme System

- [ ] **Dark / Light theme toggle** — button in the header bar that switches between dark (`#090D16` base) and light (`#F8FAFC` base) theme at runtime
- [ ] **Persist theme preference** — save chosen theme to `%AppData%\CustomVPN\prefs.json` and load on startup
- [ ] **System theme auto-detect** — on first launch, check Windows `AppsUseLightTheme` registry key and default to matching theme
- [ ] **Smooth theme transition** — animate color changes with `ColorAnimation` (250ms) instead of instant switch

---

## ?? Login Screen UX

- [ ] **Show/Hide password toggle** — eye icon inside the PasswordBox to reveal the typed password
- [ ] **Remember username** — checkbox to persist the last-used username in `%AppData%\CustomVPN\prefs.json`
- [ ] **Login status text updates** — currently button text changes; the status messages should appear as a clearly visible step-by-step progress indicator below the button instead
- [ ] **Better error styling** — red error text should also pulse/shake briefly on failed login to draw attention
- [ ] **Auto-focus** — set keyboard focus on the username field when the login panel appears

---

## ?? Dashboard — Status Panel (Left Column)

- [ ] **Latency / Ping display** — show live ping to `10.77.0.1` in ms next to the CONNECTED label, updating every 5 seconds
- [ ] **Connection uptime timer** — show how long the current session has been active (e.g. `Connected for 1h 23m`)
- [ ] **Bytes sent / received counters** — read from `wg show` output and display data usage for the current session
- [ ] **Last handshake time** — display when WireGuard last completed a cryptographic handshake (parsed from `wg show`)
- [ ] **Animated status dot** — when CONNECTED, the green dot should pulse with a slow glow animation instead of being static
- [ ] **Connecting / Reconnecting state** — show an animated spinner next to "CONNECTING..." when the tunnel is being established
- [ ] **Split tunnel vs Full tunnel label** — show which mode is currently active beneath the Route All toggle

---

## ?? Peers Panel (Right Column)

- [ ] **Peer ping indicator** — show latency in ms to each online peer's VPN IP
- [ ] **Last-seen timestamp for offline peers** — show "Offline · 2h ago" using the `lastSeenAt` field already returned by the API
- [ ] **Click peer to ping** — right-clicking a peer opens a small popup that pings their VPN IP and shows the result
- [ ] **Copy peer IP button** — a small clipboard icon on each peer card to copy their `10.77.0.x` IP with one click
- [ ] **Sort peers** — online peers first, then offline; optionally sortable by username alphabetically
- [ ] **Empty state UI** — when there are no peers, show a friendly placeholder instead of a blank list
- [ ] **Peer search / filter box** — a small input above the list to filter by username or IP

---

## ?? File Transfer UX

- [ ] **Inline toast notifications** — replace blocking `MessageBox` popup for received files with a non-blocking toast at bottom of the window
- [ ] **Transfer progress indicator** — `TransferProgress` event exists in `FileTransferService` but is never shown; wire it up to a progress bar
- [ ] **Send file by double-clicking a peer** — double-click an online peer card to directly open file picker
- [ ] **Received files history panel** — collapsible section at bottom showing recently received files with "Open File" / "Open Folder" quick links
- [ ] **Drag-and-drop to peer card** — drag a file from Explorer and drop it onto an online peer card to send

---

## ?? System Tray & Notifications

- [ ] **Tray icon state change** — icon should change visually when connected vs disconnected (green vs grey shield)
- [ ] **Balloon on connect/disconnect** — brief tray notification when the VPN connects or disconnects
- [ ] **Balloon on file received** — replace blocking MessageBox with a non-blocking tray balloon with "Open File" action
- [ ] **Connection lost notification** — if WireGuard service stops unexpectedly, show "VPN disconnected — click to reconnect"

---

## ?? Settings / Preferences Panel

- [ ] **Settings dialog** accessible from the header or tray icon, containing:
  - Theme toggle (dark / light)
  - Launch on startup toggle (Windows registry)
  - Start minimized to tray toggle
  - File download destination folder picker
  - Reconnect-on-resume toggle
- [ ] **Auto-reconnect on sleep/wake** — register for `SystemEvents.PowerModeChanged` and re-establish tunnel on resume
- [ ] **Startup with Windows** — register/deregister from `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` within the app

---

## ??? Window & App Polish

- [ ] **Minimize to tray on close** — option to minimize-to-tray instead of disconnect-and-exit (with toggle in settings)
- [ ] **Animated panel transitions** — fade or slide animation when switching login ? dashboard instead of instant toggle
- [ ] **Keyboard shortcuts** — `Ctrl+L` to sign out, `Ctrl+T` to toggle full tunnel, `Escape` to minimize
- [ ] **Resizable window** — currently locked; allow free resize with minimum size constraint, peer list fills available space
- [ ] **Window size / position memory** — save and restore window position and size between sessions
- [ ] **App version display** — show client version number in the header (e.g. `v1.0.0`)

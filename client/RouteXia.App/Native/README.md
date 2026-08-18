# WinDivert Binaries

Place the following files in this directory before building:

- `WinDivert.dll`   — User-mode DLL (P/Invoke target)
- `WinDivert64.sys` — Kernel filter driver (auto-loaded by WinDivert.dll with admin privileges)

## Download

Download from the official WinDivert releases:
https://github.com/basil00/WinDivert/releases

Get the latest `WinDivert-X.X.X-A.zip` (use the "A" variant which is WHQL-signed).

Extract and copy:
```
WinDivert-X.X.X-A\x64\WinDivert.dll    → client\windivert\WinDivert.dll
WinDivert-X.X.X-A\x64\WinDivert64.sys  → client\windivert\WinDivert64.sys
```

## Notes

- WinDivert.sys is a **signed kernel driver** — Windows loads it automatically when your app calls `WinDivertOpen()` with admin privileges.
- No manual driver installation (sc.exe, etc.) is needed.
- The driver is unloaded when all WinDivert handles are closed.
- Only x64 is supported (matches the RouteXia.App build target: `win-x64`).

## License

WinDivert is distributed under the LGPL license.
See: https://www.reqrypt.org/windivert.html

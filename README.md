# RouteXia — Windows Desktop Gaming Network Optimizer

<div align="center">
  <img src="RX_LOGO_TRANSPRANT.png" alt="RouteXia Logo" width="220" />
  <br />
  <h3>Next-Generation Low Latency Multipath Network Optimizer for PC Gaming</h3>

  [![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
  [![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-blue)](https://routexia.com)
  [![Framework](https://img.shields.io/badge/.NET-9.0%20WPF-purple)](https://dotnet.microsoft.com/)
  [![UI](https://img.shields.io/badge/UI-WPF%20Fluent%20Design-cyan)](https://github.com/lepoco/wpfui)
</div>

---

## 🚀 Overview

**RouteXia** is an advanced Windows desktop gaming network accelerator engineered for competitive PC gaming titles (including *PUBG: BATTLEGROUNDS*, *Counter-Strike 2*, *Valorant*, *Apex Legends*, *Call of Duty: Warzone*, and *Fortnite*).

It provides real-time latency monitoring, multi-route game packet diversion, network shield protection, and dynamic kill-switch failover to deliver the lowest possible ping and eliminate jitter.

---

## ✨ Features

- **🎨 Modern Fluent Design**: Built with modern dark-mode aesthetics, custom Mica/Acrylic glassmorphism, responsive navigation, and animated live HUD telemetry.
- **⚡ Kernel-Level Packet Interception**: Uses high-performance network filtering to intercept game UDP sessions with near-zero overhead.
- **🛡️ Network Shield & Kill Switch**: Prevents ISP fallback latency spikes and packet leakage if the connection drops.
- **📊 Real-Time Latency & Jitter Telemetry**: Live ping graphs powered by SkiaSharp to track round-trip time (RTT), jitter variance, and packet loss in real-time.
- **🎯 Dynamic Game Library**: Out-of-the-box support for popular competitive titles with custom CIDR and server tracking.
- **🔄 Discord-Style Seamless Auto-Updates**: Integrated with Velopack OTA engine to deliver silent delta background updates.
- **📦 Production-Grade Setup Wizard**: Standalone, self-contained Inno Setup installer requiring zero external .NET runtimes.

---

## 🏗️ Client Architecture

```
ROUTEXIA/
├── client/
│   ├── RouteXia.App/            # WPF UI (Views, ViewModels, Resources, Icons)
│   ├── RouteXia.VpnClient/      # Routing engine, packet interceptor, API client
│   ├── RouteXia.WfpFilter/      # Windows Filtering Platform user-mode bindings
│   └── RouteXia.sln             # Visual Studio .NET 9 Solution
│
├── installer/
│   └── RouteXia.iss             # Production Inno Setup 6 packaging script
│
├── scripts/
│   ├── build-release.ps1        # Automated self-contained build & packaging
│   ├── release-r2.ps1           # OTA update packaging pipeline
│   └── sign-binaries.ps1        # Automated Authenticode code-signing script
│
├── LICENSE                      # MIT Open-Source License
└── README.md                    # Project Documentation
```

---

## 🛠️ Building from Source

### Prerequisites
- **Windows 10 / 11 (64-bit)**
- **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)**
- **Visual Studio 2022** (v17.12+) or **Visual Studio Code** with C# Dev Kit
- **[Inno Setup 6](https://jrsoftware.org/isdl.php)** (Optional, for compiling the `.exe` installer)

### Build Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/MrShuvoBiswas/ROUTEXIA.git
   cd ROUTEXIA
   ```

2. **Restore dependencies & build:**
   ```bash
   dotnet restore client/RouteXia.sln
   dotnet build client/RouteXia.sln -c Release
   ```

3. **Compile the standalone Production Installer:**
   ```powershell
   .\scripts\build-release.ps1 -Version "1.0.7"
   ```
   The compiled setup installer will be generated in `artifacts/installer/SetupRouteXia-v1.0.7.exe`.

---

## 🔒 Security & Privacy

RouteXia requires administrative privileges during execution to interact with the Windows Filtering Platform (WFP) and configure local routing rules. The client is completely open source and does not collect or log personal user telemetry.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

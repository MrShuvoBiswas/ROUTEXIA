# RouteXia - Custom Game Accelerator for PUBG PC Optimization

> **Multipath UDP Overlay • Process-Level WFP Filter • BattlEye-Safe • Zero Kernel Injection**

RouteXia is a custom multipath game accelerator for PUBG PC gaming optimization (ExitLag / NoPing architecture). It intercepts PUBG traffic at the process level and routes packets simultaneously over multiple parallel low-latency paths.

## 🎯 Key Features

- **Process-Level Interception (WFP)**: Filters traffic strictly by game process name (`TslGame.exe`), leaving general internet traffic completely untouched.
- **Multipath Routing**: Sends each game packet across 2–3 parallel relay routes simultaneously. The fastest copy reaches the server, and duplicates are automatically dropped.
- **Real-Time Route Scoring**: Continuous ping and jitter measurement every 500ms dynamically picks the optimal routes.
- **Kill-Switch**: Automatically blocks PUBG traffic via Windows Firewall if the tunnel drops, preventing IP leaks and mid-game latency spikes.
- **BattlEye Safe**: Operates via standard Windows Filtering Platform (WFP) APIs without touching game process memory.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  PUBG PC (TslGame.exe — Steam / Krafton Launcher)          │
│  UDP Game Traffic                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  ★ RouteXia WFP Filter Engine (C# / WinAPI)                 │
│     • User-mode ALE layer packet filter                     │
│     • Process-level detection (TslGame.exe)                 │
│     • Zero game process injection (BattlEye Safe!)          │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  ★ RouteXia Multipath Router (C#)                           │
│     • Parallel UDP delivery (2-3 routes)                    │
│     • 500ms real-time ping/jitter scoring                   │
│     • Integrated Kill-Switch manager                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                [PARALLEL UDP ROUTES]
                         │
┌────────────────────────▼────────────────────────────────────┐
│  ★ RouteXia Relay Servers (Go)                              │
│     • Singapore, India, Dubai                               │
│     • Sequence-based deduplication                          │
│     • Low-latency forwarding to PUBG servers                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
               [PUBG Game Servers]
```

## 📁 Project Structure

```
ROUTEXIA/
├── client/                          # Windows client components
│   ├── RouteXia.App/                # WPF Desktop UI (.NET 9.0)
│   │   ├── Views/                   # MainWindow, ConnectView, SettingsView
│   │   ├── ViewModels/              # ConnectViewModel, SettingsViewModel
│   │   └── Resources/               # Dark theme styles & fonts
│   │
│   ├── RouteXia.VpnClient/          # Core Client Components
│   │   ├── Routing/                 # MultipathRouter (parallel sending & scoring)
│   │   └── KillSwitch/              # KillSwitchManager (firewall leak protection)
│   │
│   └── RouteXia.WfpFilter/          # WFP Packet Interception (User-Mode)
│       ├── WfpFilterEngine.cs       # Process poller & ALE connection filter
│       └── Native/WfpNative.cs      # Windows Filtering Platform P/Invoke
│
├── server/                          # Backend relay server (Go)
│   ├── main.go                      # UDP listener + deduplication engine
│   └── go.mod                       # Go module setup
│
├── scripts/                         # Deployment scripts
│   └── install-relay.sh             # VPS setup script (Ubuntu 22.04)
│
├── docs/                            # Documentation
│   ├── ARCHITECTURE.md              # System design specification
│   ├── BACKEND_SERVER_GUIDE.md      # Server deployment guide
│   └── deep-research-report.md      # ExitLag / NoPing research report
│
├── ARCHITECTURE.md                  # Complete system design
└── README.md                        # Project overview
```

## 🚀 Building and Running

### Prerequisites
- Windows 10/11 x64
- .NET 9.0 SDK
- Visual Studio 2022
- Go 1.21+ (for backend server)
- Administrator Privileges (required for WFP & Firewall operations)

### Building the Client

```powershell
cd client
dotnet build RouteXia.sln --configuration Release
```

### Running the Client

Right-click Visual Studio or Windows Terminal and select **Run as Administrator**, then execute:

```powershell
cd client\RouteXia.App\bin\x64\Release\net9.0-windows\win-x64
.\RouteXia.exe
```

### Deploying the Relay Server

On an Ubuntu 22.04 VPS (Singapore / India / Dubai):

```bash
git clone <your-repo-url>
cd ROUTEXIA/scripts
chmod +x install-relay.sh
sudo ./install-relay.sh SG
```

---

**Built with ❤️ for ultra-low latency PUBG PC gaming.**

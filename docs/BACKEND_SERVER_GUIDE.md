# RouteXia Backend Server - Development Guide

## Overview
The backend server infrastructure handles incoming VPN connections, routes traffic to PUBG servers, and manages load balancing across multiple geographic locations.

## Server Architecture

```
                    ┌─────────────────────────────┐
                    │  Load Balancer (Optional)   │
                    │  - HAProxy or Nginx         │
                    └──────────────┬──────────────┘
                                   │
                  ┌────────────────┼────────────────┐
                  │                │                │
        ┌─────────▼──────┐  ┌─────▼─────┐  ┌──────▼──────┐
        │  Singapore VPS │  │ India VPS │  │  Dubai VPS  │
        │  RouteXia Node │  │RouteXia Node│ │RouteXia Node│
        └─────────┬──────┘  └─────┬─────┘  └──────┬──────┘
                  │                │                │
                  └────────────────┼────────────────┘
                                   │
                         ┌─────────▼──────────┐
                         │  PUBG Game Servers │
                         │  (Tencent Cloud)   │
                         └────────────────────┘
```

## Server Components

### 1. Connection Handler
- Accept UDP connections from clients
- Perform handshake
- Manage session state

### 2. Packet Router
- Decrypt incoming packets
- Forward to PUBG servers
- Route responses back to clients

### 3. Route Optimizer
- Measure latency to PUBG servers
- Select optimal routes
- Cache routing decisions

### 4. Load Balancer
- Distribute load across nodes
- Health checking
- Automatic failover

### 5. Monitoring System
- Track active connections
- Log traffic statistics
- Alert on anomalies

## Technology Choice: Go vs Rust

### Recommended: **Go**

**Advantages:**
- Fast development
- Excellent network performance
- Built-in concurrency (goroutines)
- Easy deployment (single binary)
- Large ecosystem for network tools
- Better for rapid iteration

**Code Example (Go):**
```go
func main() {
    conn, _ := net.ListenUDP("udp", &net.UDPAddr{Port: 5000})
    go handleConnections(conn)
}
```

### Alternative: **Rust**

**Advantages:**
- Maximum performance
- Memory safety without GC
- Better for high-load scenarios
- Lower resource usage

**Use Rust if:**
- You need absolute maximum performance
- You expect very high concurrent connections (100k+)
- You have Rust expertise on the team

## Go Implementation

### Project Structure

```
routexia-server/
├── cmd/
│   └── server/
│       └── main.go              # Entry point
├── internal/
│   ├── server/
│   │   ├── server.go            # Main server
│   │   ├── handler.go           # Connection handler
│   │   └── session.go           # Session management
│   ├── protocol/
│   │   ├── packet.go            # Packet structure
│   │   ├── crypto.go            # Encryption
│   │   └── handshake.go         # Handshake logic
│   ├── router/
│   │   ├── router.go            # Packet routing
│   │   ├── pubg.go              # PUBG server discovery
│   │   └── optimizer.go         # Route optimization
│   └── monitoring/
│       ├── stats.go             # Statistics
│       └── metrics.go           # Prometheus metrics
├── configs/
│   └── config.yaml              # Configuration
├── scripts/
│   ├── install.sh               # Server installation
│   └── setup-firewall.sh        # Firewall rules
├── go.mod
└── README.md
```

### Main Server Implementation

```go
package server

import (
    "crypto/rand"
    "encoding/binary"
    "fmt"
    "net"
    "sync"
    "time"
)

type RouteXiaServer struct {
    config      *Config
    conn        *net.UDPConn
    sessions    sync.Map // clientAddr -> *Session
    pubgServers []*PubgServer
    stats       *Statistics
}

type Config struct {
    ListenPort    int
    MaxClients    int
    SessionTTL    time.Duration
    PubgDiscovery bool
}

type Session struct {
    ID          uint32
    ClientAddr  *net.UDPAddr
    SharedKey   []byte
    SendCounter uint64
    RecvCounter uint64
    LastSeen    time.Time
    Stats       SessionStats
}

type SessionStats struct {
    PacketsSent     uint64
    PacketsReceived uint64
    BytesSent       uint64
    BytesReceived   uint64
}

func NewServer(config *Config) (*RouteXiaServer, error) {
    addr := &net.UDPAddr{
        Port: config.ListenPort,
    }
    
    conn, err := net.ListenUDP("udp", addr)
    if err != nil {
        return nil, fmt.Errorf("failed to listen: %w", err)
    }
    
    // Set buffer sizes for high throughput
    conn.SetReadBuffer(4 * 1024 * 1024)  // 4MB
    conn.SetWriteBuffer(4 * 1024 * 1024) // 4MB
    
    server := &RouteXiaServer{
        config: config,
        conn:   conn,
        stats:  NewStatistics(),
    }
    
    // Discover PUBG servers
    if config.PubgDiscovery {
        server.pubgServers = DiscoverPubgServers()
    }
    
    return server, nil
}

func (s *RouteXiaServer) Start() error {
    fmt.Printf("RouteXia server started on port %d\n", s.config.ListenPort)
    
    // Start background tasks
    go s.cleanupSessions()
    go s.monitorPerformance()
    
    // Main packet loop
    buffer := make([]byte, 2048)
    
    for {
        n, clientAddr, err := s.conn.ReadFromUDP(buffer)
        if err != nil {
            fmt.Printf("Read error: %v\n", err)
            continue
        }
        
        // Handle packet in goroutine (non-blocking)
        packet := make([]byte, n)
        copy(packet, buffer[:n])
        
        go s.handlePacket(packet, clientAddr)
    }
}

func (s *RouteXiaServer) handlePacket(data []byte, clientAddr *net.UDPAddr) {
    // Parse packet
    packet, err := ParsePacket(data)
    if err != nil {
        fmt.Printf("Invalid packet from %s: %v\n", clientAddr, err)
        return
    }
    
    switch packet.Type {
    case PacketTypeHello:
        s.handleHello(packet, clientAddr)
    case PacketTypeData:
        s.handleData(packet, clientAddr)
    case PacketTypePing:
        s.handlePing(packet, clientAddr)
    case PacketTypeGoodbye:
        s.handleGoodbye(packet, clientAddr)
    }
}

func (s *RouteXiaServer) handleHello(packet *Packet, clientAddr *net.UDPAddr) {
    // Parse Hello payload
    hello := &HelloPayload{}
    if err := hello.Deserialize(packet.Payload); err != nil {
        return
    }
    
    // Generate server key pair
    serverPrivKey, serverPubKey, _ := GenerateKeyPair()
    
    // Derive shared secret
    sharedSecret := DeriveSharedSecret(serverPrivKey, hello.ClientPublicKey)
    
    // Generate session key
    salt := make([]byte, 32)
    rand.Read(salt)
    
    sessionKey := HKDF(sharedSecret, salt, []byte("RouteXia-Session-Key"), 32)
    
    // Create session
    sessionID := s.generateSessionID()
    session := &Session{
        ID:         sessionID,
        ClientAddr: clientAddr,
        SharedKey:  sessionKey,
        LastSeen:   time.Now(),
    }
    
    s.sessions.Store(clientAddr.String(), session)
    
    // Send HelloAck
    helloAck := &HelloAckPayload{
        ServerPublicKey: serverPubKey,
        Salt:            salt,
        SessionID:       sessionID,
    }
    
    response := &Packet{
        Magic:     0x5258,
        Version:   1,
        Type:      PacketTypeHelloAck,
        SessionID: sessionID,
        Timestamp: time.Now().UnixMilli(),
        Payload:   helloAck.Serialize(),
    }
    
    s.sendPacket(response, clientAddr)
    
    fmt.Printf("New session: %d from %s\n", sessionID, clientAddr)
}

func (s *RouteXiaServer) handleData(packet *Packet, clientAddr *net.UDPAddr) {
    // Get session
    sessionVal, ok := s.sessions.Load(clientAddr.String())
    if !ok {
        fmt.Printf("No session for %s\n", clientAddr)
        return
    }
    session := sessionVal.(*Session)
    
    // Update last seen
    session.LastSeen = time.Now()
    
    // Decrypt packet
    crypto := NewCryptoEngine(session.SharedKey)
    decrypted, err := crypto.Decrypt(packet.Payload)
    if err != nil {
        fmt.Printf("Decryption failed: %v\n", err)
        return
    }
    
    // Extract original IP packet
    ipPacket := decrypted
    
    // Parse destination (PUBG server IP)
    destIP := net.IP(ipPacket[16:20]) // IPv4 destination offset
    
    // Find best route to PUBG server
    route := s.findBestRoute(destIP)
    
    // Forward packet
    if route != nil {
        s.forwardToPubg(ipPacket, route)
        session.Stats.PacketsSent++
        session.Stats.BytesSent += uint64(len(ipPacket))
    }
}

func (s *RouteXiaServer) forwardToPubg(ipPacket []byte, route *Route) error {
    // Create raw socket to PUBG server
    conn, err := net.Dial("udp", route.Address)
    if err != nil {
        return err
    }
    defer conn.Close()
    
    // Extract UDP payload from IP packet
    udpPayload := extractUdpPayload(ipPacket)
    
    // Send to PUBG server
    _, err = conn.Write(udpPayload)
    return err
}

func (s *RouteXiaServer) handlePing(packet *Packet, clientAddr *net.UDPAddr) {
    // Send Pong response
    pong := &Packet{
        Magic:     0x5258,
        Version:   1,
        Type:      PacketTypePong,
        SessionID: packet.SessionID,
        Timestamp: time.Now().UnixMilli(),
    }
    
    s.sendPacket(pong, clientAddr)
}

func (s *RouteXiaServer) sendPacket(packet *Packet, addr *net.UDPAddr) error {
    data := packet.Serialize()
    _, err := s.conn.WriteToUDP(data, addr)
    return err
}

func (s *RouteXiaServer) generateSessionID() uint32 {
    var id uint32
    binary.Read(rand.Reader, binary.BigEndian, &id)
    return id
}

func (s *RouteXiaServer) cleanupSessions() {
    ticker := time.NewTicker(30 * time.Second)
    defer ticker.Stop()
    
    for range ticker.C {
        now := time.Now()
        s.sessions.Range(func(key, value interface{}) bool {
            session := value.(*Session)
            if now.Sub(session.LastSeen) > s.config.SessionTTL {
                s.sessions.Delete(key)
                fmt.Printf("Session %d expired\n", session.ID)
            }
            return true
        })
    }
}
```

### Route Optimization

```go
package router

import (
    "net"
    "sync"
    "time"
)

type Route struct {
    Address   string
    Latency   time.Duration
    PacketLoss float64
    LastCheck time.Time
}

type RouteOptimizer struct {
    routes     map[string]*Route
    routesMux  sync.RWMutex
    pubgRanges []*net.IPNet
}

func NewRouteOptimizer() *RouteOptimizer {
    return &RouteOptimizer{
        routes:     make(map[string]*Route),
        pubgRanges: LoadPubgIPRanges(),
    }
}

func (ro *RouteOptimizer) FindBestRoute(destIP net.IP) *Route {
    // Check if destination is PUBG server
    if !ro.isPubgServer(destIP) {
        return nil
    }
    
    ro.routesMux.RLock()
    defer ro.routesMux.RUnlock()
    
    // Find cached route
    route, exists := ro.routes[destIP.String()]
    
    if !exists || time.Since(route.LastCheck) > 5*time.Minute {
        // Discover new route
        route = ro.discoverRoute(destIP)
        ro.routes[destIP.String()] = route
    }
    
    return route
}

func (ro *RouteOptimizer) isPubgServer(ip net.IP) bool {
    for _, ipRange := range ro.pubgRanges {
        if ipRange.Contains(ip) {
            return true
        }
    }
    return false
}

func (ro *RouteOptimizer) discoverRoute(destIP net.IP) *Route {
    // Measure latency
    start := time.Now()
    
    conn, err := net.DialTimeout("udp", destIP.String()+":7350", 2*time.Second)
    if err != nil {
        return &Route{
            Address:   destIP.String() + ":7350",
            Latency:   999 * time.Millisecond,
            LastCheck: time.Now(),
        }
    }
    defer conn.Close()
    
    latency := time.Since(start)
    
    return &Route{
        Address:    destIP.String() + ":7350",
        Latency:    latency,
        PacketLoss: 0,
        LastCheck:  time.Now(),
    }
}

func LoadPubgIPRanges() []*net.IPNet {
    // Load from JSON or hardcoded
    ranges := []string{
        "129.226.0.0/16",     // Tencent Cloud
        "150.109.0.0/16",     // Tencent
        "203.205.128.0/17",   // Asia region
        // Add more ranges
    }
    
    var ipnets []*net.IPNet
    for _, cidr := range ranges {
        _, ipnet, _ := net.ParseCIDR(cidr)
        if ipnet != nil {
            ipnets = append(ipnets, ipnet)
        }
    }
    
    return ipnets
}
```

### Monitoring & Statistics

```go
package monitoring

import (
    "fmt"
    "time"
)

type Statistics struct {
    StartTime       time.Time
    TotalSessions   uint64
    ActiveSessions  uint64
    PacketsForwarded uint64
    BytesForwarded  uint64
}

func (s *Statistics) PrintStats() {
    uptime := time.Since(s.StartTime)
    
    fmt.Printf("\n=== RouteXia Server Statistics ===\n")
    fmt.Printf("Uptime: %s\n", uptime)
    fmt.Printf("Total Sessions: %d\n", s.TotalSessions)
    fmt.Printf("Active Sessions: %d\n", s.ActiveSessions)
    fmt.Printf("Packets Forwarded: %d\n", s.PacketsForwarded)
    fmt.Printf("Bytes Forwarded: %.2f MB\n", float64(s.BytesForwarded)/(1024*1024))
    fmt.Printf("===================================\n\n")
}
```

## Deployment

### VPS Requirements

**Minimum Specs (per node):**
- CPU: 2 cores
- RAM: 2 GB
- Bandwidth: 100 Mbps
- OS: Ubuntu 22.04 LTS

**Recommended Locations:**
1. Singapore (closest to Southeast Asia PUBG servers)
2. India (Mumbai/Bangalore)
3. Dubai/Bahrain (Middle East)

### Installation Script

```bash
#!/bin/bash
# install.sh

set -e

echo "Installing RouteXia Server..."

# Update system
apt-get update
apt-get upgrade -y

# Install dependencies
apt-get install -y wget curl ufw

# Download RouteXia server binary
wget https://github.com/routexia/server/releases/latest/download/routexia-server-linux-amd64
mv routexia-server-linux-amd64 /usr/local/bin/routexia-server
chmod +x /usr/local/bin/routexia-server

# Create config directory
mkdir -p /etc/routexia

# Create config file
cat > /etc/routexia/config.yaml << EOF
listen_port: 5000
max_clients: 1000
session_ttl: 300s
pubg_discovery: true
log_level: info
EOF

# Create systemd service
cat > /etc/systemd/system/routexia.service << EOF
[Unit]
Description=RouteXia VPN Server
After=network.target

[Service]
Type=simple
User=root
ExecStart=/usr/local/bin/routexia-server -config /etc/routexia/config.yaml
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# Configure firewall
ufw allow 5000/udp
ufw --force enable

# Enable and start service
systemctl daemon-reload
systemctl enable routexia
systemctl start routexia

echo "RouteXia Server installed successfully!"
echo "Status: systemctl status routexia"
echo "Logs: journalctl -u routexia -f"
```

### Firewall Configuration

```bash
#!/bin/bash
# setup-firewall.sh

# Allow SSH
ufw allow 22/tcp

# Allow RouteXia VPN
ufw allow 5000/udp

# Allow outbound to PUBG servers
# (Usually no explicit rule needed for outbound)

# Enable firewall
ufw --force enable

echo "Firewall configured!"
```

## Monitoring & Maintenance

### Health Check Endpoint

```go
func (s *RouteXiaServer) StartHealthCheckServer() {
    http.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
        w.WriteHeader(http.StatusOK)
        fmt.Fprintf(w, "OK")
    })
    
    http.HandleFunc("/stats", func(w http.ResponseWriter, r *http.Request) {
        stats := s.GetStats()
        json.NewEncoder(w).Encode(stats)
    })
    
    http.ListenAndServe(":8080", nil)
}
```

### Prometheus Metrics (Optional)

```go
import "github.com/prometheus/client_golang/prometheus"

var (
    sessionsActive = prometheus.NewGauge(prometheus.GaugeOpts{
        Name: "routexia_sessions_active",
        Help: "Number of active VPN sessions",
    })
    
    packetsForwarded = prometheus.NewCounter(prometheus.CounterOpts{
        Name: "routexia_packets_forwarded_total",
        Help: "Total packets forwarded to PUBG servers",
    })
)
```

## Next Steps

1. Set up development environment
2. Implement basic Go server
3. Add protocol handling
4. Implement encryption
5. Test with mock clients
6. Deploy to VPS
7. Configure monitoring
8. Load testing

## Cost Estimation

**Per month (for 3 VPS nodes):**
- Singapore VPS: $10/month
- India VPS: $10/month  
- Dubai VPS: $15/month
- **Total: ~$35/month**

**Bandwidth costs:**
- Most VPS include 1-2 TB free
- Additional: ~$0.01/GB

**For 1000 users gaming 2 hours/day:**
- Traffic: ~500 GB/month
- Cost: $35 base + minimal bandwidth
- **Total: ~$40-50/month**

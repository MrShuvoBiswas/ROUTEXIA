// RouteXia Relay Server v3 — High Performance Game UDP Relay
//
// Maintains PERSISTENT UDP sessions per (client, game_server) pair.
// This preserves PUBG's connection state (same source port on the relay side),
// streams game responses back to the client continuously in background goroutines,
// and eliminates socket-churn latency.

package main

import (
	"encoding/binary"
	"flag"
	"fmt"
	"log"
	"net"
	"sync"
	"sync/atomic"
	"time"
)

var (
	listenPort = flag.Int("port", 9001, "UDP port to listen on")
	region     = flag.String("region", "SG", "Region label (SG/IN/DXB)")
	debugMode  = flag.Bool("debug", false, "Verbose packet logging")
)

const (
	Magic0 = 0x52 // R
	Magic1 = 0x58 // X
	Magic2 = 0x49 // I
	Magic3 = 0x41 // A

	TypePing     = 0x01
	TypeData     = 0x02
	TypeResponse = 0x03

	HeaderSizeV2 = 18
	MaxPacket    = 65535
	SessionIdleTimeout = 60 * time.Second
)

// ── GameSession ──────────────────────────────────────────────────────────────
// A persistent UDP socket between this relay and a specific PUBG game server.
type GameSession struct {
	mu              sync.RWMutex
	gameConn        *net.UDPConn
	clientAddr      *net.UDPAddr
	clientLocalPort uint16
	destIP          net.IP
	destPort        uint16
	lastActive      atomic.Int64 // unix nano
	closed          atomic.Bool
}

func (s *GameSession) touch() {
	s.lastActive.Store(time.Now().UnixNano())
}

func (s *GameSession) updateClientAddr(addr *net.UDPAddr) {
	s.mu.Lock()
	s.clientAddr = addr
	s.mu.Unlock()
}

func (s *GameSession) getClientAddr() *net.UDPAddr {
	s.mu.RLock()
	defer s.mu.RUnlock()
	return s.clientAddr
}

// ── SessionManager ───────────────────────────────────────────────────────────
// Maps (clientIP + clientLocalPort + destIP + destPort) -> GameSession
type SessionManager struct {
	mu       sync.RWMutex
	sessions map[string]*GameSession
	server   *RelayServer
}

func NewSessionManager(server *RelayServer) *SessionManager {
	sm := &SessionManager{
		sessions: make(map[string]*GameSession),
		server:   server,
	}
	go sm.cleanupLoop()
	return sm
}

func (sm *SessionManager) sessionKey(clientAddr *net.UDPAddr, clientLocalPort uint16, destIP net.IP, destPort uint16) string {
	return fmt.Sprintf("%s:%d->%s:%d", clientAddr.IP, clientLocalPort, destIP, destPort)
}

func (sm *SessionManager) GetOrCreate(
	clientAddr *net.UDPAddr,
	clientLocalPort uint16,
	destIP net.IP,
	destPort uint16,
) (*GameSession, error) {
	key := sm.sessionKey(clientAddr, clientLocalPort, destIP, destPort)

	sm.mu.RLock()
	sess, exists := sm.sessions[key]
	sm.mu.RUnlock()

	if exists && !sess.closed.Load() {
		sess.touch()
		// Always update clientAddr in case client reconnected from a new ephemeral port/route
		sess.updateClientAddr(clientAddr)
		return sess, nil
	}

	sm.mu.Lock()
	defer sm.mu.Unlock()

	// Double check after write lock
	if sess, exists = sm.sessions[key]; exists && !sess.closed.Load() {
		sess.touch()
		sess.updateClientAddr(clientAddr)
		return sess, nil
	}

	// Dial persistent UDP socket to game server
	gameServerAddr := &net.UDPAddr{IP: destIP, Port: int(destPort)}
	gameConn, err := net.DialUDP("udp4", nil, gameServerAddr)
	if err != nil {
		return nil, fmt.Errorf("dial game server %s:%d failed: %w", destIP, destPort, err)
	}

	sess = &GameSession{
		gameConn:        gameConn,
		clientAddr:      clientAddr,
		clientLocalPort: clientLocalPort,
		destIP:          destIP,
		destPort:        destPort,
	}
	sess.touch()

	sm.sessions[key] = sess
	log.Printf("[Relay] New persistent session established: %s (local game socket: %s, client: %s)", key, gameConn.LocalAddr(), clientAddr)

	// Start background reader that streams server responses back to the client
	go sm.readFromGameServer(sess, key)

	return sess, nil
}

func (sm *SessionManager) readFromGameServer(sess *GameSession, key string) {
	buf := make([]byte, MaxPacket)

	for !sess.closed.Load() {
		_ = sess.gameConn.SetReadDeadline(time.Now().Add(SessionIdleTimeout))
		n, err := sess.gameConn.Read(buf)
		if err != nil {
			if sess.closed.Load() {
				break
			}
			// Timeout or socket error
			break
		}

		sess.touch()
		gameResponse := buf[:n]
		sm.server.stats.RecordReturned()

		// Build RXIA v2 response frame
		frame := make([]byte, HeaderSizeV2+len(gameResponse))
		frame[0] = Magic0
		frame[1] = Magic1
		frame[2] = Magic2
		frame[3] = Magic3
		frame[4] = TypeResponse

		// Echo sequence (0)
		frame[5] = 0; frame[6] = 0; frame[7] = 0

		// Source IP (game server's IP)
		srcIP := sess.destIP.To4()
		frame[8]  = srcIP[0]
		frame[9]  = srcIP[1]
		frame[10] = srcIP[2]
		frame[11] = srcIP[3]

		// Source port (game server port)
		binary.BigEndian.PutUint16(frame[12:14], sess.destPort)

		// Client's local PUBG port
		binary.BigEndian.PutUint16(frame[14:16], sess.clientLocalPort)

		// Payload length
		binary.BigEndian.PutUint16(frame[16:18], uint16(len(gameResponse)))

		// Payload
		copy(frame[HeaderSizeV2:], gameResponse)

		// Send back to client UDP socket
		targetClientAddr := sess.getClientAddr()
		if targetClientAddr != nil {
			_, err = sm.server.conn.WriteToUDP(frame, targetClientAddr)
			if err != nil && *debugMode {
				log.Printf("[Relay] Write response to %s failed: %v", targetClientAddr, err)
			}
		}
	}

	sm.closeSession(key, sess)
}

func (sm *SessionManager) closeSession(key string, sess *GameSession) {
	if sess.closed.CompareAndSwap(false, true) {
		_ = sess.gameConn.Close()
		sm.mu.Lock()
		delete(sm.sessions, key)
		sm.mu.Unlock()
		log.Printf("[Relay] Session closed: %s", key)
	}
}

func (sm *SessionManager) cleanupLoop() {
	ticker := time.NewTicker(10 * time.Second)
	for range ticker.C {
		cutoff := time.Now().Add(-SessionIdleTimeout).UnixNano()
		sm.mu.Lock()
		for key, sess := range sm.sessions {
			if sess.lastActive.Load() < cutoff {
				if sess.closed.CompareAndSwap(false, true) {
					_ = sess.gameConn.Close()
					delete(sm.sessions, key)
					log.Printf("[Relay] Cleaned up idle session: %s", key)
				}
			}
		}
		sm.mu.Unlock()
	}
}

// ── RelayServer ──────────────────────────────────────────────────────────────
type RelayServer struct {
	conn     *net.UDPConn
	sessions *SessionManager
	stats    *ServerStats
}

type ServerStats struct {
	mu               sync.Mutex
	PacketsReceived  int64
	PacketsForwarded int64
	PacketsReturned  int64
	ActiveClients    map[string]time.Time
}

func (s *ServerStats) RecordReceived(client string) {
	s.mu.Lock()
	s.PacketsReceived++
	s.ActiveClients[client] = time.Now()
	s.mu.Unlock()
}

func (s *ServerStats) RecordForwarded() {
	s.mu.Lock()
	s.PacketsForwarded++
	s.mu.Unlock()
}

func (s *ServerStats) RecordReturned() {
	s.mu.Lock()
	s.PacketsReturned++
	s.mu.Unlock()
}

func NewRelayServer(port int) (*RelayServer, error) {
	addr := &net.UDPAddr{Port: port, IP: net.ParseIP("0.0.0.0")}
	conn, err := net.ListenUDP("udp4", addr)
	if err != nil {
		log.Fatalf("Listen error: %v", err)
	}

	server := &RelayServer{
		conn:  conn,
		stats: &ServerStats{ActiveClients: make(map[string]time.Time)},
	}
	server.sessions = NewSessionManager(server)

	return server, nil
}

func (s *RelayServer) Run() {
	log.Printf("[RouteXia Relay v3 - Persistent Sessions] Listening on UDP :%d (region=%s)", *listenPort, *region)
	buf := make([]byte, MaxPacket)

	for {
		n, clientAddr, err := s.conn.ReadFromUDP(buf)
		if err != nil {
			log.Printf("[Relay] Read error: %v", err)
			continue
		}

		packet := make([]byte, n)
		copy(packet, buf[:n])

		go s.handlePacket(packet, clientAddr)
	}
}

func (s *RelayServer) handlePacket(packet []byte, clientAddr *net.UDPAddr) {
	clientKey := clientAddr.String()
	s.stats.RecordReceived(clientKey)

	if len(packet) < 5 {
		return
	}

	// Verify RXIA magic
	if packet[0] != Magic0 || packet[1] != Magic1 ||
		packet[2] != Magic2 || packet[3] != Magic3 {
		return
	}

	pktType := packet[4]

	// ── Ping probe (type 0x01) — echo back immediately ─────────────────────
	if pktType == TypePing {
		_, _ = s.conn.WriteToUDP(packet, clientAddr)
		return
	}

	// ── Data packet (type 0x02) ─────────────────────────────────────────────
	if pktType != TypeData || len(packet) < HeaderSizeV2 {
		return
	}

	// Parse destination IP + port (where to forward)
	destIP   := net.IP(packet[8:12])
	destPort := binary.BigEndian.Uint16(packet[12:14])

	// Parse client's local port (to route response back to PUBG)
	clientLocalPort := binary.BigEndian.Uint16(packet[14:16])

	// Parse payload
	payloadLen := binary.BigEndian.Uint16(packet[16:18])
	if int(payloadLen) > len(packet)-HeaderSizeV2 {
		return
	}
	payload := packet[HeaderSizeV2 : HeaderSizeV2+int(payloadLen)]

	// Get or create persistent session to this game server
	sess, err := s.sessions.GetOrCreate(clientAddr, clientLocalPort, destIP, destPort)
	if err != nil {
		log.Printf("[Relay] GetOrCreate session error: %v", err)
		return
	}

	// Forward payload on the persistent UDP socket
	_, err = sess.gameConn.Write(payload)
	if err != nil {
		log.Printf("[Relay] Write to game server failed: %v", err)
		return
	}

	s.stats.RecordForwarded()
}

func (s *RelayServer) PrintStats() {
	ticker := time.NewTicker(10 * time.Second)
	for range ticker.C {
		s.stats.mu.Lock()
		s.sessions.mu.RLock()
		sessionCount := len(s.sessions.sessions)
		s.sessions.mu.RUnlock()

		log.Printf("[Stats] Received=%d Forwarded=%d Returned=%d ActiveSessions=%d ActiveClients=%d",
			s.stats.PacketsReceived,
			s.stats.PacketsForwarded,
			s.stats.PacketsReturned,
			sessionCount,
			len(s.stats.ActiveClients))
		s.stats.mu.Unlock()
	}
}

func main() {
	flag.Parse()

	server, err := NewRelayServer(*listenPort)
	if err != nil {
		log.Fatalf("Failed to start relay: %v", err)
	}

	go server.PrintStats()
	server.Run()
}

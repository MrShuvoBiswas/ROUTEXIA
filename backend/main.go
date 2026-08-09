package main

import (
	"context"
	"database/sql"
	"flag"
	"fmt"
	"log"
	"net/http"
	"strings"

	"routexia-backend/database"
	"routexia-backend/handlers"
	"routexia-backend/security"

	"github.com/google/uuid"
)

var (
	port   = flag.Int("port", 8080, "Backend API port")
	dbPath = flag.String("db", "routexia.db", "SQLite database file path")
)

func main() {
	flag.Parse()

	log.Printf("==================================================")
	log.Printf("  RouteXia Management & Subscription API Server   ")
	log.Printf("==================================================")

	// Initialize Database
	db, err := database.InitDB(*dbPath)
	if err != nil {
		log.Fatalf("Failed to initialize database: %v", err)
	}
	defer db.Close()

	// Seed default data
	seedInitialData(db)

	// Setup Router
	mux := http.NewServeMux()

	// ── Public Routes ─────────────────────────────────────────────────────────
	mux.HandleFunc("/api/v1/auth/register", withCORS(handlers.Register))
	mux.HandleFunc("/api/v1/auth/login", withCORS(handlers.Login))
	mux.HandleFunc("/api/v1/auth/firebase", withCORS(handlers.FirebaseAuth))
	mux.HandleFunc("/api/v1/relays", withCORS(handlers.GetRelays))
	mux.HandleFunc("/auth", func(w http.ResponseWriter, r *http.Request) {
		http.ServeFile(w, r, "web/auth.html")
	})
	mux.HandleFunc("/api/v1/health", withCORS(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		fmt.Fprintf(w, `{"status":"ok","service":"RouteXia Management API"}`)
	}))

	// ── Authenticated User Routes ─────────────────────────────────────────────
	mux.HandleFunc("/api/v1/user/profile", withCORS(withAuth(handlers.GetProfile)))

	// ── Admin Routes ──────────────────────────────────────────────────────────
	mux.HandleFunc("/api/v1/admin/relays", withCORS(withAdmin(handlers.AddRelay)))
	mux.HandleFunc("/api/v1/admin/relays/delete", withCORS(withAdmin(handlers.DeleteRelay)))
	mux.HandleFunc("/api/v1/admin/subscriptions/extend", withCORS(withAdmin(handlers.ExtendSubscription)))
	mux.HandleFunc("/api/v1/admin/devices/ban", withCORS(withAdmin(handlers.BanDevice)))

	serverAddr := fmt.Sprintf(":%d", *port)
	log.Printf("[API] Server listening on http://0.0.0.0%s", serverAddr)
	if err := http.ListenAndServe(serverAddr, mux); err != nil {
		log.Fatalf("Server failed: %v", err)
	}
}

// ── Middlewares ───────────────────────────────────────────────────────────────

func withCORS(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Authorization")

		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusOK)
			return
		}

		next(w, r)
	}
}

func withAuth(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		authHeader := r.Header.Get("Authorization")
		if authHeader == "" || !strings.HasPrefix(authHeader, "Bearer ") {
			http.Error(w, "Missing or invalid Authorization header", http.StatusUnauthorized)
			return
		}

		tokenString := strings.TrimPrefix(authHeader, "Bearer ")
		claims, err := security.ValidateToken(tokenString)
		if err != nil {
			http.Error(w, "Invalid or expired token", http.StatusUnauthorized)
			return
		}

		ctx := context.WithValue(r.Context(), "claims", claims)
		next(w, r.WithContext(ctx))
	}
}

func withAdmin(next http.HandlerFunc) http.HandlerFunc {
	return withAuth(func(w http.ResponseWriter, r *http.Request) {
		claims, ok := r.Context().Value("claims").(*security.Claims)
		if !ok || claims == nil || claims.Role != "admin" {
			http.Error(w, "Admin privileges required", http.StatusForbidden)
			return
		}
		next(w, r)
	})
}

// ── Database Seeder ───────────────────────────────────────────────────────────

func seedInitialData(db *sql.DB) {
	// Seed Admin user if not exists
	var count int
	_ = db.QueryRow("SELECT COUNT(*) FROM users WHERE role = 'admin'").Scan(&count)
	if count == 0 {
		adminID := uuid.New().String()
		pwHash, _ := security.HashPassword("admin123")
		_, err := db.Exec("INSERT INTO users (id, email, password_hash, role) VALUES (?, ?, ?, 'admin')",
			adminID, "admin@routexia.com", pwHash)
		if err == nil {
			log.Printf("[Seed] Default admin created: admin@routexia.com / admin123")
		}
	}

	// Seed Singapore relay if no relays exist
	var relayCount int
	_ = db.QueryRow("SELECT COUNT(*) FROM relay_servers").Scan(&relayCount)
	if relayCount == 0 {
		relayID := uuid.New().String()
		_, err := db.Exec(`
			INSERT INTO relay_servers (id, region_code, display_name, host, port, is_active, priority)
			VALUES (?, 'SG', 'Singapore 01 (AWS EC2)', '3.1.31.201', 9001, 1, 1)`,
			relayID)
		if err == nil {
			log.Printf("[Seed] Initial Singapore relay registered: 3.1.31.201:9001")
		}
	}
}

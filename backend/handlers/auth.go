package handlers

import (
	"database/sql"
	"encoding/json"
	"log"
	"net/http"
	"strings"
	"time"

	"routexia-backend/database"
	"routexia-backend/models"
	"routexia-backend/security"

	"github.com/google/uuid"
)

const TrialDurationDays = 4

func Register(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req models.RegisterRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
		return
	}

	req.Email = strings.ToLower(strings.TrimSpace(req.Email))
	req.HWID = strings.TrimSpace(req.HWID)

	if req.Email == "" || len(req.Password) < 6 {
		http.Error(w, "Valid email and minimum 6 character password required", http.StatusBadRequest)
		return
	}

	if req.HWID == "" {
		http.Error(w, "Valid Hardware ID (HWID) required", http.StatusBadRequest)
		return
	}

	// Check if email already exists
	var existingID string
	err := database.DB.QueryRow("SELECT id FROM users WHERE email = ?", req.Email).Scan(&existingID)
	if err == nil {
		http.Error(w, "Email already registered", http.StatusConflict)
		return
	} else if err != sql.ErrNoRows {
		log.Printf("[Auth] DB check error: %v", err)
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}

	// Hash password
	pwHash, err := security.HashPassword(req.Password)
	if err != nil {
		http.Error(w, "Failed to hash password", http.StatusInternalServerError)
		return
	}

	userID := uuid.New().String()

	// Begin transaction
	tx, err := database.DB.Begin()
	if err != nil {
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}
	defer tx.Rollback()

	// Insert user
	_, err = tx.Exec("INSERT INTO users (id, email, password_hash, role) VALUES (?, ?, ?, 'user')",
		userID, req.Email, pwHash)
	if err != nil {
		log.Printf("[Auth] Insert user error: %v", err)
		http.Error(w, "Failed to create user", http.StatusInternalServerError)
		return
	}

	// ── Check HWID Anti-Abuse ────────────────────────────────────────────────
	var (
		hwidBanned       int
		trialAlreadyUsed bool
	)

	var devHWID string
	err = tx.QueryRow("SELECT hwid_hash, is_banned FROM devices WHERE hwid_hash = ?", req.HWID).
		Scan(&devHWID, &hwidBanned)

	if err == sql.ErrNoRows {
		// New Hardware device! Record it and grant trial
		_, err = tx.Exec("INSERT INTO devices (hwid_hash, first_user_id, trial_claimed, is_banned) VALUES (?, ?, 1, 0)",
			req.HWID, userID)
		if err != nil {
			log.Printf("[Auth] Insert device error: %v", err)
		}
		trialAlreadyUsed = false
	} else if err == nil {
		if hwidBanned == 1 {
			http.Error(w, "This hardware device has been banned", http.StatusForbidden)
			return
		}
		// Device already claimed trial in the past
		trialAlreadyUsed = true
	}

	// ── Create Subscription ───────────────────────────────────────────────────
	subID := uuid.New().String()
	now := time.Now()
	var (
		planType   string
		status     string
		expiresAt  time.Time
		isTrial    bool
		canConnect bool
		subMsg     string
	)

	if !trialAlreadyUsed {
		planType = "trial"
		status = "active"
		expiresAt = now.AddDate(0, 0, TrialDurationDays)
		isTrial = true
		canConnect = true
		subMsg = "🎉 4-Day Free Trial Activated!"
	} else {
		planType = "trial"
		status = "expired"
		expiresAt = now
		isTrial = true
		canConnect = false
		subMsg = "⚠️ Free trial was already used on this PC. Please purchase a subscription to connect."
	}

	_, err = tx.Exec(`INSERT INTO subscriptions 
		(id, user_id, hwid_hash, plan_type, status, starts_at, expires_at) 
		VALUES (?, ?, ?, ?, ?, ?, ?)`,
		subID, userID, req.HWID, planType, status, now, expiresAt)
	if err != nil {
		log.Printf("[Auth] Insert subscription error: %v", err)
		http.Error(w, "Failed to create subscription", http.StatusInternalServerError)
		return
	}

	if err := tx.Commit(); err != nil {
		http.Error(w, "Transaction commit failed", http.StatusInternalServerError)
		return
	}

	// Generate JWT
	token, err := security.GenerateToken(userID, req.Email, "user")
	if err != nil {
		http.Error(w, "Failed to generate token", http.StatusInternalServerError)
		return
	}

	// Fetch active relays
	relays := fetchActiveRelays()

	daysLeft := int(time.Until(expiresAt).Hours() / 24)
	if daysLeft < 0 {
		daysLeft = 0
	}

	resp := models.AuthResponse{
		Token: token,
		User: models.UserDto{
			ID:        userID,
			Email:     req.Email,
			Role:      "user",
			CreatedAt: now,
		},
		Subscription: models.SubscriptionDto{
			ID:         subID,
			PlanType:   planType,
			Status:     status,
			DaysLeft:   daysLeft,
			ExpiresAt:  expiresAt,
			IsTrial:    isTrial,
			CanConnect: canConnect,
			Message:    subMsg,
		},
		Relays: relays,
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusCreated)
	_ = json.NewEncoder(w).Encode(resp)
}

func Login(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req models.LoginRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
		return
	}

	req.Email = strings.ToLower(strings.TrimSpace(req.Email))
	req.HWID = strings.TrimSpace(req.HWID)

	var (
		userID       string
		email        string
		passwordHash string
		role         string
		createdAt    time.Time
	)

	err := database.DB.QueryRow(`SELECT id, email, password_hash, role, created_at FROM users WHERE email = ?`,
		req.Email).Scan(&userID, &email, &passwordHash, &role, &createdAt)

	if err == sql.ErrNoRows || !security.CheckPasswordHash(req.Password, passwordHash) {
		http.Error(w, "Invalid email or password", http.StatusUnauthorized)
		return
	} else if err != nil {
		log.Printf("[Auth] Login query error: %v", err)
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}

	// Check device ban if HWID provided
	if req.HWID != "" {
		var isBanned int
		err := database.DB.QueryRow("SELECT is_banned FROM devices WHERE hwid_hash = ?", req.HWID).Scan(&isBanned)
		if err == nil && isBanned == 1 {
			http.Error(w, "This hardware device has been banned", http.StatusForbidden)
			return
		}
	}

	// Get latest subscription
	subDto := getLatestSubscription(userID)

	token, err := security.GenerateToken(userID, email, role)
	if err != nil {
		http.Error(w, "Failed to generate token", http.StatusInternalServerError)
		return
	}

	relays := fetchActiveRelays()

	resp := models.AuthResponse{
		Token: token,
		User: models.UserDto{
			ID:        userID,
			Email:     email,
			Role:      role,
			CreatedAt: createdAt,
		},
		Subscription: subDto,
		Relays:       relays,
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(resp)
}

func GetProfile(w http.ResponseWriter, r *http.Request) {
	claims, ok := r.Context().Value("claims").(*security.Claims)
	if !ok || claims == nil {
		http.Error(w, "Unauthorized", http.StatusUnauthorized)
		return
	}

	var (
		userID    string
		email     string
		role      string
		createdAt time.Time
	)

	err := database.DB.QueryRow("SELECT id, email, role, created_at FROM users WHERE id = ?", claims.UserID).
		Scan(&userID, &email, &role, &createdAt)
	if err != nil {
		http.Error(w, "User not found", http.StatusNotFound)
		return
	}

	subDto := getLatestSubscription(userID)
	relays := fetchActiveRelays()

	resp := models.AuthResponse{
		User: models.UserDto{
			ID:        userID,
			Email:     email,
			Role:      role,
			CreatedAt: createdAt,
		},
		Subscription: subDto,
		Relays:       relays,
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(resp)
}

// ── Helpers ───────────────────────────────────────────────────────────────────

func getLatestSubscription(userID string) models.SubscriptionDto {
	var (
		subID     string
		planType  string
		status    string
		expiresAt time.Time
	)

	err := database.DB.QueryRow(`
		SELECT id, plan_type, status, expires_at 
		FROM subscriptions 
		WHERE user_id = ? 
		ORDER BY expires_at DESC 
		LIMIT 1`, userID).Scan(&subID, &planType, &status, &expiresAt)

	now := time.Now()
	if err == sql.ErrNoRows {
		return models.SubscriptionDto{
			PlanType:   "none",
			Status:     "expired",
			DaysLeft:   0,
			CanConnect: false,
			Message:    "No active subscription found. Please subscribe to start optimizing.",
		}
	}

	isExpired := expiresAt.Before(now)
	if isExpired && status == "active" {
		status = "expired"
		_, _ = database.DB.Exec("UPDATE subscriptions SET status = 'expired' WHERE id = ?", subID)
	}

	daysLeft := int(time.Until(expiresAt).Hours() / 24)
	if daysLeft < 0 {
		daysLeft = 0
	}

	isTrial := planType == "trial"
	canConnect := status == "active" && !isExpired

	var msg string
	if canConnect {
		if isTrial {
			msg = "🎉 4-Day Free Trial Active"
		} else {
			msg = "👑 Premium Active"
		}
	} else {
		msg = "⚠️ Subscription Expired"
	}

	return models.SubscriptionDto{
		ID:         subID,
		PlanType:   planType,
		Status:     status,
		DaysLeft:   daysLeft,
		ExpiresAt:  expiresAt,
		IsTrial:    isTrial,
		CanConnect: canConnect,
		Message:    msg,
	}
}

func fetchActiveRelays() []models.RelayServerDto {
	rows, err := database.DB.Query(`
		SELECT id, region_code, display_name, host, port, priority, is_active 
		FROM relay_servers 
		WHERE is_active = 1 
		ORDER BY priority ASC, region_code ASC`)
	if err != nil {
		log.Printf("[Relays] Fetch error: %v", err)
		return []models.RelayServerDto{}
	}
	defer rows.Close()

	relays := make([]models.RelayServerDto, 0)
	for rows.Next() {
		var r models.RelayServerDto
		var isActiveInt int
		if err := rows.Scan(&r.ID, &r.RegionCode, &r.DisplayName, &r.Host, &r.Port, &r.Priority, &isActiveInt); err == nil {
			r.IsActive = isActiveInt == 1
			relays = append(relays, r)
		}
	}
	return relays
}

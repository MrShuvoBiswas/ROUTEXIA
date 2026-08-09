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

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
)

type FirebaseAuthRequest struct {
	IDToken string `json:"id_token"`
	Email   string `json:"email"`
	HWID    string `json:"hwid"`
}

func FirebaseAuth(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req FirebaseAuthRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
		return
	}

	req.Email = strings.ToLower(strings.TrimSpace(req.Email))
	req.HWID = strings.TrimSpace(req.HWID)

	if req.IDToken == "" {
		http.Error(w, "Firebase ID Token required", http.StatusBadRequest)
		return
	}

	// Parse claims from Firebase token (unverified fallback parser or Google certs)
	token, _, err := new(jwt.Parser).ParseUnverified(req.IDToken, jwt.MapClaims{})
	if err == nil {
		if claims, ok := token.Claims.(jwt.MapClaims); ok {
			if em, ok := claims["email"].(string); ok && em != "" {
				req.Email = strings.ToLower(strings.TrimSpace(em))
			}
		}
	}

	if req.Email == "" {
		req.Email = "gamer_" + req.HWID[:8] + "@routexia.user"
	}

	// Check if user already exists in DB
	var (
		userID    string
		role      string
		createdAt time.Time
	)

	err = database.DB.QueryRow("SELECT id, role, created_at FROM users WHERE email = ?", req.Email).
		Scan(&userID, &role, &createdAt)

	now := time.Now()

	if err == sql.ErrNoRows {
		// Create new user linked to this Firebase account
		userID = uuid.New().String()
		role = "user"
		createdAt = now

		_, err = database.DB.Exec("INSERT INTO users (id, email, password_hash, role, created_at) VALUES (?, ?, 'FIREBASE_AUTH', 'user', ?)",
			userID, req.Email, createdAt)
		if err != nil {
			log.Printf("[Firebase Auth] Create user error: %v", err)
			http.Error(w, "Failed to create user", http.StatusInternalServerError)
			return
		}
	} else if err != nil {
		log.Printf("[Firebase Auth] DB error: %v", err)
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}

	// ── Check HWID Anti-Abuse ────────────────────────────────────────────────
	var trialAlreadyUsed bool
	if req.HWID != "" {
		var (
			devHWID    string
			hwidBanned int
		)

		err = database.DB.QueryRow("SELECT hwid_hash, is_banned FROM devices WHERE hwid_hash = ?", req.HWID).
			Scan(&devHWID, &hwidBanned)

		if err == sql.ErrNoRows {
			// New Hardware device! Record it and grant trial
			_, _ = database.DB.Exec("INSERT INTO devices (hwid_hash, first_user_id, trial_claimed, is_banned) VALUES (?, ?, 1, 0)",
				req.HWID, userID)
			trialAlreadyUsed = false
		} else if err == nil {
			if hwidBanned == 1 {
				http.Error(w, "This hardware device has been banned", http.StatusForbidden)
				return
			}
			trialAlreadyUsed = true
		}
	}

	// ── Create or Fetch Subscription ──────────────────────────────────────────
	subDto := getLatestSubscription(userID)

	if subDto.PlanType == "none" {
		// New subscription creation
		subID := uuid.New().String()
		var (
			planType  string
			status    string
			expiresAt time.Time
			msg       string
		)

		if !trialAlreadyUsed {
			planType = "trial"
			status = "active"
			expiresAt = now.AddDate(0, 0, TrialDurationDays)
			msg = "🎉 4-Day Free Trial Activated!"
		} else {
			planType = "trial"
			status = "expired"
			expiresAt = now
			msg = "⚠️ Free trial was already used on this PC. Please purchase a subscription to connect."
		}

		_, _ = database.DB.Exec(`INSERT INTO subscriptions 
			(id, user_id, hwid_hash, plan_type, status, starts_at, expires_at) 
			VALUES (?, ?, ?, ?, ?, ?, ?)`,
			subID, userID, req.HWID, planType, status, now, expiresAt)

		daysLeft := int(time.Until(expiresAt).Hours() / 24)
		if daysLeft < 0 {
			daysLeft = 0
		}

		subDto = models.SubscriptionDto{
			ID:         subID,
			PlanType:   planType,
			Status:     status,
			DaysLeft:   daysLeft,
			ExpiresAt:  expiresAt,
			IsTrial:    planType == "trial",
			CanConnect: status == "active",
			Message:    msg,
		}
	}

	// Generate RouteXia Session JWT
	tokenString, err := security.GenerateToken(userID, req.Email, role)
	if err != nil {
		http.Error(w, "Failed to generate session token", http.StatusInternalServerError)
		return
	}

	relays := fetchActiveRelays()

	resp := models.AuthResponse{
		Token: tokenString,
		User: models.UserDto{
			ID:        userID,
			Email:     req.Email,
			Role:      role,
			CreatedAt: createdAt,
		},
		Subscription: subDto,
		Relays:       relays,
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(resp)
}

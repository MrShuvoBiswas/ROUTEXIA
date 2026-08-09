package handlers

import (
	"encoding/json"
	"net/http"
	"time"

	"routexia-backend/database"
	"routexia-backend/models"

	"github.com/google/uuid"
)

// ExtendSubscription (Admin or Payment Webhook) grants/extends a subscription for a user
func ExtendSubscription(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req models.ExtendSubscriptionRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
		return
	}

	if req.UserID == "" || req.Days <= 0 {
		http.Error(w, "Valid UserID and positive Days are required", http.StatusBadRequest)
		return
	}

	if req.PlanType == "" {
		req.PlanType = "premium"
	}

	// Fetch latest subscription expiration
	var currentExpiresAt time.Time
	err := database.DB.QueryRow(`
		SELECT expires_at FROM subscriptions 
		WHERE user_id = ? 
		ORDER BY expires_at DESC LIMIT 1`, req.UserID).Scan(&currentExpiresAt)

	now := time.Now()
	var newExpiresAt time.Time

	if err == nil && currentExpiresAt.After(now) {
		// Extend from current active expiration
		newExpiresAt = currentExpiresAt.AddDate(0, 0, req.Days)
	} else {
		// Fresh activation from now
		newExpiresAt = now.AddDate(0, 0, req.Days)
	}

	subID := uuid.New().String()
	_, err = database.DB.Exec(`
		INSERT INTO subscriptions (id, user_id, hwid_hash, plan_type, status, starts_at, expires_at)
		VALUES (?, ?, '', ?, 'active', ?, ?)`,
		subID, req.UserID, req.PlanType, now, newExpiresAt)
	if err != nil {
		http.Error(w, "Failed to extend subscription: "+err.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"success":    true,
		"user_id":    req.UserID,
		"plan_type":  req.PlanType,
		"days_added": req.Days,
		"expires_at": newExpiresAt,
		"message":    "Subscription extended successfully",
	})
}

// BanDevice (Admin only) bans a hardware device from using the accelerator
func BanDevice(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req struct {
		HWID   string `json:"hwid"`
		Reason string `json:"reason"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.HWID == "" {
		http.Error(w, "Valid HWID required", http.StatusBadRequest)
		return
	}

	_, err := database.DB.Exec(`
		INSERT INTO devices (hwid_hash, first_user_id, trial_claimed, is_banned, ban_reason)
		VALUES (?, '', 1, 1, ?)
		ON CONFLICT(hwid_hash) DO UPDATE SET is_banned = 1, ban_reason = ?`,
		req.HWID, req.Reason, req.Reason)

	if err != nil {
		http.Error(w, "Failed to ban device: "+err.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"success": true,
		"message": "Device banned successfully",
	})
}

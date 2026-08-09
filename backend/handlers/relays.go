package handlers

import (
	"encoding/json"
	"net/http"
	"strings"

	"routexia-backend/database"
	"routexia-backend/models"

	"github.com/google/uuid"
)

// GetRelays returns list of all active relay servers (Public / Authenticated)
func GetRelays(w http.ResponseWriter, r *http.Request) {
	relays := fetchActiveRelays()
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(relays)
}

// AddRelay (Admin only) adds a new relay VPS to the database
func AddRelay(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req models.AddRelayRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
		return
	}

	req.RegionCode = strings.ToUpper(strings.TrimSpace(req.RegionCode))
	req.DisplayName = strings.TrimSpace(req.DisplayName)
	req.Host = strings.TrimSpace(req.Host)

	if req.RegionCode == "" || req.Host == "" || req.Port <= 0 {
		http.Error(w, "RegionCode, Host and valid Port are required", http.StatusBadRequest)
		return
	}

	if req.DisplayName == "" {
		req.DisplayName = req.RegionCode + " Relay"
	}

	if req.Priority <= 0 {
		req.Priority = 1
	}

	id := uuid.New().String()
	_, err := database.DB.Exec(`
		INSERT INTO relay_servers (id, region_code, display_name, host, port, is_active, priority)
		VALUES (?, ?, ?, ?, ?, 1, ?)`,
		id, req.RegionCode, req.DisplayName, req.Host, req.Port, req.Priority)
	if err != nil {
		http.Error(w, "Failed to add relay: "+err.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusCreated)
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"success": true,
		"id":      id,
		"message": "Relay server added successfully",
	})
}

// DeleteRelay (Admin only) removes or deactivates a relay server
func DeleteRelay(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodDelete && r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	id := r.URL.Query().Get("id")
	if id == "" {
		http.Error(w, "Query parameter 'id' is required", http.StatusBadRequest)
		return
	}

	_, err := database.DB.Exec("DELETE FROM relay_servers WHERE id = ?", id)
	if err != nil {
		http.Error(w, "Failed to delete relay: "+err.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"success": true,
		"message": "Relay server deleted successfully",
	})
}

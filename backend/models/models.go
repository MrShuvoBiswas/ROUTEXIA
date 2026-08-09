package models

import "time"

type RegisterRequest struct {
	Email    string `json:"email"`
	Password string `json:"password"`
	HWID     string `json:"hwid"`
}

type LoginRequest struct {
	Email    string `json:"email"`
	Password string `json:"password"`
	HWID     string `json:"hwid"`
}

type AuthResponse struct {
	Token        string           `json:"token"`
	User         UserDto          `json:"user"`
	Subscription SubscriptionDto  `json:"subscription"`
	Relays       []RelayServerDto `json:"relays"`
}

type UserDto struct {
	ID        string    `json:"id"`
	Email     string    `json:"email"`
	Role      string    `json:"role"`
	CreatedAt time.Time `json:"created_at"`
}

type SubscriptionDto struct {
	ID          string    `json:"id"`
	PlanType    string    `json:"plan_type"` // 'trial', 'monthly', 'quarterly', 'yearly'
	Status      string    `json:"status"`    // 'active', 'expired', 'banned'
	DaysLeft    int       `json:"days_left"`
	ExpiresAt   time.Time `json:"expires_at"`
	IsTrial     bool      `json:"is_trial"`
	CanConnect  bool      `json:"can_connect"`
	Message     string    `json:"message"`
}

type RelayServerDto struct {
	ID          string `json:"id"`
	RegionCode  string `json:"region_code"`  // 'SG', 'IN', 'DXB'
	DisplayName string `json:"display_name"` // 'Singapore 01 (AWS EC2)'
	Host        string `json:"host"`         // '3.1.31.201' or domain
	Port        int    `json:"port"`         // 9001
	Priority    int    `json:"priority"`
	IsActive    bool   `json:"is_active"`
}

type AddRelayRequest struct {
	RegionCode  string `json:"region_code"`
	DisplayName string `json:"display_name"`
	Host        string `json:"host"`
	Port        int    `json:"port"`
	Priority    int    `json:"priority"`
}

type ExtendSubscriptionRequest struct {
	UserID   string `json:"user_id"`
	PlanType string `json:"plan_type"`
	Days     int    `json:"days"`
}

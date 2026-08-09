using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RouteXia.VpnClient.Api
{
    public class RegisterRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("hwid")]
        public string HWID { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("hwid")]
        public string HWID { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("user")]
        public UserDto? User { get; set; }

        [JsonPropertyName("subscription")]
        public SubscriptionDto? Subscription { get; set; }

        [JsonPropertyName("relays")]
        public List<RelayServerDto> Relays { get; set; } = new();
    }

    public class UserDto
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class SubscriptionDto
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("plan_type")]
        public string PlanType { get; set; } = "none"; // 'trial', 'monthly', 'quarterly', 'yearly'

        [JsonPropertyName("status")]
        public string Status { get; set; } = "expired"; // 'active', 'expired', 'banned'

        [JsonPropertyName("days_left")]
        public int DaysLeft { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("is_trial")]
        public bool IsTrial { get; set; }

        [JsonPropertyName("can_connect")]
        public bool CanConnect { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class RelayServerDto
    {
        [JsonPropertyName("id")]
        public string ID { get; set; } = string.Empty;

        [JsonPropertyName("region_code")]
        public string RegionCode { get; set; } = "SG";

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        [JsonPropertyName("port")]
        public int Port { get; set; } = 9001;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;
    }
}

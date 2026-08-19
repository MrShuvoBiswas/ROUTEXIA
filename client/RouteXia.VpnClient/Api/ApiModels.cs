using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RouteXia.VpnClient.Api
{
    public class SessionConnectRequest
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("relayId")]
        public string RelayId { get; set; } = string.Empty;

        [JsonPropertyName("relayName")]
        public string RelayName { get; set; } = string.Empty;

        [JsonPropertyName("relayRegion")]
        public string RelayRegion { get; set; } = string.Empty;

        [JsonPropertyName("relayHost")]
        public string RelayHost { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string? GameName { get; set; }

        [JsonPropertyName("gameProcess")]
        public string? GameProcess { get; set; }

        [JsonPropertyName("pingMs")]
        public int? PingMs { get; set; }

        [JsonPropertyName("hwid")]
        public string? Hwid { get; set; }

        [JsonPropertyName("clientVersion")]
        public string? ClientVersion { get; set; }
    }

    public class SessionConnectResponse
    {
        [JsonPropertyName("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("connected_at")]
        public DateTime ConnectedAt { get; set; }

        [JsonPropertyName("relay_auth_ticket")]
        public RelayAuthTicket? RelayAuthTicket { get; set; }
    }

    public class RelayAuthTicket
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public class SessionHeartbeatRequest
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("pingMs")]
        public int? PingMs { get; set; }

        [JsonPropertyName("downloadMbps")]
        public double? DownloadMbps { get; set; }

        [JsonPropertyName("uploadMbps")]
        public double? UploadMbps { get; set; }

        [JsonPropertyName("bytesSent")]
        public long? BytesSent { get; set; }

        [JsonPropertyName("bytesReceived")]
        public long? BytesReceived { get; set; }

        [JsonPropertyName("gameName")]
        public string? GameName { get; set; }

        [JsonPropertyName("gameProcess")]
        public string? GameProcess { get; set; }
    }

    public class SessionDisconnectRequest
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("bytesSent")]
        public long? BytesSent { get; set; }

        [JsonPropertyName("bytesReceived")]
        public long? BytesReceived { get; set; }
    }

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

        [JsonPropertyName("is_banned")]
        public bool IsBanned { get; set; }

        [JsonPropertyName("ban_reason")]
        public string? BanReason { get; set; }

        [JsonPropertyName("custom_discount_pct")]
        public int CustomDiscountPct { get; set; }

        [JsonPropertyName("can_manual_select_relay")]
        public bool CanManualSelectRelay { get; set; }

        [JsonPropertyName("referral_code")]
        public string ReferralCode { get; set; } = string.Empty;

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

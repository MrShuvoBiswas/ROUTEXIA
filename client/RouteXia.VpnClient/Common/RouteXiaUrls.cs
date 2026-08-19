namespace RouteXia.VpnClient.Common
{
    /// <summary>
    /// Centralized RouteXia production and development URLs.
    /// Separates public user web portals from administrative endpoints.
    /// </summary>
    public static class RouteXiaUrls
    {
        /// <summary>
        /// Main Public Landing Page
        /// </summary>
        public const string LandingPage = "https://routexia.in";

        /// <summary>
        /// User Web Application & Registration Portal
        /// Completely isolated from the Admin Panel
        /// </summary>
        public const string UserWebPortal = "https://app.routexia.in";
        public const string RegisterUrl = "https://app.routexia.in";
        public const string AuthPortalUrl = "https://app.routexia.in";
        public const string ForgotPasswordUrl = "https://app.routexia.in";

        /// <summary>
        /// RouteXia Backend API Server (Production Cloud Domain)
        /// </summary>
        public const string ProductionApiUrl = "https://api.routexia.in";
        public const string LocalApiUrl = "https://api.routexia.in";

        /// <summary>
        /// Cloudflare R2 / Public release bucket endpoint for Velopack auto-updates.
        /// </summary>
        public const string ReleaseUpdateUrl = "https://releases.routexia.in";
    }
}

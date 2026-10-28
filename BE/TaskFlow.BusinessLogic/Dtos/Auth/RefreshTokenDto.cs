namespace TaskFlow.BusinessLogic.Dtos.Auth
{
    public class RefreshTokenDto
    {
        /// <summary>
        /// New token
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// New refresh token
        /// </summary>
        public string? RefreshToken { get; set; }

        public DateTimeOffset RefreshTokenExpiresAt { get; set; }

        public string[] Permissions { get; set; } = [];

        public bool Success { get; set; }
    }
}

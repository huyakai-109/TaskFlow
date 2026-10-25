namespace TaskFlow.BusinessLogic.Dtos.Auth
{
    public class LoginResultDto
    {
        public string? Token { get; set; }

        public string? RefreshToken { get; set; }

        public bool Success { get; set; }

        public bool InvalidCredential { get; set; }

        public bool WillBeLockedOut { get; set; }

        public bool IsLockedOut { get; set; }
    }
}

namespace TaskFlow.Api.Models.Requests.Auth
{
    public class LoginReq
    {
        public Guid DeviceGuid { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }
    }
}

namespace TaskFlow.Api.Models.Requests.Auth
{
    public class RefreshTokenReq
    {
        public Guid DeviceGuid { get; set; }

        public string? Token { get; set; }
    }
}

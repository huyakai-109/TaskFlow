using AutoMapper;
using TaskFlow.Api.Models.Responses.Auth;
using TaskFlow.BusinessLogic.Dtos.Auth;

namespace TaskFlow.Api.Mappers
{
    public class AuthMapper : Profile
    {
        public AuthMapper()
        {
            CreateMap<LoginResultDto, LoginRes>();
            CreateMap<RefreshTokenDto, RefreshTokenRes>();
        }
    }
}

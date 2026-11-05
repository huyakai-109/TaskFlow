using AutoMapper;
using TaskFlow.Api.Models.Responses.Users;
using TaskFlow.BusinessLogic.Dtos.Users;

namespace TaskFlow.Api.Mappers
{
    public class UserMapper : Profile
    {
        public UserMapper()
        {
            CreateMap<UserResDto, UserRes>();
        }
    }
}

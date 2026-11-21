using AutoMapper;
using TaskFlow.Api.Models.Responses.Profile;
using TaskFlow.BusinessLogic.Dtos.Profile;

namespace TaskFlow.Api.Mappers
{
    public class ProfileMapper : Profile
    {
        public ProfileMapper()
        {
            CreateMap<MeResDto, MeRes>();
        }
    }
}

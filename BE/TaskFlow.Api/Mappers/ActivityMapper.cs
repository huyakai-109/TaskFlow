using AutoMapper;
using TaskFlow.Api.Models.Responses.Activity;
using TaskFlow.BusinessLogic.Dtos.Activity;

namespace TaskFlow.Api.Mappers
{
    public class ActivityMapper : Profile
    {
        public ActivityMapper()
        {
            CreateMap<ActivityResDto, ActivityRes>()
                .ForMember(dest => dest.Task, opt => opt.MapFrom(src => new ActivityTaskRes
                {
                    Id = src.TaskId,
                    Title = src.TaskTitle,
                    IsDeleted = src.TaskIsDeleted,
                }));
        }
    }
}

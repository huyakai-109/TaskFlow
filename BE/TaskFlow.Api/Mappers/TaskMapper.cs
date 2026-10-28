using AutoMapper;
using TaskFlow.Api.Models.Requests.Tasks;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.BusinessLogic.Dtos.Tasks;

namespace TaskFlow.Api.Mappers
{
    public class TaskMapper : Profile
    {
        public TaskMapper()
        {
            CreateMap<TaskCreateReq, TaskCreateReqDto>();
            CreateMap<AllowedTransitionResDto, AllowedTransitionRes>();
            CreateMap<TaskDetailResDto, TaskDetailRes>()
                .ForMember(dest => dest.Assignee, opt => opt.MapFrom(src => src.AssigneeId.HasValue
                    ? new AssigneeRes { Id = src.AssigneeId.Value, UserName = src.AssigneeUserName ?? string.Empty }
                    : null));
        }
    }
}

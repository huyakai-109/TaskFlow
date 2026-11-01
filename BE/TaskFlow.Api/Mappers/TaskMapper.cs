using AutoMapper;
using TaskFlow.Api.Models.Requests.Tasks;
using TaskFlow.Api.Models.Responses.Base;
using TaskFlow.Api.Models.Responses.Tasks;
using TaskFlow.BusinessLogic.Dtos.Tasks;

namespace TaskFlow.Api.Mappers
{
    public class TaskMapper : Profile
    {
        public TaskMapper()
        {
            CreateMap<TaskCreateReq, TaskCreateReqDto>();
            CreateMap<TaskMoveReq, TaskMoveReqDto>();
            CreateMap<TaskReopenReq, TaskReopenReqDto>();
            CreateMap<TaskUpdateReq, TaskUpdateReqDto>();
            CreateMap<TaskUpdatePriorityReq, TaskUpdatePriorityReqDto>();
            CreateMap<TaskUpdateDueDateReq, TaskUpdateDueDateReqDto>();
            CreateMap<TaskAssignReq, TaskAssignReqDto>();
            CreateMap<AllowedTransitionResDto, AllowedTransitionRes>();
            CreateMap<TaskDetailResDto, TaskDetailRes>()
                .ForMember(dest => dest.Assignee, opt => opt.MapFrom(src => src.AssigneeId.HasValue
                    ? new AssigneeRes { Id = src.AssigneeId.Value, UserName = src.AssigneeUserName ?? string.Empty }
                    : null));
            CreateMap<TaskSummaryResDto, TaskSummaryRes>()
                .ForMember(dest => dest.Assignee, opt => opt.MapFrom(src => src.AssigneeId.HasValue
                    ? new AssigneeRes { Id = src.AssigneeId.Value, UserName = src.AssigneeUserName ?? string.Empty }
                    : null));
            CreateMap<BoardColumnResDto, BoardColumnRes>()
                .ForMember(dest => dest.Pagination, opt => opt.MapFrom(src =>
                    new PaginationRes(src.TotalItems, src.CurrentCount, 0, src.PageSize)));
            CreateMap<BoardResDto, BoardRes>();
        }
    }
}

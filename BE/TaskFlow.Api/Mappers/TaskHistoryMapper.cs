using AutoMapper;
using TaskFlow.Api.Models.Responses.TaskHistories;
using TaskFlow.BusinessLogic.Dtos.TaskHistories;

namespace TaskFlow.Api.Mappers
{
    public class TaskHistoryMapper : Profile
    {
        public TaskHistoryMapper()
        {
            CreateMap<TaskHistoryResDto, TaskHistoryRes>();
        }
    }
}

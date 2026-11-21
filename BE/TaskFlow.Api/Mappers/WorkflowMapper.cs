using AutoMapper;
using TaskFlow.Api.Models.Responses.Workflow;
using TaskFlow.BusinessLogic.Dtos.Workflow;

namespace TaskFlow.Api.Mappers
{
    public class WorkflowMapper : Profile
    {
        public WorkflowMapper()
        {
            CreateMap<WorkflowResDto, WorkflowRes>()
                .ForMember(dest => dest.OverdueScan, opt => opt.MapFrom(src => new OverdueScanRes
                {
                    LastFiredAt = src.LastFiredAt,
                    NextFireAt = src.NextFireAt,
                }));
        }
    }
}

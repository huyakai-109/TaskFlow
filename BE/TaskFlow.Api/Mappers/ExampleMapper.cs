using AutoMapper;
using TaskFlow.Api.Models.Requests.Examples;
using TaskFlow.Api.Models.Responses.Examples;
using TaskFlow.BusinessLogic.Dtos.Examples;
using TaskFlow.DataAccess.Entities;

namespace TaskFlow.Api.Mappers
{
    public class ExampleMapper : Profile
    {
        public ExampleMapper()
        {
            CreateMap<ExampleNewReq, ExampleDto>();
            CreateMap<ExampleDto, Example>();
            CreateMap<Example, ExampleDto>();
            CreateMap<ExampleDto, ExampleNewRes>();
        }
    }
}

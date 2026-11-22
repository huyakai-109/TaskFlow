using AutoMapper;
using TaskFlow.Api.Models.Responses.Reports;
using TaskFlow.BusinessLogic.Dtos.Reports;

namespace TaskFlow.Api.Mappers
{
    public class ReportMapper : Profile
    {
        public ReportMapper()
        {
            CreateMap<ReportDeliveryDto, ReportDeliveryRes>()
                .ForMember(dest => dest.ByPriority, opt => opt.MapFrom(src => new ReportByPriorityRes
                {
                    Urgent = src.Urgent,
                    High = src.High,
                    Normal = src.Normal,
                }));
            CreateMap<ReportDistributionDto, DistributionRes>();
            CreateMap<ReportRateDto, RateRes>();
            CreateMap<ReportSpeedDto, ReportSpeedRes>();
            CreateMap<ReportReliabilityDto, ReportReliabilityRes>();
            CreateMap<ReportQualityDto, ReportQualityRes>();
            CreateMap<ReportLoadDto, ReportLoadRes>();
            CreateMap<ReportAttributionDto, ReportAttributionRes>();
            CreateMap<MetricSetDto, MetricSetRes>();
            CreateMap<ReportTeamMedianDto, TeamMedianRes>();
            CreateMap<ReportMetaDto, ReportMetaRes>();
            CreateMap<ReportUserRefDto, ReportUserRefRes>();
            CreateMap<ReportBucketDto, ReportBucketRes>();
            CreateMap<ReportScorecardResDto, ReportScorecardRes>();

            CreateMap<ReportTeamResDto, ReportTeamRes>()
                .ForMember(dest => dest.Team, opt => opt.MapFrom((src, _, _, context) => new ReportTeamTotalsRes
                {
                    Totals = context.Mapper.Map<MetricSetRes>(src.TeamTotals),
                    Contributors = src.Contributors,
                    UnassignedOverdueMarked = src.UnassignedOverdueMarked,
                }));
            CreateMap<ReportTeamMemberDto, ReportTeamMemberRes>();
            CreateMap<ReportHeatmapDto, ReportHeatmapRes>();
            CreateMap<ReportHeatmapRowDto, ReportHeatmapRowRes>();

            CreateMap<ReportTaskResDto, ReportTaskRes>()
                .ForMember(dest => dest.CreditedUser, opt => opt.MapFrom(src => src.CreditedUserId.HasValue
                    ? new ReportTaskCreditedUserRes { Id = src.CreditedUserId.Value, UserName = src.CreditedUserName, DisplayName = src.CreditedDisplayName }
                    : null));
        }
    }
}

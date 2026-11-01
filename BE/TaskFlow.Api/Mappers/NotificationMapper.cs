using AutoMapper;
using TaskFlow.Api.Models.Responses.Notifications;
using TaskFlow.BusinessLogic.Dtos.Notifications;

namespace TaskFlow.Api.Mappers
{
    public class NotificationMapper : Profile
    {
        public NotificationMapper()
        {
            CreateMap<NotificationResDto, NotificationRes>();
        }
    }
}

using TaskFlow.Common.Constants;

namespace TaskFlow.Api.Models.Requests.TaskHistories
{
    public class TaskHistoryGetListReq
    {
        public int Start { get; set; } = GlobalConstants.PageConfig.Start;

        public int Length { get; set; } = GlobalConstants.PageConfig.Length;
    }
}

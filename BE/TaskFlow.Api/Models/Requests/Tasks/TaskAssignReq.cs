using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Models.Requests.Tasks
{
    public class TaskAssignReq
    {
        public long? AssigneeId { get; set; }

        [Required]
        public Guid? Version { get; set; }
    }
}

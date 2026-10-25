using Microsoft.AspNetCore.Http;

namespace TaskFlow.BusinessLogic.Dtos.Storage
{
    public class UploadFileReqDto
    {
        public required IFormFile File { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

        public bool SkipRandomFileName { get; set; }
    }
}

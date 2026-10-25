using System.ComponentModel.DataAnnotations;
using TaskFlow.Common.Constants;

namespace TaskFlow.Api.Models.Requests.Base
{
    public class SearchReq
    {
        private string _keyword = string.Empty;

        public SearchReq()
        {
            this.Start = GlobalConstants.PageConfig.Start;
            this.Length = GlobalConstants.PageConfig.Length;
            this.SortColumn = string.Empty;
            this.Ascending = true;
            this.Keyword = string.Empty;
        }

        public int Start { get; set; }

        public int Length { get; set; }

        [MaxLength(100, ErrorMessage = "The maximum length of the Keyword is 100 characters")]
        public string Keyword
        {
            get => _keyword;
            set => _keyword = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public string? SortColumn { get; set; }

        public bool Ascending { get; set; }
    }
}

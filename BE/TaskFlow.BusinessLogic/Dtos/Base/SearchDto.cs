namespace TaskFlow.BusinessLogic.Dtos.Base
{
    public class SearchDto
    {
        public int Start { get; set; }

        public int Length { get; set; }

        public string? Keyword { get; set; }

        public string? SortColumn { get; set; }

        public bool Ascending { get; set; } = true;
    }
}

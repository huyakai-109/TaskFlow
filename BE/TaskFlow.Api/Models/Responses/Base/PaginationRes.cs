namespace TaskFlow.Api.Models.Responses.Base
{
    public class PaginationRes
    {
        public PaginationRes() { }

        public PaginationRes(int totalCount, int currentCount, int? skip, int? take)
        {
            TotalItems = totalCount;
            CurrentCount = currentCount;
            PageSize = take ?? totalCount;

            if (PageSize == 0) return;

            var remain = totalCount % PageSize != 0 ? 1 : 0;
            TotalPages = totalCount / PageSize + remain;
            PageNumber = (skip ?? 0) / PageSize + 1;
        }

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalPages { get; set; }

        public int TotalItems { get; set; }

        public int CurrentCount { get; set; }
    }
}

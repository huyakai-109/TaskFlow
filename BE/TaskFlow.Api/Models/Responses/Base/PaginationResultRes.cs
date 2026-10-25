namespace TaskFlow.Api.Models.Responses.Base
{
    public class PaginationResultRes<T> : ResultRes<T>
    {
        public PaginationRes? Pagination { get; set; }
    }
}

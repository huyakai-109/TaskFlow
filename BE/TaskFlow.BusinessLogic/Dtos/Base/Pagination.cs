using TaskFlow.Common.Constants;

namespace TaskFlow.BusinessLogic.Dtos.Base
{
    public class Pagination<T> where T : class
    {
        public Pagination() { }

        public Pagination(IQueryable<T> iQuery, int pageNumber = GlobalConstants.PageConfig.Start, int pageSize = GlobalConstants.PageConfig.Length)
        {
            this.Items = [];
            this.TotalItems = 0;
            this.TotalPages = 0;
            this.PageNumber = pageNumber;
            this.PageSize = pageSize;
            this.Query = iQuery;
            this.ExecutePaginate();
        }

        public Pagination(IEnumerable<T> iQuery, int pageNumber = GlobalConstants.PageConfig.Start, int pageSize = GlobalConstants.PageConfig.Length)
        {
            this.Items = [];
            this.TotalItems = 0;
            this.TotalPages = 0;
            this.PageNumber = pageNumber;
            this.PageSize = pageSize;
            this.QueryEnumerable = iQuery;
            this.ExecutePaginate2();
        }

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalPages { get; set; }

        public int TotalItems { get; set; }

        public List<T> Items { get; set; }

        private IQueryable<T> Query { get; set; }

        private IEnumerable<T> QueryEnumerable { get; set; }

        private void ExecutePaginate()
        {
            if (this.PageNumber <= 0)
            {
                this.PageNumber = GlobalConstants.PageConfig.Start;
            }

            if (this.PageSize <= 0)
            {
                this.PageSize = GlobalConstants.PageConfig.Length;
            }

            if (this.PageSize > GlobalConstants.PageConfig.MaxLength)
            {
                this.PageSize = GlobalConstants.PageConfig.MaxLength;
            }

            this.TotalItems = this.Query.Count();
            this.TotalPages = (int)Math.Ceiling((double)this.TotalItems / this.PageSize);
            this.Items = [.. this.Query.Skip((this.PageNumber - GlobalConstants.PageConfig.Start) * this.PageSize).Take(this.PageSize)];
        }

        private void ExecutePaginate2()
        {
            if (this.PageNumber <= 0)
            {
                this.PageNumber = GlobalConstants.PageConfig.Start;
            }

            if (this.PageSize <= 0)
            {
                this.PageSize = GlobalConstants.PageConfig.Length;
            }

            if (this.PageSize > GlobalConstants.PageConfig.MaxLength)
            {
                this.PageSize = GlobalConstants.PageConfig.MaxLength;
            }

            this.TotalItems = this.QueryEnumerable.Count();
            this.TotalPages = (int)Math.Ceiling((double)this.TotalItems / this.PageSize);
            this.Items = [.. this.QueryEnumerable.Skip((this.PageNumber - GlobalConstants.PageConfig.Start) * this.PageSize).Take(this.PageSize)];
        }
    }
}

namespace TaskFlow.Api.Models.Responses.Base
{
    // ADR 0007 (keyset paging for the Activity feed): a second paging style
    // and envelope next to the codebase's offset `start`/`length` +
    // PaginationRes default - scoped to an append-only, newest-first stream
    // only (GET /activity). No total count: "N results" has no cheap
    // meaning under keyset paging, so callers use `hasMore`.
    public class CursorResultRes<T> : ResultRes<T>
    {
        public CursorPageRes? Page { get; set; }
    }

    public class CursorPageRes
    {
        public CursorPageRes() { }

        public CursorPageRes(string? nextCursor, bool hasMore, int pageSize)
        {
            NextCursor = nextCursor;
            HasMore = hasMore;
            PageSize = pageSize;
        }

        public string? NextCursor { get; set; }

        public bool HasMore { get; set; }

        public int PageSize { get; set; }
    }
}

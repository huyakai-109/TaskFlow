namespace TaskFlow.DataAccess.IEntities
{
    public interface IBaseEntity
    {
        long CreatedBy { get; set; }

        DateTimeOffset CreatedAt { get; set; }

        long UpdatedBy { get; set; }

        DateTimeOffset UpdatedAt { get; set; }
    }
}

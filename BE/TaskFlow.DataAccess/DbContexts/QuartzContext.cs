using Microsoft.EntityFrameworkCore;

namespace TaskFlow.DataAccess.DbContexts
{
    public class QuartzContext : DbContext
    {
        public QuartzContext()
        {
        }

        public QuartzContext(DbContextOptions<QuartzContext> options)
            : base(options)
        {
        }
    }
}

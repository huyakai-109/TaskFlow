using TaskFlow.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TaskFlow.DataAccess.Configurations
{
    public class ExampleConfiguration : IEntityTypeConfiguration<Example>
    {
        public void Configure(EntityTypeBuilder<Example> builder)
        {
        }
    }
}

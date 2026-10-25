using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskFlow.DataAccess.IEntities;

namespace TaskFlow.DataAccess.Entities
{
    [Table("Examples")]
    public class Example : BaseEntity, IIsDeletedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public DateTimeOffset DateOfBirth { get; set; }

        public bool IsDeleted { get; set; }
    }
}

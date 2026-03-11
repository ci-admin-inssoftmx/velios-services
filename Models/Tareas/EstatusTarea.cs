using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("CatEstatusTarea", Schema = "dbo")]
public class EstatusTarea
{
    [Key]
    [Column("EstatusTareaId")]
    public int EstatusTareaId { get; set; }

    [Column("Codigo")]
    [MaxLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Column("Nombre")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;
}
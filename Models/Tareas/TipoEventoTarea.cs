using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Tareas;

[Table("CatTipoEventoTarea", Schema = "dbo")]
public class TipoEventoTarea
{
    [Key]
    [Column("TipoEventoTareaId")]
    public int TipoEventoTareaId { get; set; }

    [Column("Codigo")]
    [MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Column("Nombre")]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;
}
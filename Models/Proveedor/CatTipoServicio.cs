using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("CatTipoServicio", Schema = "dbo")]
public class CatTipoServicio
{
    [Key]
    public int TipoServicioId { get; set; }

    [MaxLength(10)]
    public string Codigo { get; set; } = "";

    [MaxLength(100)]
    public string Nombre { get; set; } = "";
    [MaxLength(100)]
    public string Descripcion { get; set; } = "";

    public bool IsDeleted { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Proveedores;

[Table("CatPaquete", Schema = "dbo")]
public class CatPaquete
{
    [Key]
    public int PaqueteId { get; set; }

    [Required, MaxLength(20)]
    public string Codigo { get; set; } = "";

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; }
}
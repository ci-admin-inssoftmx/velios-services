using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

[Table("tb_Clientes", Schema = "dbo")]
public class Cliente
{
    [Key]
    public int ClienteId { get; set; }

    [MaxLength(20)]
    public string? RFC { get; set; }

    [MaxLength(250)]
    public string? RazonSocial { get; set; }

    [MaxLength(250)]
    public string? NombreComercial { get; set; }

    [Required, MaxLength(150)]
    public string CorreoContacto { get; set; } = "";

    [MaxLength(20)]
    public string? TelefonoContacto { get; set; }

    public int EstatusClienteId { get; set; } = 1; // 1=ACTIVO

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; set; }
    public bool IsDeleted { get; set; } = false;
}
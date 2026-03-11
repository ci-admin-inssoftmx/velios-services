using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Clientes;

/// <summary>
/// Entidad del catálogo de estatus de cliente.
/// </summary>
[Table("CatEstatusCliente", Schema = "dbo")]
public class EstatusCliente
{
    [Key]
    [Column("EstatusClienteId")]
    public int EstatusClienteId { get; set; }
}
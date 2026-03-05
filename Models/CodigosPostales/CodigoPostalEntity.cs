using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.CodigosPostales;

[Table("tb_CodigosPostales", Schema = "dbo")]
public class CodigoPostalEntity
{
    [Key]
    public long CodigoPostalId { get; set; } // PK real

    public string d_codigo { get; set; } = "";
    public string d_estado { get; set; } = "";
    public string D_mnpio { get; set; } = "";
    public string d_asenta { get; set; } = "";
    public bool IsDeleted { get; set; }
}
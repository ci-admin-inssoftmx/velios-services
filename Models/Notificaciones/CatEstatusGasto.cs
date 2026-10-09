using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.NetworkInformation;

namespace velios.Api.Models.Notificaciones
{
    
    [Table("tb_CatEstatusGasto", Schema = "dbo")]
    public class CatEstatusGasto
    {
        [Column("idCatEstatusGasto")]
        [Key]
        public int IdCatEstatusGasto { get; set; }

        [Column("Estatus")]
        [MaxLength(50)]
        public string Estatus { get; set; }

        [Column("Descripcion")]
        [MaxLength(50)]
        public string Descripcion { get; set; }

    }

}

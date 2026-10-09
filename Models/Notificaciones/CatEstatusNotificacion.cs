using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.NetworkInformation;


namespace velios.Api.Models.Notificaciones
{

    [Table("tb_CatEstatusNotificacion", Schema = "dbo")]
    public class CatEstatusNotificacion
    {
        
        [Column("idCatEstatusNotificacion")]
        [Key]
        public int IdCatEstatusNotificacion { get; set; }

        [Column("Estatus")]
        [MaxLength(50)]
        public required string  Estatus { get; set; }

        [Column("Descripcion")]
        [MaxLength(500)]
        public required string Descripcion { get; set; }
    }

}

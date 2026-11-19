using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace velios.Api.Models.Notificaciones
{
    [Table("tb_CatTipoNotificacion", Schema = "dbo")]
    public class CatTipoNotificacion
    {
        [Column("idTipoNotificacion")]
        [Key]
        public int IdTipoNotificacion { get; set; }

        [Column("Evento")]
        [MaxLength(50)]
        public required string Evento { get; set; }

        [Column("Descripcion")]
        [MaxLength(500)]
        public required string Descripcion { get; set; }

    }

}

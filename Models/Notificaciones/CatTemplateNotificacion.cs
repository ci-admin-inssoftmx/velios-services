using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Notificaciones
{

    [Table("tb_CatTemplateNotificacion", Schema = "dbo")]
    public class CatTemplateNotificacion
    {

        [Column("IdTemplateNotificacion")]
        [Key]
        public int IdTemplateNotificacion { get; set; }

        [Column("nombre")]
        [MaxLength(50)]
        public required string Nombre { get; set; }

        [Column("asunto")]
        [MaxLength(500)]
        public required string Asunto { get; set; }

        [Column("contenidoHtml")]
        public required string ContenidoHtml { get; set; }

        [Column("activo")]
        public required bool Activo { get; set; }


        [Column("fechaRegistro")]
        public required DateTime FechaRegistro { get; set; }

        [Column("fechaModificacion")]
        public required DateTime FechaModificacion { get; set; }

    }
}

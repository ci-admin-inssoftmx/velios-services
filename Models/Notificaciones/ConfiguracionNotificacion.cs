using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Notificaciones
{
    [Table("tb_ConfiguracionNotificacion", Schema = "dbo")]
    public class ConfiguracionNotificacion
    {
        [Column("IdConfiguracion")]
        [Key]
        public int IdConfiguracion { get; set; }

        [Column("Clave")]
        [MaxLength(50)]
        public string Clave { get; set; }

        [Column("Valor")]
        [MaxLength(1000)]
        public string Valor { get; set; }

        [Column("Descripcion")]
        [MaxLength(500)]
        public string Descripcion { get; set; }

    }

}

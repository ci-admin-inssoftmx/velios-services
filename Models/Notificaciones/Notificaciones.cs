using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace velios.Api.Models.Notificaciones
{

    [Table("tb_Notificaciones", Schema = "dbo")]
    public class Notificaciones
    {

        [Key]
        public long IdNotificacion { get; set; }

        [Column("idTipoNotificacion")]
        public required int IdTipoNotificacion { get; set; }

        [Column("idCatEstatusNotificacion")]
        public required int IdCatEstatusNotificacion { get; set; }

        [Column("idReferencia")]
        public required int IdReferencia { get; set; }

        [Column("tablaReferencia")]
        [MaxLength(500)]
        public required string TablaReferencia { get; set; }

        [Column("intento")]
        public required int Intento { get; set; }

        [Column("NumeroReintentos")]
        public int NumeroReintentos { get; set; }

        [Column("fechaRegistro")]
        public required DateTime FechaRegistro { get; set; } = DateTime.Now;
       
        [Column("fechaEnvio")]
        public DateTime? FechaEnvio { get; set; }

    }
}


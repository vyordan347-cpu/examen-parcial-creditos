using System.ComponentModel.DataAnnotations;

namespace GestionCreditos.Models
{
    public enum EstadoSolicitud
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public class SolicitudCredito
    {
        public int Id { get; set; }

        [Required]
        public int ClienteId { get; set; }

        public Cliente? Cliente { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0")]
        public decimal MontoSolicitado { get; set; }

        public DateTime FechaSolicitud { get; set; } = DateTime.Now;

        public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

        public string? MotivoRechazo { get; set; }
    }
}
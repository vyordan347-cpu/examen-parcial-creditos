using System.ComponentModel.DataAnnotations;

namespace GestionCreditos.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Los ingresos mensuales deben ser mayores a 0")]
        public decimal IngresosMensuales { get; set; }

        public bool Activo { get; set; } = true;
    }
}
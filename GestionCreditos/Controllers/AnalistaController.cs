using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using GestionCreditos.Data;
using GestionCreditos.Models;

namespace GestionCreditos.Controllers
{
    [Authorize(Roles = "Analista")]
    public class AnalistaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public AnalistaController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: Analista
        public async Task<IActionResult> Index()
        {
            var pendientes = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .Where(s => s.Estado == EstadoSolicitud.Pendiente)
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            return View(pendientes);
        }

        // POST: Analista/Aprobar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(int id)
        {
            var solicitud = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                return NotFound();
            }

            // No procesar solicitudes ya aprobadas o rechazadas
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = "Esta solicitud ya fue procesada anteriormente.";
                return RedirectToAction(nameof(Index));
            }

            // No aprobar si el monto excede 5 veces los ingresos
            if (solicitud.MontoSolicitado > solicitud.Cliente.IngresosMensuales * 5)
            {
                TempData["Error"] = "No se puede aprobar: el monto excede 5 veces los ingresos del cliente.";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Aprobado;
            await _context.SaveChangesAsync();

            // Invalidar el caché del listado del cliente dueño de esta solicitud
            await _cache.RemoveAsync($"listado_solicitudes_{solicitud.Cliente.UsuarioId}");

            TempData["Exito"] = "Solicitud aprobada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Analista/Rechazar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
        {
            if (string.IsNullOrWhiteSpace(motivoRechazo))
            {
                TempData["Error"] = "El motivo de rechazo es obligatorio.";
                return RedirectToAction(nameof(Index));
            }

            var solicitud = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                return NotFound();
            }

            if (solicitud.Estado != EstadoSolicitud.Pendiente)
            {
                TempData["Error"] = "Esta solicitud ya fue procesada anteriormente.";
                return RedirectToAction(nameof(Index));
            }

            solicitud.Estado = EstadoSolicitud.Rechazado;
            solicitud.MotivoRechazo = motivoRechazo;
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync($"listado_solicitudes_{solicitud.Cliente.UsuarioId}");

            TempData["Exito"] = "Solicitud rechazada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
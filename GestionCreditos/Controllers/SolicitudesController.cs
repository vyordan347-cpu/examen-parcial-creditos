using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionCreditos.Data;
using GestionCreditos.Models;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
namespace GestionCreditos.Controllers
{
    [Authorize]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public SolicitudesController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private string ClaveCacheListado => $"listado_solicitudes_{UsuarioActualId}";

        private string UsuarioActualId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET: Solicitudes/Mis
        public async Task<IActionResult> Mis(EstadoSolicitud? estado, decimal? montoMin, decimal? montoMax, DateTime? fechaInicio, DateTime? fechaFin)
        {
            // Validación server-side: rango de fechas inválido
            if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
            {
                ModelState.AddModelError("", "La fecha de inicio no puede ser mayor a la fecha fin.");
            }

            // Validación server-side: montos negativos
            if (montoMin.HasValue && montoMin < 0)
            {
                ModelState.AddModelError("", "El monto mínimo no puede ser negativo.");
            }
            if (montoMax.HasValue && montoMax < 0)
            {
                ModelState.AddModelError("", "El monto máximo no puede ser negativo.");
            }

            var query = _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .Where(s => s.Cliente.UsuarioId == UsuarioActualId)
                .AsQueryable();

            if (!ModelState.IsValid)
            {
                ViewBag.Solicitudes = new List<SolicitudCredito>();
                return View();
            }

            if (estado.HasValue)
            {
                query = query.Where(s => s.Estado == estado.Value);
            }

            if (montoMin.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= montoMin.Value);
            }

            if (montoMax.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= montoMax.Value);
            }

            if (fechaInicio.HasValue)
            {
                query = query.Where(s => s.FechaSolicitud >= fechaInicio.Value);
            }

            if (fechaFin.HasValue)
            {
                query = query.Where(s => s.FechaSolicitud <= fechaFin.Value);
            }

            List<SolicitudCredito> solicitudes;

            // Solo usamos caché cuando NO hay filtros activos (el listado "base" del usuario)
            bool sinFiltros = !estado.HasValue && !montoMin.HasValue && !montoMax.HasValue && !fechaInicio.HasValue && !fechaFin.HasValue;

            if (sinFiltros)
            {
                var cacheado = await _cache.GetStringAsync(ClaveCacheListado);
                if (cacheado != null)
                {
                    solicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cacheado);
                }
                else
                {
                    solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();

                    var opciones = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    };
                    await _cache.SetStringAsync(ClaveCacheListado, JsonSerializer.Serialize(solicitudes), opciones);
                }
            }
            else
            {
                // Con filtros activos, siempre consultamos directo (no cacheamos combinaciones de filtros)
                solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
            }

            ViewBag.Solicitudes = solicitudes;
            return View();
        }

        // GET: Solicitudes/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var solicitud = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id && s.Cliente.UsuarioId == UsuarioActualId);

            if (solicitud == null)
            {
                return NotFound();
            }

            // Guardar en sesión (Redis) la última solicitud visitada
            HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
            HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("C"));

            return View(solicitud);
        }
        public IActionResult Crear()
        {
            return View();
        }

        // POST: Solicitudes/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(decimal montoSolicitado)
        {
            // 1) Usuario debe estar autenticado -> ya lo garantiza [Authorize] en la clase

            // 2) Buscar el cliente activo del usuario actual
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == UsuarioActualId && c.Activo);

            if (cliente == null)
            {
                ViewBag.Error = "No se encontró un cliente activo asociado a tu usuario.";
                return View();
            }

            // 3) Monto no puede superar 10 veces los ingresos mensuales
            if (montoSolicitado > cliente.IngresosMensuales * 10)
            {
                ViewBag.Error = "El monto solicitado no puede superar 10 veces tus ingresos mensuales.";
                return View();
            }

            if (montoSolicitado <= 0)
            {
                ViewBag.Error = "El monto solicitado debe ser mayor a 0.";
                return View();
            }

            // 4) No permitir más de una solicitud Pendiente por cliente
            var yaTienePendiente = await _context.SolicitudesCredito
                .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

            if (yaTienePendiente)
            {
                ViewBag.Error = "Ya tienes una solicitud Pendiente. No puedes registrar otra hasta que sea evaluada.";
                return View();
            }

            // Todo válido: crear la solicitud
            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente.Id,
                MontoSolicitado = montoSolicitado,
                Estado = EstadoSolicitud.Pendiente,
                FechaSolicitud = DateTime.Now
            };

             _context.SolicitudesCredito.Add(solicitud);
            await _context.SaveChangesAsync();

            // Invalidar el caché del listado, porque ahora hay una solicitud nueva
            await _cache.RemoveAsync(ClaveCacheListado);

            ViewBag.Exito = "Solicitud registrada correctamente. Quedó en estado Pendiente.";
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> EstadoActual(int id)
        {
            var solicitud = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s => s.Id == id && s.Cliente.UsuarioId == UsuarioActualId);

            if (solicitud == null)
            {
                return NotFound();
            }

            return Json(new
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado.ToString(),
                MotivoRechazo = solicitud.MotivoRechazo
            });
        }
    }
}
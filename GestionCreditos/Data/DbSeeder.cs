using Microsoft.AspNetCore.Identity;
using GestionCreditos.Models;

namespace GestionCreditos.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1) Crear el rol Analista si no existe
            if (!await roleManager.RoleExistsAsync("Analista"))
            {
                await roleManager.CreateAsync(new IdentityRole("Analista"));
            }

            // 2) Crear un usuario Analista si no existe
            var analistaEmail = "analista@tecnogas.com";
            var analista = await userManager.FindByEmailAsync(analistaEmail);
            if (analista == null)
            {
                analista = new IdentityUser
                {
                    UserName = analistaEmail,
                    Email = analistaEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(analista, "Analista123!");
                await userManager.AddToRoleAsync(analista, "Analista");
            }

            // 3) Crear un usuario Cliente de prueba si no existe
            var clienteEmail = "cliente@tecnogas.com";
            var usuarioCliente = await userManager.FindByEmailAsync(clienteEmail);
            if (usuarioCliente == null)
            {
                usuarioCliente = new IdentityUser
                {
                    UserName = clienteEmail,
                    Email = clienteEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(usuarioCliente, "Cliente123!");
            }

            // 4) Sembrar 2 clientes y 2 solicitudes si la tabla está vacía
            if (!context.Clientes.Any())
            {
                var cliente1 = new Cliente
                {
                    UsuarioId = usuarioCliente.Id,
                    IngresosMensuales = 3000,
                    Activo = true
                };

                var cliente2 = new Cliente
                {
                    UsuarioId = usuarioCliente.Id,
                    IngresosMensuales = 5000,
                    Activo = true
                };

                context.Clientes.AddRange(cliente1, cliente2);
                await context.SaveChangesAsync();

                context.SolicitudesCredito.AddRange(
                    new SolicitudCredito
                    {
                        ClienteId = cliente1.Id,
                        MontoSolicitado = 8000,
                        Estado = EstadoSolicitud.Pendiente,
                        FechaSolicitud = DateTime.Now
                    },
                    new SolicitudCredito
                    {
                        ClienteId = cliente2.Id,
                        MontoSolicitado = 10000,
                        Estado = EstadoSolicitud.Aprobado,
                        FechaSolicitud = DateTime.Now.AddDays(-5)
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}
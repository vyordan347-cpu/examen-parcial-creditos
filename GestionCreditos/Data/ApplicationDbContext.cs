using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GestionCreditos.Models;

namespace GestionCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
     public DbSet<Cliente> Clientes { get; set; }
    public DbSet<SolicitudCredito> SolicitudesCredito { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Un cliente solo puede tener UNA solicitud en estado "Pendiente" (Estado = 0)
        // Usamos un índice único filtrado: solo aplica cuando Estado = 0 (Pendiente)
        builder.Entity<SolicitudCredito>()
            .HasIndex(s => s.ClienteId)
            .HasFilter("\"Estado\" = 0")
            .IsUnique();

        builder.Entity<SolicitudCredito>()
            .Property(s => s.Estado)
            .HasConversion<int>();
        builder.Entity<Notificacion>()
            .HasIndex(n => n.MessageId)
            .IsUnique();
    }
}
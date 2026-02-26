using Microsoft.EntityFrameworkCore;
using velios.Api.Models.Asistencia;
using velios.Api.Models.Empleado;
using velios.Api.Models.Proveedores;
using velios.Api.Models.Security;

namespace velios.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // =============================
    // SEGURIDAD
    // =============================
    public DbSet<AccesoUsuarioColaborador> AccesosUsuarios => Set<AccesoUsuarioColaborador>();

    // =============================
    // EMPLEADOS
    // =============================
    public DbSet<Empleado> Empleados => Set<Empleado>();

    // =============================
    // ASISTENCIA
    // =============================
    public DbSet<AsistenciaRegistroRequest> AsistenciaRegistros => Set<AsistenciaRegistroRequest>();
    public DbSet<CatOrigen> CatOrigen => Set<CatOrigen>();
    public DbSet<CatPaquete> CatPaquetes => Set<CatPaquete>();
    public DbSet<ProveedorSuscripcion> ProveedorSuscripciones => Set<ProveedorSuscripcion>();

    public DbSet<CatTipoRegistro> CatTipoRegistro => Set<CatTipoRegistro>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<CatTipoServicio> CatTipoServicio => Set<CatTipoServicio>();
    public DbSet<ProveedorTipoServicio> ProveedorTipoServicios => Set<ProveedorTipoServicio>();
    public DbSet<ProveedorProyecto> ProveedorProyectos => Set<ProveedorProyecto>();

    public DbSet<ProveedorPresupuesto> ProveedorPresupuestos => Set<ProveedorPresupuesto>();

    public DbSet<ProveedorTrabajador> ProveedorTrabajadores => Set<ProveedorTrabajador>();
    public DbSet<ProveedorProyectoTrabajador> ProveedorProyectoTrabajadores => Set<ProveedorProyectoTrabajador>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =============================
        // ACCESOS
        // =============================
        modelBuilder.Entity<AccesoUsuarioColaborador>(e =>
        {
            e.ToTable("tb_AccesoUsuariosColaborador", "dbo");
            e.HasKey(x => x.IdAccesoUsuarioColaborador);
        });

        // =============================
        // EMPLEADOS
        // =============================
        modelBuilder.Entity<Empleado>(e =>
        {
            e.ToTable("tb_Empleados", "dbo");
            e.HasKey(x => x.IdEmpleado);
        });

        // =============================
        // ASISTENCIA REGISTRO
        // =============================
        modelBuilder.Entity<AsistenciaRegistroRequest>(e =>
        {
            e.ToTable("AsistenciaRegistro", "dbo"); // nombre REAL

            e.HasKey(x => x.AsistenciaId);

            e.Property(x => x.Fecha)
                .HasColumnType("date");

            e.Property(x => x.HoraEntrada)
                .HasColumnType("time");

            e.Property(x => x.HoraSalida)
                .HasColumnType("time");

            e.Property(x => x.Observaciones)
                .HasMaxLength(300);

            // FK -> Empleado
            e.HasOne<Empleado>()
                .WithMany()
                .HasForeignKey(x => x.IdEmpleado)
                .OnDelete(DeleteBehavior.Restrict);

            // FK -> CatOrigen
            e.HasOne<CatOrigen>()
                .WithMany()
                .HasForeignKey(x => x.OrigenId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK -> CatTipoRegistro
            e.HasOne<CatTipoRegistro>()
                .WithMany()
                .HasForeignKey(x => x.TipoRegistroId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =============================
        // CAT ORIGEN
        // =============================
        modelBuilder.Entity<CatOrigen>(e =>
        {
            e.ToTable("CatOrigen", "dbo");
            e.HasKey(x => x.OrigenId);
        });

        // =============================
        // CAT TIPO REGISTRO
        // =============================
        modelBuilder.Entity<CatTipoRegistro>(e =>
        {
            e.ToTable("CatTipoRegistro", "dbo");
            e.HasKey(x => x.TipoRegistroId);
        });

        modelBuilder.Entity<CatPaquete>(e =>
        {
            e.ToTable("CatPaquete", "dbo");
            e.HasKey(x => x.PaqueteId);
        });

        modelBuilder.Entity<ProveedorSuscripcion>(e =>
        {
            e.ToTable("tb_ProveedorSuscripcion", "dbo");
            e.HasKey(x => x.ProveedorSuscripcionId);
        });
        // =============================
        // PROVEEDORES
        // =============================
        modelBuilder.Entity<Proveedor>(e =>
        {
            e.ToTable("tb_Proveedores", "dbo");
            e.HasKey(x => x.ProveedorId);
        });

        modelBuilder.Entity<CatTipoServicio>(e =>
        {
            e.ToTable("CatTipoServicio", "dbo");
            e.HasKey(x => x.TipoServicioId);
        });

        modelBuilder.Entity<ProveedorTipoServicio>(e =>
        {
            e.ToTable("tb_ProveedorTipoServicio", "dbo");
            e.HasKey(x => x.ProveedorTipoServicioId);
        });
        modelBuilder.Entity<CatPaquete>(e =>
        {
            e.ToTable("CatPaquete", "dbo");
            e.HasKey(x => x.PaqueteId);
        });

        modelBuilder.Entity<ProveedorSuscripcion>(e =>
        {
            e.ToTable("tb_ProveedorSuscripcion", "dbo");
            e.HasKey(x => x.ProveedorSuscripcionId);
        });

    }
}
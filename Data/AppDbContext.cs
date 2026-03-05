using Microsoft.EntityFrameworkCore;
using velios.Api.Models.Asistencia;
using velios.Api.Models.Clientes;
using velios.Api.Models.CodigosPostales;
using velios.Api.Models.Empleado;
using velios.Api.Models.Proveedores;
using velios.Api.Models.Security;

namespace velios.Api.Data;

/// <summary>
/// Contexto principal de base de datos de la API Velios.
/// 
/// Gestiona el acceso a las entidades del sistema mediante Entity Framework Core.
/// Representa la unidad de trabajo (Unit of Work) y el patrón Repository implícito.
/// 
/// Módulos incluidos:
/// - Seguridad
/// - Empleados
/// - Asistencia
/// - Proveedores
/// - Clientes
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Constructor que recibe la configuración del contexto.
    /// </summary>
    /// <param name="options">Opciones de configuración de EF Core.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // =========================================================
    // SEGURIDAD
    // =========================================================

    /// <summary>
    /// Tabla de accesos de usuarios colaboradores.
    /// Contiene credenciales, intentos de login y estado de acceso.
    /// </summary>
    public DbSet<AccesoUsuarioColaborador> AccesosUsuarios => Set<AccesoUsuarioColaborador>();

    // =========================================================
    // EMPLEADOS
    // =========================================================

    /// <summary>
    /// Tabla principal de empleados registrados en el sistema.
    /// </summary>
    public DbSet<Empleado> Empleados => Set<Empleado>();

    // =========================================================
    // ASISTENCIA
    // =========================================================

    /// <summary>
    /// Registros de asistencia diaria de empleados.
    /// </summary>
    public DbSet<AsistenciaRegistroRequest> AsistenciaRegistros => Set<AsistenciaRegistroRequest>();

    /// <summary>
    /// Catálogo de orígenes de registro (ej. App, Web, Biométrico).
    /// </summary>
    public DbSet<CatOrigen> CatOrigen => Set<CatOrigen>();

    /// <summary>
    /// Catálogo de paquetes disponibles.
    /// </summary>
    public DbSet<CatPaquete> CatPaquetes => Set<CatPaquete>();

    /// <summary>
    /// Suscripciones activas de proveedores.
    /// </summary>
    public DbSet<ProveedorSuscripcion> ProveedorSuscripciones => Set<ProveedorSuscripcion>();

    /// <summary>
    /// Catálogo de tipos de registro de asistencia.
    /// </summary>
    public DbSet<CatTipoRegistro> CatTipoRegistro => Set<CatTipoRegistro>();

    // =========================================================
    // PROVEEDORES
    // =========================================================

    /// <summary>
    /// Tabla principal de proveedores.
    /// Contiene información fiscal, comercial y credenciales de acceso.
    /// </summary>
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    /// <summary>
    /// Catálogo de tipos de servicio ofrecidos por proveedores.
    /// </summary>
    public DbSet<CatTipoServicio> CatTipoServicio => Set<CatTipoServicio>();

    /// <summary>
    /// Relación muchos-a-muchos entre proveedores y tipos de servicio.
    /// </summary>
    public DbSet<ProveedorTipoServicio> ProveedorTipoServicios => Set<ProveedorTipoServicio>();

    /// <summary>
    /// Proyectos asociados a proveedores.
    /// </summary>
    public DbSet<ProveedorProyecto> ProveedorProyectos => Set<ProveedorProyecto>();

    /// <summary>
    /// Presupuestos asignados a proveedores.
    /// </summary>
    public DbSet<ProveedorPresupuesto> ProveedorPresupuestos => Set<ProveedorPresupuesto>();

    /// <summary>
    /// Trabajadores pertenecientes a un proveedor.
    /// </summary>
    public DbSet<ProveedorTrabajador> ProveedorTrabajadores => Set<ProveedorTrabajador>();

    /// <summary>
    /// Relación entre proyectos y trabajadores de proveedor.
    /// </summary>
    public DbSet<ProveedorProyectoTrabajador> ProveedorProyectoTrabajadores => Set<ProveedorProyectoTrabajador>();

    // =========================================================
    // CLIENTES
    // =========================================================

    /// <summary>
    /// Tabla principal de clientes.
    /// </summary>
    public DbSet<Cliente> Clientes => Set<Cliente>();

    /// <summary>
    /// Relación entre clientes y proveedores.
    /// </summary>
    public DbSet<ClienteProveedor> ClienteProveedor => Set<ClienteProveedor>();

    /// <summary>
    /// Proyectos asociados a clientes.
    /// </summary>
    public DbSet<ClienteProyecto> ClienteProyectos => Set<ClienteProyecto>();

    /// <summary>
    /// Relación entre proyectos de clientes y proveedores.
    /// </summary>
    public DbSet<ClienteProyectoProveedor> ClienteProyectoProveedores => Set<ClienteProyectoProveedor>();

    /// <summary>
    /// Presupuestos asignados a proyectos de clientes.
    /// </summary>
    public DbSet<ClienteProyectoPresupuesto> ClienteProyectoPresupuestos => Set<ClienteProyectoPresupuesto>();

    /// <summary>
    /// Centros de trabajo registrados.
    /// </summary>
    public DbSet<CentroTrabajo> CentrosTrabajo => Set<CentroTrabajo>();

    /// <summary>
    /// Polígonos geográficos asociados a centros de trabajo.
    /// </summary>
    public DbSet<CentroTrabajoPoligono> CentroTrabajoPoligonos => Set<CentroTrabajoPoligono>();

    /// <summary>
    /// Codigos Postales.
    /// </summary>
    public DbSet<CodigoPostalEntity> CodigosPostales => Set<CodigoPostalEntity>();

    /// <summary>
    /// Colonia por Codigo Postale .
    /// </summary>
    public DbSet<CodigoPostalColonia> CodigoPostalColonias => Set<CodigoPostalColonia>();

    public DbSet<ProveedorDocumento> ProveedorDocumentos => Set<ProveedorDocumento>();

    public DbSet<CatTipoDocumentoProveedor> CatTipoDocumentoProveedor => Set<CatTipoDocumentoProveedor>();

    // =========================================================
    // CONFIGURACIÓN DE MODELOS
    // =========================================================

    /// <summary>
    /// Configuración avanzada de entidades y relaciones.
    /// Define:
    /// - Nombres reales de tablas
    /// - Llaves primarias
    /// - Relaciones (FK)
    /// - Restricciones
    /// - Índices únicos
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        modelBuilder.Entity<Cliente>()
        .HasIndex(x => x.CorreoContacto)
        .IsUnique()
        .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<ClienteProveedor>()
            .HasIndex(x => new { x.ClienteId, x.ProveedorId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<ClienteProyectoProveedor>()
            .HasIndex(x => new { x.ProyectoId, x.ProveedorId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<CodigoPostalColonia>().HasNoKey();

        modelBuilder.Entity<ProveedorDocumento>(e =>
        {
            e.ToTable("tb_ProveedorDocumentos", "dbo");
            e.HasKey(x => x.ProveedorDocumentoId);
        });
    }
}
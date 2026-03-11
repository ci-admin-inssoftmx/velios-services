using Microsoft.EntityFrameworkCore;
using velios.Api.Models.Proveedores;

namespace velios.Api.Data
{
    /// <summary>
    /// Contexto de base de datos para Nomclick.
    /// Se utiliza para guardar una copia de la información del proveedor.
    /// </summary>
    public class NomclickDbContext : DbContext
    {
        /// <summary>
        /// Constructor que recibe la configuración del contexto desde Program.cs.
        /// </summary>
        public NomclickDbContext(DbContextOptions<NomclickDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Representa la tabla tb_Proveedores en la base de datos Nomclick.
        /// </summary>
        public DbSet<Proveedor> Proveedores => Set<Proveedor>();

        /// <summary>
        /// Configuración de las entidades mediante Fluent API.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Llamada base de EF Core
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Proveedor>(e =>
            {
                // Nombre de tabla y esquema
                e.ToTable("tb_Proveedores", "dbo");

                // Clave primaria
                e.HasKey(x => x.ProveedorId);

                // Indica que ProveedorId NO será generado por Nomclick,
                // porque el valor vendrá desde Velios
                e.Property(x => x.ProveedorId)
                    .ValueGeneratedNever();

                // Configuración de longitudes según tu tabla
                e.Property(x => x.RFC).HasMaxLength(20);
                e.Property(x => x.RazonSocial).HasMaxLength(250);
                e.Property(x => x.NombreComercial).HasMaxLength(250);
                e.Property(x => x.CorreoContacto).HasMaxLength(150).IsRequired();
                e.Property(x => x.TelefonoContacto).HasMaxLength(20);
                e.Property(x => x.RepresentanteLegal).HasMaxLength(250);
                e.Property(x => x.CreatedBy).HasMaxLength(450);
                e.Property(x => x.ModifiedBy).HasMaxLength(450);
                e.Property(x => x.PasswordHash).HasMaxLength(255);
                e.Property(x => x.Calle).HasMaxLength(250);
                e.Property(x => x.CodigoPostal).HasMaxLength(10);
                e.Property(x => x.Colonia).HasMaxLength(150);
                e.Property(x => x.DelegacionMunicipio).HasMaxLength(150);
                e.Property(x => x.Ciudad).HasMaxLength(150);
                e.Property(x => x.Estado).HasMaxLength(150);
                e.Property(x => x.Pais).HasMaxLength(80);
                e.Property(x => x.LogoUrl).HasMaxLength(500);

                // Campo obligatorio según la tabla
                e.Property(x => x.IsDeleted).IsRequired();
            });
        }
    }
}
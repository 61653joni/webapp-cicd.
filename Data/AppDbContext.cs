using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Prenda> Prendas { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Marca> Marcas { get; set; }
        public DbSet<Talla> Tallas { get; set; }
        public DbSet<Color> Colores { get; set; }
        public DbSet<Genero> Generos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<Usuario>()
                .Property(u => u.Email)
                .IsRequired();
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Producto>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Producto>()
                .Property(p => p.Nombre)
                .IsRequired();
            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio)
                .HasPrecision(10, 2);

            // Catálogos: nombre obligatorio y sin duplicados
            ConfigurarCatalogo<Categoria>(modelBuilder);
            ConfigurarCatalogo<Marca>(modelBuilder);
            ConfigurarCatalogo<Talla>(modelBuilder);
            ConfigurarCatalogo<Color>(modelBuilder);
            ConfigurarCatalogo<Genero>(modelBuilder);

            // Prendas: relaciones N:1 con cada catálogo. Restrict impide borrar un valor de catálogo en uso
            var prenda = modelBuilder.Entity<Prenda>();
            prenda.HasKey(p => p.Id);
            prenda.Property(p => p.Nombre).IsRequired();
            prenda.Property(p => p.Precio).HasPrecision(10, 2);
            prenda.HasOne(p => p.Categoria).WithMany().HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            prenda.HasOne(p => p.Marca).WithMany().HasForeignKey(p => p.MarcaId).OnDelete(DeleteBehavior.Restrict);
            prenda.HasOne(p => p.Talla).WithMany().HasForeignKey(p => p.TallaId).OnDelete(DeleteBehavior.Restrict);
            prenda.HasOne(p => p.Color).WithMany().HasForeignKey(p => p.ColorId).OnDelete(DeleteBehavior.Restrict);
            prenda.HasOne(p => p.Genero).WithMany().HasForeignKey(p => p.GeneroId).OnDelete(DeleteBehavior.Restrict);

            // Datos iniciales de ejemplo
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nombre = "Camiseta" }, new Categoria { Id = 2, Nombre = "Pantalón" },
                new Categoria { Id = 3, Nombre = "Vestido" }, new Categoria { Id = 4, Nombre = "Sudadera" },
                new Categoria { Id = 5, Nombre = "Chaqueta" }, new Categoria { Id = 6, Nombre = "Zapatos" },
                new Categoria { Id = 7, Nombre = "Falda" }, new Categoria { Id = 8, Nombre = "Camisa" },
                new Categoria { Id = 9, Nombre = "Accesorio" }, new Categoria { Id = 10, Nombre = "Abrigo" });

            modelBuilder.Entity<Marca>().HasData(
                new Marca { Id = 1, Nombre = "Zara" }, new Marca { Id = 2, Nombre = "Levi's" },
                new Marca { Id = 3, Nombre = "Mango" }, new Marca { Id = 4, Nombre = "Nike" },
                new Marca { Id = 5, Nombre = "Bershka" }, new Marca { Id = 6, Nombre = "Adidas" },
                new Marca { Id = 7, Nombre = "H&M" }, new Marca { Id = 8, Nombre = "Tommy Hilfiger" },
                new Marca { Id = 9, Nombre = "New Era" }, new Marca { Id = 10, Nombre = "Massimo Dutti" });

            modelBuilder.Entity<Talla>().HasData(
                new Talla { Id = 1, Nombre = "XS" }, new Talla { Id = 2, Nombre = "S" },
                new Talla { Id = 3, Nombre = "M" }, new Talla { Id = 4, Nombre = "L" },
                new Talla { Id = 5, Nombre = "XL" }, new Talla { Id = 6, Nombre = "32" },
                new Talla { Id = 7, Nombre = "42" }, new Talla { Id = 8, Nombre = "Única" });

            modelBuilder.Entity<Color>().HasData(
                new Color { Id = 1, Nombre = "Blanco" }, new Color { Id = 2, Nombre = "Azul" },
                new Color { Id = 3, Nombre = "Rojo" }, new Color { Id = 4, Nombre = "Gris" },
                new Color { Id = 5, Nombre = "Negro" }, new Color { Id = 6, Nombre = "Beige" },
                new Color { Id = 7, Nombre = "Celeste" }, new Color { Id = 8, Nombre = "Verde" },
                new Color { Id = 9, Nombre = "Camel" });

            modelBuilder.Entity<Genero>().HasData(
                new Genero { Id = 1, Nombre = "Hombre" }, new Genero { Id = 2, Nombre = "Mujer" },
                new Genero { Id = 3, Nombre = "Unisex" });

            var fecha = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            prenda.HasData(
                new Prenda { Id = 1, Nombre = "Camiseta básica", CategoriaId = 1, MarcaId = 1, TallaId = 3, ColorId = 1, GeneroId = 3, Precio = 12.99m, Stock = 50, FechaCreacion = fecha },
                new Prenda { Id = 2, Nombre = "Jeans slim fit", CategoriaId = 2, MarcaId = 2, TallaId = 6, ColorId = 2, GeneroId = 1, Precio = 59.90m, Stock = 30, FechaCreacion = fecha },
                new Prenda { Id = 3, Nombre = "Vestido floral", CategoriaId = 3, MarcaId = 3, TallaId = 2, ColorId = 3, GeneroId = 2, Precio = 39.95m, Stock = 15, FechaCreacion = fecha },
                new Prenda { Id = 4, Nombre = "Sudadera con capucha", CategoriaId = 4, MarcaId = 4, TallaId = 4, ColorId = 4, GeneroId = 3, Precio = 49.99m, Stock = 25, FechaCreacion = fecha },
                new Prenda { Id = 5, Nombre = "Chaqueta de cuero", CategoriaId = 5, MarcaId = 5, TallaId = 3, ColorId = 5, GeneroId = 2, Precio = 89.00m, Stock = 8, FechaCreacion = fecha },
                new Prenda { Id = 6, Nombre = "Zapatillas running", CategoriaId = 6, MarcaId = 6, TallaId = 7, ColorId = 5, GeneroId = 1, Precio = 99.95m, Stock = 20, FechaCreacion = fecha },
                new Prenda { Id = 7, Nombre = "Falda plisada", CategoriaId = 7, MarcaId = 7, TallaId = 3, ColorId = 6, GeneroId = 2, Precio = 24.99m, Stock = 18, FechaCreacion = fecha },
                new Prenda { Id = 8, Nombre = "Camisa Oxford", CategoriaId = 8, MarcaId = 8, TallaId = 5, ColorId = 7, GeneroId = 1, Precio = 69.00m, Stock = 12, FechaCreacion = fecha },
                new Prenda { Id = 9, Nombre = "Gorra clásica", CategoriaId = 9, MarcaId = 9, TallaId = 8, ColorId = 8, GeneroId = 3, Precio = 29.99m, Stock = 40, FechaCreacion = fecha },
                new Prenda { Id = 10, Nombre = "Abrigo de lana", CategoriaId = 10, MarcaId = 10, TallaId = 4, ColorId = 9, GeneroId = 2, Precio = 149.00m, Stock = 0, Disponible = false, FechaCreacion = fecha }
            );
        }

        private static void ConfigurarCatalogo<T>(ModelBuilder modelBuilder) where T : Catalogo
        {
            modelBuilder.Entity<T>().HasKey(c => c.Id);
            modelBuilder.Entity<T>().Property(c => c.Nombre).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<T>().HasIndex(c => c.Nombre).IsUnique();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;

namespace WebApp.Services
{
    // Reglas compartidas por la API HTTP (PrendasController) y el servidor socket
    public static class PrendaReglas
    {
        // Prendas con sus catálogos (JOIN)
        public static IQueryable<Prenda> ConCatalogos(AppDbContext context) => context.Prendas
            .Include(p => p.Categoria)
            .Include(p => p.Marca)
            .Include(p => p.Talla)
            .Include(p => p.Color)
            .Include(p => p.Genero);

        public static async Task<string?> ValidarAsync(AppDbContext context, PrendaRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Nombre)) return "Nombre requerido";
            if (r.Nombre.Trim().Length > 100) return "El nombre no puede tener más de 100 caracteres";
            if (r.Precio < 0) return "El precio no puede ser negativo";
            if (decimal.Round(r.Precio, 2) != r.Precio) return "El precio no puede tener más de 2 decimales";
            if (r.Stock < 0) return "El stock no puede ser negativo";
            if (!await context.Categorias.AnyAsync(c => c.Id == r.CategoriaId)) return $"categoriaId {r.CategoriaId} no existe";
            if (!await context.Marcas.AnyAsync(c => c.Id == r.MarcaId)) return $"marcaId {r.MarcaId} no existe";
            if (!await context.Tallas.AnyAsync(c => c.Id == r.TallaId)) return $"tallaId {r.TallaId} no existe";
            if (!await context.Colores.AnyAsync(c => c.Id == r.ColorId)) return $"colorId {r.ColorId} no existe";
            if (!await context.Generos.AnyAsync(c => c.Id == r.GeneroId)) return $"generoId {r.GeneroId} no existe";
            return null;
        }

        public static void AplicarCambios(Prenda prenda, PrendaRequest r)
        {
            prenda.Nombre = r.Nombre.Trim();
            prenda.CategoriaId = r.CategoriaId;
            prenda.MarcaId = r.MarcaId;
            prenda.TallaId = r.TallaId;
            prenda.ColorId = r.ColorId;
            prenda.GeneroId = r.GeneroId;
            prenda.Precio = r.Precio;
            prenda.Stock = r.Stock;
            prenda.Disponible = r.Disponible;
        }
    }
}

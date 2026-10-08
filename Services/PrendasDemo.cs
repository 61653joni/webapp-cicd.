using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;

namespace WebApp.Services
{
    // Prendas que la app inserta al arrancar si todavía no existen (se buscan por nombre).
    // Demo de CI/CD: agregar una línea a la lista, hacer git push y, cuando termine el pipeline,
    // la prenda aparece en GET /api/prendas en el servidor.
    public static class PrendasDemo
    {
        public static readonly PrendaRequest[] Lista =
        {
            // Ejemplo (quitar las // para activarla):
            // new PrendaRequest { Nombre = " Chamarra CI/CD", CategoriaId = 4, MarcaId = 4, TallaId = 3, ColorId = 5, GeneroId = 3, Precio = 499.99m, Stock = 10 },
        };

        public static async Task AplicarAsync(AppDbContext db)
        {
            foreach (var r in Lista)
            {
                if (await db.Prendas.AnyAsync(p => p.Nombre == r.Nombre))
                    continue;
                // Una prenda inválida detiene el arranque: las pruebas del pipeline fallan y no se despliega
                var error = await PrendaReglas.ValidarAsync(db, r);
                if (error != null)
                    throw new InvalidOperationException($"Prenda de demo '{r.Nombre}' inválida: {error}");
                var prenda = new Prenda { FechaCreacion = DateTime.UtcNow };
                PrendaReglas.AplicarCambios(prenda, r);
                db.Prendas.Add(prenda);
            }
            await db.SaveChangesAsync();
        }
    }
}

using System.Net;
using System.Net.Http.Json;

namespace WebApp.Tests
{
    public class ProductosTests : PruebaApi
    {
        public ProductosTests(ApiFactory factory) : base(factory) { }

        private static object Producto(string? nombre = null, decimal precio = 19.99m, int stock = 5, bool disponible = true) =>
            new { nombre = nombre ?? $"Producto {Unico()}", descripcion = "Descripción", precio, stock, disponible };

        private async Task<int> Crear(string? nombre = null, int stock = 5, bool disponible = true)
        {
            var r = await Post("/api/productos", Producto(nombre, stock: stock, disponible: disponible));
            Assert.Equal(HttpStatusCode.Created, r.Status);
            return r.Data.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Crear_devuelve_201_con_location_y_se_puede_obtener()
        {
            using var res = await Client.PostAsync("/api/productos", JsonContent.Create(Producto()));
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            Assert.Contains("/api/productos/", res.Headers.Location!.ToString());

            var r = await Get(res.Headers.Location.ToString());
            Assert.Equal(HttpStatusCode.OK, r.Status);
        }

        [Fact]
        public async Task Listar_incluye_el_creado()
        {
            var id = await Crear();
            var r = await Get("/api/productos");
            Assert.Contains(r.Data.EnumerateArray(), p => p.GetProperty("id").GetInt32() == id);
        }

        [Fact]
        public async Task Obtener_inexistente_devuelve_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/productos/99999")).Status);
        }

        [Fact]
        public async Task Crear_sin_nombre_devuelve_400()
        {
            var r = await Post("/api/productos", new { nombre = "  ", descripcion = "d", precio = 1, stock = 1 });
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }

        [Theory]
        [InlineData(-1, 1, "negativo")]
        [InlineData(1.999, 1, "decimales")]
        [InlineData(1, -5, "stock")]
        public async Task Crear_con_datos_invalidos_devuelve_400(decimal precio, int stock, string mensaje)
        {
            var r = await Post("/api/productos", Producto(precio: precio, stock: stock));
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.Contains(mensaje, r.Message);
        }

        [Fact]
        public async Task Actualizar_cambia_los_datos()
        {
            var id = await Crear();
            var r = await Put($"/api/productos/{id}", Producto("Editado", 5.5m, 2, false));
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("Editado", r.Data.GetProperty("nombre").GetString());
            Assert.Equal(5.5m, r.Data.GetProperty("precio").GetDecimal());
        }

        [Fact]
        public async Task Actualizar_inexistente_devuelve_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Put("/api/productos/99999", Producto())).Status);
        }

        [Fact]
        public async Task Actualizar_con_precio_negativo_devuelve_400()
        {
            var id = await Crear();
            Assert.Equal(HttpStatusCode.BadRequest, (await Put($"/api/productos/{id}", Producto(precio: -3))).Status);
        }

        [Fact]
        public async Task Eliminar_y_despues_no_existe()
        {
            var id = await Crear();
            Assert.Equal(HttpStatusCode.OK, (await Delete($"/api/productos/{id}")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Delete($"/api/productos/{id}")).Status);
        }

        [Fact]
        public async Task Buscar_por_nombre()
        {
            var marca = Unico();
            await Crear($"Laptop {marca}");
            var r = await Get($"/api/productos/buscar?nombre={marca}");
            Assert.Equal(1, r.Data.GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/productos/buscar")).Status);
        }

        [Fact]
        public async Task Disponibles_excluye_sin_stock_y_no_disponibles()
        {
            var conStock = await Crear();
            var sinStock = await Crear(stock: 0);
            var noDisponible = await Crear(disponible: false);
            var ids = (await Get("/api/productos/disponibles")).Data.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
            Assert.Contains(conStock, ids);
            Assert.DoesNotContain(sinStock, ids);
            Assert.DoesNotContain(noDisponible, ids);
        }

        [Fact]
        public async Task Patch_stock()
        {
            var id = await Crear();
            var r = await Patch($"/api/productos/{id}/stock", new { stock = 42 });
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal(42, r.Data.GetProperty("stock").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await Patch($"/api/productos/{id}/stock", new { stock = -1 })).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Patch("/api/productos/99999/stock", new { stock = 1 })).Status);
        }
    }
}

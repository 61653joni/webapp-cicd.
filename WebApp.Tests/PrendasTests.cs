using System.Net;

namespace WebApp.Tests
{
    public class PrendasTests : PruebaApi
    {
        public PrendasTests(ApiFactory factory) : base(factory) { }

        // Body válido de una prenda; se puede sobrescribir cualquier campo
        private static Dictionary<string, object> Prenda(params (string campo, object valor)[] cambios)
        {
            var body = new Dictionary<string, object>
            {
                ["nombre"] = $"Prenda {Unico()}",
                ["categoriaId"] = 1, ["marcaId"] = 1, ["tallaId"] = 1, ["colorId"] = 1, ["generoId"] = 1,
                ["precio"] = 25.50m, ["stock"] = 10, ["disponible"] = true
            };
            foreach (var (campo, valor) in cambios) body[campo] = valor;
            return body;
        }

        private async Task<int> Crear(params (string, object)[] cambios)
        {
            var r = await Post("/api/prendas", Prenda(cambios));
            Assert.Equal(HttpStatusCode.Created, r.Status);
            return r.Data.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Listar_devuelve_las_prendas_iniciales_con_sus_catalogos()
        {
            var r = await Get("/api/prendas");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.True(r.Data.GetArrayLength() >= 10);
            var primera = r.Data[0];
            Assert.Equal("Camiseta", primera.GetProperty("categoria").GetProperty("nombre").GetString());
        }

        [Fact]
        public async Task Filtrar_por_todos_los_catalogos()
        {
            var r = await Get("/api/prendas?categoria=Camiseta&talla=M&genero=Unisex&color=Blanco&marca=Zara");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Contains(r.Data.EnumerateArray(), p => p.GetProperty("id").GetInt32() == 1);
            Assert.All(r.Data.EnumerateArray(), p => Assert.Equal("Zara", p.GetProperty("marca").GetProperty("nombre").GetString()));
        }

        [Fact]
        public async Task Filtro_sin_coincidencias_devuelve_lista_vacia()
        {
            var r = await Get("/api/prendas?marca=NoExiste");
            Assert.Equal(0, r.Data.GetArrayLength());
        }

        [Fact]
        public async Task Obtener_por_id()
        {
            var r = await Get("/api/prendas/2");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("Jeans slim fit", r.Data.GetProperty("nombre").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/prendas/99999")).Status);
        }

        [Fact]
        public async Task Crear_devuelve_la_prenda_con_catalogos()
        {
            var r = await Post("/api/prendas", Prenda(("nombre", "  Polo nuevo  "), ("marcaId", 4)));
            Assert.Equal(HttpStatusCode.Created, r.Status);
            Assert.Equal("Polo nuevo", r.Data.GetProperty("nombre").GetString());
            Assert.Equal("Nike", r.Data.GetProperty("marca").GetProperty("nombre").GetString());
        }

        public static IEnumerable<object[]> DatosInvalidos() => new[]
        {
            new object[] { "nombre", "" },
            new object[] { "nombre", new string('x', 101) },
            new object[] { "precio", -1m },
            new object[] { "precio", 1.999m },
            new object[] { "stock", -1 },
            new object[] { "categoriaId", 999 },
            new object[] { "marcaId", 999 },
            new object[] { "tallaId", 999 },
            new object[] { "colorId", 999 },
            new object[] { "generoId", 999 }
        };

        [Theory]
        [MemberData(nameof(DatosInvalidos))]
        public async Task Crear_con_datos_invalidos_devuelve_400(string campo, object valor)
        {
            var r = await Post("/api/prendas", Prenda((campo, valor)));
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }

        [Fact]
        public async Task Actualizar_cambia_los_datos()
        {
            var id = await Crear();
            var r = await Put($"/api/prendas/{id}", Prenda(("nombre", "Editada"), ("colorId", 5), ("precio", 9.99m)));
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("Editada", r.Data.GetProperty("nombre").GetString());
            Assert.Equal("Negro", r.Data.GetProperty("color").GetProperty("nombre").GetString());
        }

        [Fact]
        public async Task Actualizar_inexistente_o_invalida()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Put("/api/prendas/99999", Prenda())).Status);
            var id = await Crear();
            Assert.Equal(HttpStatusCode.BadRequest, (await Put($"/api/prendas/{id}", Prenda(("stock", -3)))).Status);
        }

        [Fact]
        public async Task Eliminar_y_despues_no_existe()
        {
            var id = await Crear();
            Assert.Equal(HttpStatusCode.OK, (await Delete($"/api/prendas/{id}")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Delete($"/api/prendas/{id}")).Status);
        }

        [Fact]
        public async Task Buscar_por_nombre_sin_distinguir_mayusculas()
        {
            var r = await Get("/api/prendas/buscar?nombre=CAMISETA");
            Assert.Contains(r.Data.EnumerateArray(), p => p.GetProperty("id").GetInt32() == 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/prendas/buscar?nombre=%20")).Status);
        }

        [Fact]
        public async Task Disponibles_y_agotadas()
        {
            var agotada = await Crear(("stock", 0));
            var disponibles = (await Get("/api/prendas/disponibles")).Data.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
            var agotadas = (await Get("/api/prendas/agotadas")).Data.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

            Assert.Contains(1, disponibles);
            Assert.DoesNotContain(10, disponibles);   // Abrigo de lana: stock 0
            Assert.DoesNotContain(agotada, disponibles);
            Assert.Contains(10, agotadas);
            Assert.Contains(agotada, agotadas);
        }

        [Fact]
        public async Task Count_y_estadisticas()
        {
            var count = (await Get("/api/prendas/count")).Data.GetInt32();
            Assert.True(count >= 10);

            var r = await Get("/api/prendas/estadisticas");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.True(r.Data.GetProperty("totalPrendas").GetInt32() >= 10);
            Assert.True(r.Data.GetProperty("valorInventario").GetDecimal() > 0);
            Assert.True(r.Data.GetProperty("precioMinimo").GetDecimal() <= r.Data.GetProperty("precioMaximo").GetDecimal());
        }

        [Fact]
        public async Task Patch_stock()
        {
            var id = await Crear();
            var r = await Patch($"/api/prendas/{id}/stock", new { stock = 3 });
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal(3, r.Data.GetProperty("stock").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await Patch($"/api/prendas/{id}/stock", new { stock = -1 })).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Patch("/api/prendas/99999/stock", new { stock = 1 })).Status);
        }
    }
}

using System.Net;
using System.Text.Json;

namespace WebApp.Tests
{
    public class SistemaTests : PruebaApi
    {
        public SistemaTests(ApiFactory factory) : base(factory) { }

        [Fact]
        public async Task Health_responde_OK()
        {
            var r = await Get("/api/health");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("OK", r.Data.GetString());
        }

        [Fact]
        public async Task Info_devuelve_version_y_entorno()
        {
            var r = await Get("/api/info");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.False(string.IsNullOrEmpty(r.Message));
            Assert.False(string.IsNullOrEmpty(r.Data.GetProperty("version").GetString()));
            Assert.False(string.IsNullOrEmpty(r.Data.GetProperty("servidor").GetString()));
        }

        [Fact]
        public async Task Estadisticas_cuenta_registros_de_cada_tabla()
        {
            var r = await Get("/api/estadisticas");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.True(r.Data.GetProperty("prendas").GetInt32() >= 10);
            Assert.Equal(3, r.Data.GetProperty("generos").GetInt32());
            foreach (var tabla in new[] { "usuarios", "productos", "categorias", "marcas", "tallas", "colores" })
                Assert.True(r.Data.TryGetProperty(tabla, out _), tabla);
        }

        [Fact]
        public async Task La_API_expone_al_menos_60_endpoints()
        {
            var json = await Client.GetStringAsync("/swagger/v1/swagger.json");
            using var doc = JsonDocument.Parse(json);
            var verbos = new[] { "get", "post", "put", "patch", "delete" };
            int total = doc.RootElement.GetProperty("paths").EnumerateObject()
                .Sum(p => p.Value.EnumerateObject().Count(m => verbos.Contains(m.Name)));
            Assert.True(total >= 60, $"Solo hay {total} endpoints");
        }

        [Fact]
        public async Task JSON_mal_escrito_devuelve_400_con_el_esquema_de_la_API()
        {
            var r = await PostRaw("/api/prendas", "{ esto no es json");
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.Equal(400, r.Json.GetProperty("statusCode").GetInt32());
        }

        [Fact]
        public async Task Tipo_de_dato_incorrecto_indica_el_campo()
        {
            var r = await PostRaw("/api/prendas", "{\"nombre\":\"X\",\"precio\":\"abc\"}");
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.Contains("precio", r.Message);
        }

        [Fact]
        public async Task Backup_se_crea_se_lista_y_se_descarga()
        {
            var creado = await Post("/api/backup", new { });
            Assert.Equal(HttpStatusCode.OK, creado.Status);
            var archivo = creado.Data.GetString()!;
            Assert.StartsWith("backup_", archivo);

            var lista = await Get("/api/backup");
            Assert.Contains(lista.Data.EnumerateArray(), a => a.GetString() == archivo);

            using var descarga = await Client.GetAsync($"/api/backup/{archivo}");
            Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
            Assert.Equal("application/vnd.sqlite3", descarga.Content.Headers.ContentType!.MediaType);
            Assert.True((await descarga.Content.ReadAsByteArrayAsync()).Length > 0);
        }

        [Fact]
        public async Task Backup_descargar_crea_y_devuelve_el_archivo()
        {
            using var res = await Client.GetAsync("/api/backup/descargar");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.StartsWith("backup_", res.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        }

        [Theory]
        [InlineData("otro.db")]
        [InlineData("backup_x.txt")]
        public async Task Backup_con_nombre_invalido_devuelve_400(string archivo)
        {
            var r = await Get($"/api/backup/{archivo}");
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }

        [Fact]
        public async Task Backup_inexistente_devuelve_404()
        {
            var r = await Get("/api/backup/backup_19990101_000000_000.db");
            Assert.Equal(HttpStatusCode.NotFound, r.Status);
        }
    }

    // Va en su propia clase (y por tanto su propia BD) porque vacía todas las tablas
    public class LimpiarTests : PruebaApi
    {
        public LimpiarTests(ApiFactory factory) : base(factory) { }

        [Fact]
        public async Task Limpiar_hace_backup_y_vacia_todas_las_tablas()
        {
            await Post("/api/usuarios", new { nombre = "A", email = "a@b.com", telefono = "1", activo = true });

            var r = await Delete("/api/limpiar");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.StartsWith("backup_", r.Data.GetProperty("backup").GetString());
            Assert.Equal(10, r.Data.GetProperty("eliminados").GetProperty("prendas").GetInt32());

            var stats = await Get("/api/estadisticas");
            foreach (var tabla in stats.Data.EnumerateObject())
                Assert.Equal(0, tabla.Value.GetInt32());

            // Con el inventario vacío las estadísticas devuelven ceros
            var inventario = await Get("/api/prendas/estadisticas");
            Assert.Equal(0, inventario.Data.GetProperty("totalPrendas").GetInt32());
            Assert.Equal(0m, inventario.Data.GetProperty("precioPromedio").GetDecimal());
        }
    }
}

using System.Net;

namespace WebApp.Tests
{
    // Las mismas pruebas se ejecutan para los 5 catálogos
    public class CatalogosTests : PruebaApi
    {
        public CatalogosTests(ApiFactory factory) : base(factory) { }

        public static IEnumerable<object[]> Catalogos() => new[]
        {
            new object[] { "categorias", "categoriaId" },
            new object[] { "marcas", "marcaId" },
            new object[] { "tallas", "tallaId" },
            new object[] { "colores", "colorId" },
            new object[] { "generos", "generoId" }
        };

        private async Task<int> Crear(string catalogo, string nombre)
        {
            var r = await Post($"/api/{catalogo}", new { nombre });
            Assert.Equal(HttpStatusCode.Created, r.Status);
            return r.Data.GetProperty("id").GetInt32();
        }

        [Theory, MemberData(nameof(Catalogos))]
        public async Task Listar_y_obtener_por_id(string catalogo, string _)
        {
            var lista = await Get($"/api/{catalogo}");
            Assert.Equal(HttpStatusCode.OK, lista.Status);
            Assert.True(lista.Data.GetArrayLength() >= 3);

            var uno = await Get($"/api/{catalogo}/1");
            Assert.Equal(HttpStatusCode.OK, uno.Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/{catalogo}/99999")).Status);
        }

        [Theory, MemberData(nameof(Catalogos))]
        public async Task Crear_valida_nombre_requerido_largo_y_duplicado(string catalogo, string _)
        {
            var nombre = $"Nuevo {Unico()}";
            var r = await Post($"/api/{catalogo}", new { nombre = $"  {nombre}  " });
            Assert.Equal(HttpStatusCode.Created, r.Status);
            Assert.Equal(nombre, r.Data.GetProperty("nombre").GetString());

            Assert.Equal(HttpStatusCode.Conflict, (await Post($"/api/{catalogo}", new { nombre })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post($"/api/{catalogo}", new { nombre = "   " })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post($"/api/{catalogo}", new { nombre = new string('x', 51) })).Status);
        }

        [Theory, MemberData(nameof(Catalogos))]
        public async Task Actualizar_renombra_y_valida(string catalogo, string _)
        {
            var id = await Crear(catalogo, $"Viejo {Unico()}");
            var otro = $"Otro {Unico()}";
            await Crear(catalogo, otro);

            var nuevo = $"Renombrado {Unico()}";
            var r = await Put($"/api/{catalogo}/{id}", new { nombre = nuevo });
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal(nuevo, (await Get($"/api/{catalogo}/{id}")).Data.GetProperty("nombre").GetString());

            Assert.Equal(HttpStatusCode.NotFound, (await Put($"/api/{catalogo}/99999", new { nombre = "X" })).Status);
            Assert.Equal(HttpStatusCode.Conflict, (await Put($"/api/{catalogo}/{id}", new { nombre = otro })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put($"/api/{catalogo}/{id}", new { nombre = " " })).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put($"/api/{catalogo}/{id}", new { nombre = new string('x', 51) })).Status);
        }

        [Theory, MemberData(nameof(Catalogos))]
        public async Task Eliminar_libre_ok_en_uso_409(string catalogo, string campoPrenda)
        {
            var libre = await Crear(catalogo, $"Libre {Unico()}");
            Assert.Equal(HttpStatusCode.OK, (await Delete($"/api/{catalogo}/{libre}")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Delete($"/api/{catalogo}/{libre}")).Status);

            // Un valor usado por una prenda no se puede borrar (clave foránea)
            var enUso = await Crear(catalogo, $"EnUso {Unico()}");
            var prenda = new Dictionary<string, object>
            {
                ["nombre"] = "Prenda FK", ["categoriaId"] = 1, ["marcaId"] = 1, ["tallaId"] = 1,
                ["colorId"] = 1, ["generoId"] = 1, ["precio"] = 10, ["stock"] = 1, ["disponible"] = true
            };
            prenda[campoPrenda] = enUso;
            Assert.Equal(HttpStatusCode.Created, (await Post("/api/prendas", prenda)).Status);
            Assert.Equal(HttpStatusCode.Conflict, (await Delete($"/api/{catalogo}/{enUso}")).Status);

            // Y la prenda aparece en /{id}/prendas
            var prendas = await Get($"/api/{catalogo}/{enUso}/prendas");
            Assert.Equal(HttpStatusCode.OK, prendas.Status);
            Assert.Equal(1, prendas.Data.GetArrayLength());
            Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/{catalogo}/99999/prendas")).Status);
        }

        [Theory, MemberData(nameof(Catalogos))]
        public async Task Buscar_y_contar(string catalogo, string _)
        {
            var marca = Unico();
            await Crear(catalogo, $"Busca {marca}");
            var r = await Get($"/api/{catalogo}/buscar?nombre={marca.ToUpper()}");
            Assert.Equal(1, r.Data.GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await Get($"/api/{catalogo}/buscar")).Status);

            var total = (await Get($"/api/{catalogo}/count")).Data.GetInt32();
            Assert.Equal((await Get($"/api/{catalogo}")).Data.GetArrayLength(), total);
        }
    }
}

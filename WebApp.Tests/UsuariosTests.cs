using System.Net;

namespace WebApp.Tests
{
    public class UsuariosTests : PruebaApi
    {
        public UsuariosTests(ApiFactory factory) : base(factory) { }

        private static object Usuario(string? email = null, string nombre = "Usuario Test", bool activo = true) =>
            new { nombre, email = email ?? $"u{Unico()}@test.com", telefono = "5551234567", activo };

        private async Task<int> Crear(string? email = null, string nombre = "Usuario Test", bool activo = true)
        {
            var r = await Post("/api/usuarios", Usuario(email, nombre, activo));
            Assert.Equal(HttpStatusCode.Created, r.Status);
            return r.Data.GetProperty("id").GetInt32();
        }

        [Fact]
        public async Task Crear_y_obtener_por_id()
        {
            var email = $"ana{Unico()}@test.com";
            var id = await Crear(email, "Ana");
            var r = await Get($"/api/usuarios/{id}");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal(email, r.Data.GetProperty("email").GetString());
        }

        [Fact]
        public async Task Listar_incluye_el_usuario_creado()
        {
            var id = await Crear();
            var r = await Get("/api/usuarios");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Contains(r.Data.EnumerateArray(), u => u.GetProperty("id").GetInt32() == id);
        }

        [Fact]
        public async Task Obtener_inexistente_devuelve_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/usuarios/99999")).Status);
        }

        [Theory]
        [InlineData("")]
        [InlineData("sin-arroba")]
        [InlineData("a@b")]
        public async Task Crear_con_email_invalido_devuelve_400(string email)
        {
            var r = await Post("/api/usuarios", new { nombre = "X", email, telefono = "1", activo = true });
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }

        [Fact]
        public async Task Crear_con_email_duplicado_devuelve_409()
        {
            var email = $"dup{Unico()}@test.com";
            await Crear(email);
            Assert.Equal(HttpStatusCode.Conflict, (await Post("/api/usuarios", Usuario(email))).Status);
        }

        [Fact]
        public async Task Actualizar_cambia_los_datos()
        {
            var id = await Crear();
            var nuevoEmail = $"nuevo{Unico()}@test.com";
            var r = await Put($"/api/usuarios/{id}", new { nombre = "Editado", email = nuevoEmail, telefono = "999", activo = false });
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("Editado", r.Data.GetProperty("nombre").GetString());
            Assert.Equal(nuevoEmail, r.Data.GetProperty("email").GetString());
            Assert.False(r.Data.GetProperty("activo").GetBoolean());
        }

        [Fact]
        public async Task Actualizar_inexistente_devuelve_404()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Put("/api/usuarios/99999", Usuario())).Status);
        }

        [Fact]
        public async Task Actualizar_con_email_invalido_devuelve_400()
        {
            var id = await Crear();
            Assert.Equal(HttpStatusCode.BadRequest, (await Put($"/api/usuarios/{id}", Usuario("malo"))).Status);
        }

        [Fact]
        public async Task Actualizar_con_email_de_otro_usuario_devuelve_409()
        {
            var email = $"otro{Unico()}@test.com";
            await Crear(email);
            var id = await Crear();
            Assert.Equal(HttpStatusCode.Conflict, (await Put($"/api/usuarios/{id}", Usuario(email))).Status);
        }

        [Fact]
        public async Task Eliminar_y_despues_no_existe()
        {
            var id = await Crear();
            Assert.Equal(HttpStatusCode.OK, (await Delete($"/api/usuarios/{id}")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/usuarios/{id}")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Delete($"/api/usuarios/{id}")).Status);
        }

        [Fact]
        public async Task Buscar_por_nombre_o_email()
        {
            var marca = Unico();
            await Crear($"{marca}@test.com", "Pedro");
            await Crear(nombre: $"Nombre {marca}");
            var r = await Get($"/api/usuarios/buscar?q={marca.ToUpper()}");
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal(2, r.Data.GetArrayLength());
        }

        [Fact]
        public async Task Buscar_sin_texto_devuelve_400()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/usuarios/buscar?q=")).Status);
        }

        [Fact]
        public async Task Activar_y_desactivar_cambia_la_lista_de_activos()
        {
            var id = await Crear();

            var des = await Patch($"/api/usuarios/{id}/desactivar");
            Assert.Equal(HttpStatusCode.OK, des.Status);
            Assert.False(des.Data.GetProperty("activo").GetBoolean());
            var activos = await Get("/api/usuarios/activos");
            Assert.DoesNotContain(activos.Data.EnumerateArray(), u => u.GetProperty("id").GetInt32() == id);

            var act = await Patch($"/api/usuarios/{id}/activar");
            Assert.True(act.Data.GetProperty("activo").GetBoolean());
            activos = await Get("/api/usuarios/activos");
            Assert.Contains(activos.Data.EnumerateArray(), u => u.GetProperty("id").GetInt32() == id);
        }

        [Theory]
        [InlineData("activar")]
        [InlineData("desactivar")]
        public async Task Cambiar_estado_de_inexistente_devuelve_404(string accion)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Patch($"/api/usuarios/99999/{accion}")).Status);
        }
    }
}

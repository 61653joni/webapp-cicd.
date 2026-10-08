using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WebApp.Tests
{
    // Pruebas del servidor socket TCP: un comando por conexión, la respuesta es JSON
    public class SocketTests : PruebaApi
    {
        public SocketTests(ApiFactory factory) : base(factory) { }

        private async Task<JsonElement> Enviar(string comando)
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync("127.0.0.1", Factory.SocketPort);
            var stream = cliente.GetStream();
            await stream.WriteAsync(Encoding.UTF8.GetBytes(comando));
            using var lector = new StreamReader(stream, Encoding.UTF8);
            var texto = await lector.ReadToEndAsync();
            return JsonDocument.Parse(texto).RootElement.Clone();
        }

        private static int Codigo(JsonElement r) => r.GetProperty("statusCode").GetInt32();

        [Theory]
        [InlineData("prendas")]
        [InlineData("categorias")]
        [InlineData("marcas")]
        [InlineData("tallas")]
        [InlineData("colores")]
        [InlineData("generos")]
        [InlineData("productos")]
        [InlineData("usuarios")]
        public async Task Get_todos_de_cada_tipo(string tipo)
        {
            var r = await Enviar($"{{get:{tipo}}}");
            Assert.Equal(200, Codigo(r));
            Assert.Equal(JsonValueKind.Array, r.GetProperty("data").ValueKind);
        }

        [Fact]
        public async Task Get_por_id()
        {
            var r = await Enviar("{get:prendas:1}");
            Assert.Equal(200, Codigo(r));
            Assert.Equal("Camiseta básica", r.GetProperty("data").GetProperty("nombre").GetString());
            Assert.Equal(404, Codigo(await Enviar("{get:prendas:99999}")));
        }

        [Theory]
        [InlineData("{get:prendas:abc}")]          // id no numérico
        [InlineData("{get:}")]                      // falta el tipo
        [InlineData("{get:zapatos}")]               // tipo inexistente
        [InlineData("{borrar:prendas}")]            // acción inexistente
        [InlineData("{sinseparador}")]              // sin ':'
        [InlineData("hola")]                        // sin llaves
        [InlineData("{insert:abc}")]                // insert sin json ni tipo
        [InlineData("{insert:zapatos:{}}")]         // insert de tipo inexistente
        [InlineData("{insert:{no es json}}")]       // JSON inválido
        [InlineData("{insert:null}")]               // body vacío
        public async Task Comandos_invalidos_devuelven_400(string comando)
        {
            Assert.Equal(400, Codigo(await Enviar(comando)));
        }

        [Fact]
        public async Task Insert_prenda_valida_y_invalida()
        {
            var json = "{\"nombre\":\"Prenda socket\",\"categoriaId\":1,\"marcaId\":1,\"tallaId\":1,\"colorId\":1,\"generoId\":1,\"precio\":10,\"stock\":2}";
            var r = await Enviar($"{{insert:{json}}}");
            Assert.Equal(201, Codigo(r));
            Assert.Equal("Zara", r.GetProperty("data").GetProperty("marca").GetProperty("nombre").GetString());

            Assert.Equal(400, Codigo(await Enviar("{insert:{\"nombre\":\"X\",\"categoriaId\":999}}")));
        }

        [Theory]
        [InlineData("categorias")]
        [InlineData("marcas")]
        [InlineData("tallas")]
        [InlineData("colores")]
        [InlineData("generos")]
        public async Task Insert_catalogo(string tipo)
        {
            var nombre = $"Socket {Guid.NewGuid():N}"[..20];
            Assert.Equal(201, Codigo(await Enviar($"{{insert:{tipo}:{{\"nombre\":\"{nombre}\"}}}}")));
            Assert.Equal(409, Codigo(await Enviar($"{{insert:{tipo}:{{\"nombre\":\"{nombre}\"}}}}")));
            Assert.Equal(400, Codigo(await Enviar($"{{insert:{tipo}:{{\"nombre\":\"\"}}}}")));
        }

        [Fact]
        public async Task Insert_producto()
        {
            Assert.Equal(201, Codigo(await Enviar("{insert:productos:{\"nombre\":\"Producto socket\",\"precio\":5}}")));
            Assert.Equal(400, Codigo(await Enviar("{insert:productos:{\"precio\":5}}")));
        }

        [Fact]
        public async Task Insert_usuario()
        {
            var email = $"sock{Guid.NewGuid():N}@test.com";
            Assert.Equal(201, Codigo(await Enviar($"{{insert:usuarios:{{\"email\":\"{email}\"}}}}")));
            Assert.Equal(409, Codigo(await Enviar($"{{insert:usuarios:{{\"email\":\"{email}\"}}}}")));
            Assert.Equal(400, Codigo(await Enviar("{insert:usuarios:{\"nombre\":\"Sin email\"}}")));
        }
    }
}

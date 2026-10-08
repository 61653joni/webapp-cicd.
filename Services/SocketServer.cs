using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;

namespace WebApp.Services
{
    // Servidor socket TCP (puerto 6061). Un comando por conexión:
    //   {insert:<json>}              -> inserta una prenda (mismo body que POST /api/prendas)
    //   {insert:<tipo>:<json>}       -> inserta en otra tabla (marcas, categorias, tallas, colores, generos, productos, usuarios)
    //   {get:<tipo>}                 -> obtiene todos los registros (ej. {get:prendas})
    //   {get:<tipo>:<id>}            -> obtiene un registro por id (ej. {get:prendas:1})
    public class SocketServer
    {
        public const int PuertoPorDefecto = 6061;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        private TcpListener? _listener;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SocketServer> _logger;

        // Puerto TCP (configurable con SocketPort; las pruebas usan uno libre)
        public int Puerto { get; }

        // SocketServer es singleton y AppDbContext es scoped: se crea un scope por comando
        public SocketServer(IServiceScopeFactory scopeFactory, ILogger<SocketServer> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            Puerto = configuration.GetValue("SocketPort", PuertoPorDefecto);
        }

        // Acepta conexiones hasta que se cancela el token (al detener la app / el contenedor)
        public async Task StartAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, Puerto);
                _listener.Start();
                _logger.LogInformation("Socket server iniciado en puerto {Puerto}", Puerto);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                // Parada normal
            }
            catch (Exception ex)
            {
                _logger.LogError("Error en socket server: {Mensaje}", ex.Message);
            }
            finally
            {
                _listener?.Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var request = await LeerComandoAsync(stream);
                    if (string.IsNullOrWhiteSpace(request))
                        return;

                    _logger.LogInformation("Socket {Cliente} -> {Comando}", client.Client.RemoteEndPoint, request);
                    string response = await ProcessCommandAsync(request);

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                    await stream.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error al procesar cliente socket: {Mensaje}", ex.Message);
            }
        }

        // Lee hasta tener un comando completo {...} (llaves balanceadas), aunque llegue en varios paquetes.
        // Si el comando está mal cerrado, se procesa lo recibido tras 1 s sin datos nuevos.
        private static async Task<string> LeerComandoAsync(NetworkStream stream)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var decoder = Encoding.UTF8.GetDecoder();
            var bytes = new byte[4096];
            var chars = new char[4096];
            var sb = new StringBuilder();

            while (true)
            {
                int leidos = await stream.ReadAsync(bytes, timeout.Token);
                if (leidos == 0) break;
                int n = decoder.GetChars(bytes, 0, leidos, chars, 0);
                sb.Append(chars, 0, n);
                if (ComandoCompleto(sb) || !await HayMasDatosAsync(stream)) break;
            }
            return sb.ToString().Trim();
        }

        private static async Task<bool> HayMasDatosAsync(NetworkStream stream)
        {
            for (int i = 0; i < 20; i++)
            {
                if (stream.DataAvailable) return true;
                await Task.Delay(50);
            }
            return false;
        }

        private static bool ComandoCompleto(StringBuilder sb)
        {
            int nivel = 0;
            bool enTexto = false, empezado = false;
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (enTexto)
                {
                    if (c == '\\') i++;
                    else if (c == '"') enTexto = false;
                }
                else if (c == '"') enTexto = true;
                else if (c == '{') { nivel++; empezado = true; }
                else if (c == '}') nivel--;
            }
            return empezado && nivel <= 0;
        }

        private async Task<string> ProcessCommandAsync(string command)
        {
            try
            {
                if (!command.StartsWith('{') || !command.EndsWith('}'))
                    return Respuesta(400, "Formato inválido. Use {insert:<json>} o {get:<tipo>[:id]}");

                string contenido = command[1..^1].Trim();
                int sep = contenido.IndexOf(':');
                if (sep < 0)
                    return Respuesta(400, "Formato inválido. Use {insert:<json>} o {get:<tipo>[:id]}");

                string accion = contenido[..sep].Trim().ToLowerInvariant();
                string elemento = contenido[(sep + 1)..].Trim();

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                return accion switch
                {
                    "insert" => await HandleInsertAsync(elemento, context),
                    "get" => await HandleGetAsync(elemento, context),
                    _ => Respuesta(400, $"Acción '{accion}' no válida. Use insert o get")
                };
            }
            catch (JsonException ex)
            {
                return Respuesta(400, $"JSON inválido: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error procesando comando: {Mensaje}", ex.Message);
                return Respuesta(500, $"Error interno: {ex.Message}");
            }
        }

        // ---------- INSERT ----------

        private async Task<string> HandleInsertAsync(string elemento, AppDbContext context)
        {
            // {insert:{...}} -> prenda   |   {insert:marcas:{...}} -> otra tabla
            string tipo = "prendas";
            string json = elemento;
            if (!elemento.StartsWith('{'))
            {
                int sep = elemento.IndexOf(':');
                if (sep < 0)
                    return Respuesta(400, "Formato inválido. Use {insert:<json>} o {insert:<tipo>:<json>}");
                tipo = elemento[..sep].Trim().ToLowerInvariant();
                json = elemento[(sep + 1)..].Trim();
            }

            return tipo switch
            {
                "prendas" => await InsertPrendaAsync(json, context),
                "categorias" => await InsertCatalogoAsync<Categoria>(json, context),
                "marcas" => await InsertCatalogoAsync<Marca>(json, context),
                "tallas" => await InsertCatalogoAsync<Talla>(json, context),
                "colores" => await InsertCatalogoAsync<Color>(json, context),
                "generos" => await InsertCatalogoAsync<Genero>(json, context),
                "productos" => await InsertProductoAsync(json, context),
                "usuarios" => await InsertUsuarioAsync(json, context),
                _ => Respuesta(400, $"Tipo '{tipo}' no válido")
            };
        }

        private async Task<string> InsertPrendaAsync(string json, AppDbContext context)
        {
            var request = JsonSerializer.Deserialize<PrendaRequest>(json, JsonOptions);
            if (request == null)
                return Respuesta(400, "Body vacío");

            var error = await PrendaReglas.ValidarAsync(context, request);
            if (error != null)
                return Respuesta(400, error);

            var prenda = new Prenda { FechaCreacion = DateTime.UtcNow };
            PrendaReglas.AplicarCambios(prenda, request);
            context.Prendas.Add(prenda);
            await context.SaveChangesAsync();

            var creada = await PrendaReglas.ConCatalogos(context).FirstAsync(p => p.Id == prenda.Id);
            _logger.LogInformation("Prenda insertada por socket con ID {Id}", prenda.Id);
            return Respuesta(201, "Prenda insertada correctamente", creada);
        }

        private static async Task<string> InsertCatalogoAsync<T>(string json, AppDbContext context) where T : Catalogo
        {
            var item = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (item == null || string.IsNullOrWhiteSpace(item.Nombre))
                return Respuesta(400, "Nombre requerido");
            item.Id = 0;
            item.Nombre = item.Nombre.Trim();
            if (await context.Set<T>().AnyAsync(c => c.Nombre == item.Nombre))
                return Respuesta(409, $"Ya existe '{item.Nombre}'");
            context.Set<T>().Add(item);
            await context.SaveChangesAsync();
            return Respuesta(201, "Registro insertado correctamente", item);
        }

        private static async Task<string> InsertProductoAsync(string json, AppDbContext context)
        {
            var producto = JsonSerializer.Deserialize<Producto>(json, JsonOptions);
            if (producto == null || string.IsNullOrWhiteSpace(producto.Nombre))
                return Respuesta(400, "Nombre requerido");
            producto.Id = 0;
            producto.Descripcion ??= string.Empty;
            context.Productos.Add(producto);
            await context.SaveChangesAsync();
            return Respuesta(201, "Producto insertado correctamente", producto);
        }

        private static async Task<string> InsertUsuarioAsync(string json, AppDbContext context)
        {
            var usuario = JsonSerializer.Deserialize<Usuario>(json, JsonOptions);
            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
                return Respuesta(400, "Email requerido");
            if (await context.Usuarios.AnyAsync(u => u.Email == usuario.Email))
                return Respuesta(409, $"Ya existe un usuario con email '{usuario.Email}'");
            usuario.Id = 0;
            usuario.Nombre ??= string.Empty;
            usuario.Telefono ??= string.Empty;
            context.Usuarios.Add(usuario);
            await context.SaveChangesAsync();
            return Respuesta(201, "Usuario insertado correctamente", usuario);
        }

        // ---------- GET ----------

        private static async Task<string> HandleGetAsync(string elemento, AppDbContext context)
        {
            // {get:prendas} -> todos   |   {get:prendas:1} -> uno
            var partes = elemento.Split(':', 2, StringSplitOptions.TrimEntries);
            string tipo = partes[0].ToLowerInvariant();
            int? id = null;
            if (partes.Length > 1)
            {
                if (!int.TryParse(partes[1], out int valor))
                    return Respuesta(400, $"ID inválido: '{partes[1]}'");
                id = valor;
            }

            return tipo switch
            {
                "prendas" => await GetAsync(PrendaReglas.ConCatalogos(context), id, tipo),
                "categorias" => await GetAsync(context.Categorias, id, tipo),
                "marcas" => await GetAsync(context.Marcas, id, tipo),
                "tallas" => await GetAsync(context.Tallas, id, tipo),
                "colores" => await GetAsync(context.Colores, id, tipo),
                "generos" => await GetAsync(context.Generos, id, tipo),
                "productos" => await GetAsync(context.Productos, id, tipo),
                "usuarios" => await GetAsync(context.Usuarios, id, tipo),
                "" => Respuesta(400, "Falta el tipo. Ej: {get:prendas} o {get:prendas:1}"),
                _ => Respuesta(400, $"Tipo '{tipo}' no válido")
            };
        }

        private static async Task<string> GetAsync<T>(IQueryable<T> query, int? id, string tipo) where T : class
        {
            if (id == null)
            {
                var todos = await query.OrderBy(e => EF.Property<int>(e, "Id")).ToListAsync();
                return Respuesta(200, $"{todos.Count} registros obtenidos de {tipo}", todos);
            }

            var item = await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
            return item == null
                ? Respuesta(404, $"No existe ningún registro con id {id} en {tipo}")
                : Respuesta(200, $"Registro {id} de {tipo} obtenido", item);
        }

        private static string Respuesta(int statusCode, string message, object? data = null) =>
            JsonSerializer.Serialize(new { statusCode, message, data }, JsonOptions);
    }
}

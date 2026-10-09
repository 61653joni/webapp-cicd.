using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;
using System.Text.Json;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ApiController> _logger;

        public ApiController(AppDbContext context, ILogger<ApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("health")]
        public ActionResult<ApiResponse<string>> HealthCheck()
        {
            return Ok(ApiResponse<string>.Success("OK", "API funcionando working"));
        }

        // GET api/info -> datos de la versión desplegada (útil para comprobar cada despliegue)
        [HttpGet("info")]
        public ActionResult<ApiResponse<object>> Info()
        {
            var info = new
            {
                nombre = "WebApp - API de tienda de ropa",
                version = typeof(ApiController).Assembly.GetName().Version?.ToString(),
                entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                servidor = Environment.MachineName,
                fechaUtc = DateTime.UtcNow
            };
            return Ok(ApiResponse<object>.Success(info, "Bienvenido a la API de la tienda - desplegada con CI/CD"));
        }

        // GET api/estadisticas -> número de registros de cada tabla
        [HttpGet("estadisticas")]
        public async Task<ActionResult<ApiResponse<Dictionary<string, int>>>> Estadisticas()
        {
            var conteos = new Dictionary<string, int>
            {
                ["usuarios"] = await _context.Usuarios.CountAsync(),
                ["productos"] = await _context.Productos.CountAsync(),
                ["prendas"] = await _context.Prendas.CountAsync(),
                ["categorias"] = await _context.Categorias.CountAsync(),
                ["marcas"] = await _context.Marcas.CountAsync(),
                ["tallas"] = await _context.Tallas.CountAsync(),
                ["colores"] = await _context.Colores.CountAsync(),
                ["generos"] = await _context.Generos.CountAsync()
            };
            return Ok(ApiResponse<Dictionary<string, int>>.Success(conteos, "Registros por tabla"));
        }

        // GET api/usuarios/buscar?q=ana -> busca en nombre o email
        [HttpGet("usuarios/buscar")]
        public async Task<ActionResult<ApiResponse<List<Usuario>>>> BuscarUsuarios([FromQuery] string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(ApiResponse<List<Usuario>>.Error(400, "Parámetro 'q' requerido"));
            var texto = q.Trim().ToLower();
            var usuarios = await _context.Usuarios
                .Where(u => (u.Nombre != null && u.Nombre.ToLower().Contains(texto)) || u.Email.ToLower().Contains(texto))
                .ToListAsync();
            return Ok(ApiResponse<List<Usuario>>.Success(usuarios, $"{usuarios.Count} usuarios encontrados"));
        }

        // GET api/usuarios/activos
        [HttpGet("usuarios/activos")]
        public async Task<ActionResult<ApiResponse<List<Usuario>>>> GetUsuariosActivos()
        {
            var usuarios = await _context.Usuarios.Where(u => u.Activo).ToListAsync();
            return Ok(ApiResponse<List<Usuario>>.Success(usuarios, "Usuarios activos"));
        }

        // PATCH api/usuarios/{id}/activar
        [HttpPatch("usuarios/{id}/activar")]
        public Task<ActionResult<ApiResponse<Usuario>>> ActivarUsuario(int id) => CambiarEstadoUsuario(id, true);

        // PATCH api/usuarios/{id}/desactivar
        [HttpPatch("usuarios/{id}/desactivar")]
        public Task<ActionResult<ApiResponse<Usuario>>> DesactivarUsuario(int id) => CambiarEstadoUsuario(id, false);

        [HttpGet("usuarios")]
        public async Task<ActionResult<ApiResponse<List<Usuario>>>> GetUsuarios()
        {
            try
            {
                var usuarios = await _context.Usuarios.ToListAsync();
                return Ok(ApiResponse<List<Usuario>>.Success(usuarios, "Usuarios obtenidos correctamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return StatusCode(500, ApiResponse<List<Usuario>>.Error(500, "Error al obtener usuarios"));
            }
        }

        [HttpGet("usuarios/{id}")]
        public async Task<ActionResult<ApiResponse<Usuario>>> GetUsuarioById(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(ApiResponse<Usuario>.Error(404, "Usuario no encontrado"));
            return Ok(ApiResponse<Usuario>.Success(usuario, "Usuario obtenido"));
        }

        [HttpPost("usuarios")]
        public async Task<ActionResult<ApiResponse<Usuario>>> CreateUsuario([FromBody] Usuario usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario.Email))
                return BadRequest(ApiResponse<Usuario>.Error(400, "Email requerido"));
            if (!EmailValido(usuario.Email))
                return BadRequest(ApiResponse<Usuario>.Error(400, "Email con formato inválido"));
            if (await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email))
                return Conflict(ApiResponse<Usuario>.Error(409, $"Ya existe un usuario con email '{usuario.Email}'"));
            usuario.Id = 0;
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsuarioById), new { id = usuario.Id }, ApiResponse<Usuario>.Success(usuario, "Usuario creado"));
        }

        [HttpPut("usuarios/{id}")]
        public async Task<ActionResult<ApiResponse<Usuario>>> UpdateUsuario(int id, [FromBody] Usuario usuarioActualizado)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(ApiResponse<Usuario>.Error(404, "Usuario no encontrado"));
            if (!string.IsNullOrWhiteSpace(usuarioActualizado.Email) && !EmailValido(usuarioActualizado.Email))
                return BadRequest(ApiResponse<Usuario>.Error(400, "Email con formato inválido"));
            if (await _context.Usuarios.AnyAsync(u => u.Email == usuarioActualizado.Email && u.Id != id))
                return Conflict(ApiResponse<Usuario>.Error(409, $"Ya existe un usuario con email '{usuarioActualizado.Email}'"));
            usuario.Nombre = usuarioActualizado.Nombre ?? usuario.Nombre;
            usuario.Email = usuarioActualizado.Email ?? usuario.Email;
            usuario.Telefono = usuarioActualizado.Telefono ?? usuario.Telefono;
            usuario.Activo = usuarioActualizado.Activo;
            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<Usuario>.Success(usuario, "Usuario actualizado"));
        }

        [HttpDelete("usuarios/{id}")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(ApiResponse<string>.Error(404, "Usuario no encontrado"));
            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.Success($"Usuario {id} eliminado", "Éxito"));
        }

        [HttpGet("productos")]
        public async Task<ActionResult<ApiResponse<List<Producto>>>> GetProductos()
        {
            var productos = await _context.Productos.ToListAsync();
            return Ok(ApiResponse<List<Producto>>.Success(productos, "Productos obtenidos"));
        }

        [HttpGet("productos/{id}")]
        public async Task<ActionResult<ApiResponse<Producto>>> GetProductoById(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(ApiResponse<Producto>.Error(404, "Producto no encontrado"));
            return Ok(ApiResponse<Producto>.Success(producto, "Producto obtenido"));
        }

        // GET api/productos/buscar?nombre=lap
        [HttpGet("productos/buscar")]
        public async Task<ActionResult<ApiResponse<List<Producto>>>> BuscarProductos([FromQuery] string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest(ApiResponse<List<Producto>>.Error(400, "Parámetro 'nombre' requerido"));
            var texto = nombre.Trim().ToLower();
            var productos = await _context.Productos.Where(p => p.Nombre.ToLower().Contains(texto)).ToListAsync();
            return Ok(ApiResponse<List<Producto>>.Success(productos, $"{productos.Count} productos encontrados"));
        }

        // GET api/productos/disponibles -> disponibles y con stock
        [HttpGet("productos/disponibles")]
        public async Task<ActionResult<ApiResponse<List<Producto>>>> GetProductosDisponibles()
        {
            var productos = await _context.Productos.Where(p => p.Disponible && p.Stock > 0).ToListAsync();
            return Ok(ApiResponse<List<Producto>>.Success(productos, "Productos disponibles"));
        }

        // PATCH api/productos/{id}/stock  body: { "stock": 10 }
        [HttpPatch("productos/{id}/stock")]
        public async Task<ActionResult<ApiResponse<Producto>>> UpdateStockProducto(int id, [FromBody] StockRequest request)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(ApiResponse<Producto>.Error(404, "Producto no encontrado"));
            if (request.Stock < 0)
                return BadRequest(ApiResponse<Producto>.Error(400, "El stock no puede ser negativo"));
            producto.Stock = request.Stock;
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<Producto>.Success(producto, "Stock actualizado"));
        }

        [HttpPost("productos")]
        public async Task<ActionResult<ApiResponse<Producto>>> CreateProducto([FromBody] Producto producto)
        {
            if (string.IsNullOrWhiteSpace(producto.Nombre))
                return BadRequest(ApiResponse<Producto>.Error(400, "Nombre requerido"));
            var error = ValidarProducto(producto);
            if (error != null)
                return BadRequest(ApiResponse<Producto>.Error(400, error));
            producto.Id = 0;
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetProductoById), new { id = producto.Id }, ApiResponse<Producto>.Success(producto, "Producto creado"));
        }

        [HttpPut("productos/{id}")]
        public async Task<ActionResult<ApiResponse<Producto>>> UpdateProducto(int id, [FromBody] Producto productoActualizado)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(ApiResponse<Producto>.Error(404, "Producto no encontrado"));
            var error = ValidarProducto(productoActualizado);
            if (error != null)
                return BadRequest(ApiResponse<Producto>.Error(400, error));
            producto.Nombre = productoActualizado.Nombre ?? producto.Nombre;
            producto.Descripcion = productoActualizado.Descripcion ?? producto.Descripcion;
            producto.Precio = productoActualizado.Precio;
            producto.Stock = productoActualizado.Stock;
            producto.Disponible = productoActualizado.Disponible;
            _context.Productos.Update(producto);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<Producto>.Success(producto, "Producto actualizado"));
        }

        [HttpDelete("productos/{id}")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(ApiResponse<string>.Error(404, "Producto no encontrado"));
            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.Success($"Producto {id} eliminado", "Éxito"));
        }

        // POST api/backup -> copia completa de la BD SQLite (todas las tablas) a un archivo .db
        [HttpPost("backup")]
        public async Task<ActionResult<ApiResponse<string>>> BackupDatabase()
        {
            var fileName = await CrearBackupAsync();
            return Ok(ApiResponse<string>.Success(fileName, "Backup creado"));
        }

        // GET api/backup/descargar -> crea un backup y lo descarga directamente (se puede abrir en el navegador)
        [HttpGet("backup/descargar")]
        public async Task<IActionResult> BackupYDescargar()
        {
            var fileName = await CrearBackupAsync();
            return PhysicalFile(Path.Combine(CarpetaBackups(), fileName), "application/vnd.sqlite3", fileName);
        }

        // GET api/backup -> lista los backups disponibles
        [HttpGet("backup")]
        public ActionResult<ApiResponse<List<string>>> ListarBackups()
        {
            var dir = CarpetaBackups();
            var archivos = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "backup_*.db").Select(Path.GetFileName).OrderDescending().ToList()
                : new List<string?>();
            return Ok(ApiResponse<List<string>>.Success(archivos!, $"{archivos.Count} backups"));
        }

        // GET api/backup/{archivo} -> descarga un backup
        [HttpGet("backup/{archivo}")]
        public IActionResult DescargarBackup(string archivo)
        {
            if (Path.GetFileName(archivo) != archivo || !archivo.StartsWith("backup_") || !archivo.EndsWith(".db"))
                return BadRequest(ApiResponse<string>.Error(400, "Nombre de archivo inválido"));
            var path = Path.Combine(CarpetaBackups(), archivo);
            if (!System.IO.File.Exists(path))
                return NotFound(ApiResponse<string>.Error(404, "Backup no encontrado"));
            return PhysicalFile(path, "application/vnd.sqlite3", archivo);
        }

        // DELETE api/limpiar -> hace un backup y después vacía todas las tablas
        [HttpDelete("limpiar")]
        public async Task<ActionResult<ApiResponse<object>>> LimpiarDatabase()
        {
            var backup = await CrearBackupAsync();

            await using var tx = await _context.Database.BeginTransactionAsync();
            // Primero la tabla hija (Prendas) y después los catálogos, por las claves foráneas
            var eliminados = new Dictionary<string, int>
            {
                ["prendas"] = await _context.Prendas.ExecuteDeleteAsync(),
                ["categorias"] = await _context.Categorias.ExecuteDeleteAsync(),
                ["marcas"] = await _context.Marcas.ExecuteDeleteAsync(),
                ["tallas"] = await _context.Tallas.ExecuteDeleteAsync(),
                ["colores"] = await _context.Colores.ExecuteDeleteAsync(),
                ["generos"] = await _context.Generos.ExecuteDeleteAsync(),
                ["productos"] = await _context.Productos.ExecuteDeleteAsync(),
                ["usuarios"] = await _context.Usuarios.ExecuteDeleteAsync()
            };
            // Reinicia los contadores de Id (AUTOINCREMENT)
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence");
            await tx.CommitAsync();

            return Ok(ApiResponse<object>.Success(new { backup, eliminados }, "BD vaciada"));
        }

        private async Task<ActionResult<ApiResponse<Usuario>>> CambiarEstadoUsuario(int id, bool activo)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(ApiResponse<Usuario>.Error(404, "Usuario no encontrado"));
            usuario.Activo = activo;
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<Usuario>.Success(usuario, activo ? "Usuario activado" : "Usuario desactivado"));
        }

        private static bool EmailValido(string email) =>
            System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private static string? ValidarProducto(Producto p)
        {
            if (p.Precio < 0) return "El precio no puede ser negativo";
            if (decimal.Round(p.Precio, 2) != p.Precio) return "El precio no puede tener más de 2 decimales";
            if (p.Stock < 0) return "El stock no puede ser negativo";
            return null;
        }

        private string CarpetaBackups()
        {
            // Los backups se guardan junto al archivo de la BD (en Docker: /app/data/backups, dentro del volumen)
            var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(_context.Database.GetConnectionString()).DataSource;
            var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource))!;
            return Path.Combine(dir, "backups");
        }

        private async Task<string> CrearBackupAsync()
        {
            var dir = CarpetaBackups();
            Directory.CreateDirectory(dir);
            var fileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.db";
            var path = Path.Combine(dir, fileName).Replace("'", "''");
            // VACUUM INTO genera una copia consistente de la BD aunque esté en uso
            var sql = $"VACUUM INTO '{path}'";
            await _context.Database.ExecuteSqlRawAsync(sql);
            _logger.LogInformation("Backup creado: {Archivo}", fileName);
            return fileName;
        }
    }
}

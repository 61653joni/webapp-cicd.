using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("api/prendas")]
    public class PrendasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PrendasController(AppDbContext context)
        {
            _context = context;
        }

        // Prendas con sus catálogos (JOIN)
        private IQueryable<Prenda> PrendasConCatalogos() => PrendaReglas.ConCatalogos(_context);

        // GET api/prendas?categoria=Camiseta&talla=M&genero=Mujer&color=Negro&marca=Nike
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Prenda>>>> GetPrendas(
            [FromQuery] string? categoria, [FromQuery] string? talla, [FromQuery] string? genero,
            [FromQuery] string? color, [FromQuery] string? marca)
        {
            var query = PrendasConCatalogos();
            if (!string.IsNullOrWhiteSpace(categoria)) query = query.Where(p => p.Categoria!.Nombre == categoria);
            if (!string.IsNullOrWhiteSpace(talla)) query = query.Where(p => p.Talla!.Nombre == talla);
            if (!string.IsNullOrWhiteSpace(genero)) query = query.Where(p => p.Genero!.Nombre == genero);
            if (!string.IsNullOrWhiteSpace(color)) query = query.Where(p => p.Color!.Nombre == color);
            if (!string.IsNullOrWhiteSpace(marca)) query = query.Where(p => p.Marca!.Nombre == marca);

            var prendas = await query.OrderBy(p => p.Id).ToListAsync();
            return Ok(ApiResponse<List<Prenda>>.Success(prendas, "Prendas obtenidas"));
        }

        // GET api/prendas/buscar?nombre=camis -> búsqueda parcial por nombre
        [HttpGet("buscar")]
        public async Task<ActionResult<ApiResponse<List<Prenda>>>> Buscar([FromQuery] string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest(ApiResponse<List<Prenda>>.Error(400, "Parámetro 'nombre' requerido"));
            var texto = nombre.Trim().ToLower();
            var prendas = await PrendasConCatalogos().Where(p => p.Nombre.ToLower().Contains(texto)).OrderBy(p => p.Id).ToListAsync();
            return Ok(ApiResponse<List<Prenda>>.Success(prendas, $"{prendas.Count} prendas encontradas"));
        }

        // GET api/prendas/disponibles -> prendas a la venta (disponibles y con stock)
        [HttpGet("disponibles")]
        public async Task<ActionResult<ApiResponse<List<Prenda>>>> GetDisponibles()
        {
            var prendas = await PrendasConCatalogos().Where(p => p.Disponible && p.Stock > 0).OrderBy(p => p.Id).ToListAsync();
            return Ok(ApiResponse<List<Prenda>>.Success(prendas, "Prendas disponibles"));
        }

        // GET api/prendas/agotadas -> prendas sin stock
        [HttpGet("agotadas")]
        public async Task<ActionResult<ApiResponse<List<Prenda>>>> GetAgotadas()
        {
            var prendas = await PrendasConCatalogos().Where(p => p.Stock == 0).OrderBy(p => p.Id).ToListAsync();
            return Ok(ApiResponse<List<Prenda>>.Success(prendas, "Prendas agotadas"));
        }

        // GET api/prendas/count
        [HttpGet("count")]
        public async Task<ActionResult<ApiResponse<int>>> Count()
        {
            var total = await _context.Prendas.CountAsync();
            return Ok(ApiResponse<int>.Success(total, "Total de prendas"));
        }

        // GET api/prendas/estadisticas -> resumen del inventario
        [HttpGet("estadisticas")]
        public async Task<ActionResult<ApiResponse<object>>> Estadisticas()
        {
            // SQLite no agrega decimales en el servidor: se traen precio y stock y se calcula en memoria
            var datos = await _context.Prendas.Select(p => new { p.Precio, p.Stock }).ToListAsync();
            var resumen = new
            {
                totalPrendas = datos.Count,
                unidadesEnStock = datos.Sum(d => d.Stock),
                valorInventario = datos.Sum(d => d.Precio * d.Stock),
                precioPromedio = datos.Count > 0 ? decimal.Round(datos.Average(d => d.Precio), 2) : 0m,
                precioMinimo = datos.Count > 0 ? datos.Min(d => d.Precio) : 0m,
                precioMaximo = datos.Count > 0 ? datos.Max(d => d.Precio) : 0m
            };
            return Ok(ApiResponse<object>.Success(resumen, "Estadísticas del inventario"));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Prenda>>> GetPrendaById(int id)
        {
            var prenda = await PrendasConCatalogos().FirstOrDefaultAsync(p => p.Id == id);
            if (prenda == null)
                return NotFound(ApiResponse<Prenda>.Error(404, "Prenda no encontrada"));
            return Ok(ApiResponse<Prenda>.Success(prenda, "Prenda obtenida"));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<Prenda>>> CreatePrenda([FromBody] PrendaRequest request)
        {
            var error = await ValidarAsync(request);
            if (error != null)
                return BadRequest(ApiResponse<Prenda>.Error(400, error));

            var prenda = new Prenda { FechaCreacion = DateTime.UtcNow };
            AplicarCambios(prenda, request);
            _context.Prendas.Add(prenda);
            await _context.SaveChangesAsync();

            var creada = await PrendasConCatalogos().FirstAsync(p => p.Id == prenda.Id);
            return CreatedAtAction(nameof(GetPrendaById), new { id = prenda.Id }, ApiResponse<Prenda>.Success(creada, "Prenda creada"));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<Prenda>>> UpdatePrenda(int id, [FromBody] PrendaRequest request)
        {
            var prenda = await _context.Prendas.FindAsync(id);
            if (prenda == null)
                return NotFound(ApiResponse<Prenda>.Error(404, "Prenda no encontrada"));
            var error = await ValidarAsync(request);
            if (error != null)
                return BadRequest(ApiResponse<Prenda>.Error(400, error));

            AplicarCambios(prenda, request);
            await _context.SaveChangesAsync();

            var actualizada = await PrendasConCatalogos().FirstAsync(p => p.Id == id);
            return Ok(ApiResponse<Prenda>.Success(actualizada, "Prenda actualizada"));
        }

        // PATCH api/prendas/{id}/stock  body: { "stock": 10 } -> solo cambia el stock
        [HttpPatch("{id}/stock")]
        public async Task<ActionResult<ApiResponse<Prenda>>> UpdateStock(int id, [FromBody] StockRequest request)
        {
            var prenda = await _context.Prendas.FindAsync(id);
            if (prenda == null)
                return NotFound(ApiResponse<Prenda>.Error(404, "Prenda no encontrada"));
            if (request.Stock < 0)
                return BadRequest(ApiResponse<Prenda>.Error(400, "El stock no puede ser negativo"));
            prenda.Stock = request.Stock;
            await _context.SaveChangesAsync();

            var actualizada = await PrendasConCatalogos().FirstAsync(p => p.Id == id);
            return Ok(ApiResponse<Prenda>.Success(actualizada, "Stock actualizado"));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> DeletePrenda(int id)
        {
            var prenda = await _context.Prendas.FindAsync(id);
            if (prenda == null)
                return NotFound(ApiResponse<string>.Error(404, "Prenda no encontrada"));
            _context.Prendas.Remove(prenda);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.Success($"Prenda {id} eliminada", "Éxito"));
        }

        private Task<string?> ValidarAsync(PrendaRequest r) => PrendaReglas.ValidarAsync(_context, r);

        private static void AplicarCambios(Prenda prenda, PrendaRequest r) => PrendaReglas.AplicarCambios(prenda, r);
    }
}

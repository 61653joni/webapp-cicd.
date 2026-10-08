using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers
{
    // CRUD común para las tablas de catálogo (categorías, marcas, tallas, colores, géneros)
    [ApiController]
    public abstract class CatalogoController<T> : ControllerBase where T : Catalogo, new()
    {
        protected readonly AppDbContext _context;

        protected CatalogoController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<T>>>> GetAll()
        {
            var items = await _context.Set<T>().OrderBy(c => c.Id).ToListAsync();
            return Ok(ApiResponse<List<T>>.Success(items, "Registros obtenidos"));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<T>>> GetById(int id)
        {
            var item = await _context.Set<T>().FindAsync(id);
            if (item == null)
                return NotFound(ApiResponse<T>.Error(404, "Registro no encontrado"));
            return Ok(ApiResponse<T>.Success(item, "Registro obtenido"));
        }

        // GET api/{catalogo}/buscar?nombre=ca -> búsqueda parcial por nombre
        [HttpGet("buscar")]
        public async Task<ActionResult<ApiResponse<List<T>>>> Buscar([FromQuery] string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest(ApiResponse<List<T>>.Error(400, "Parámetro 'nombre' requerido"));
            var texto = nombre.Trim().ToLower();
            var items = await _context.Set<T>().Where(c => c.Nombre.ToLower().Contains(texto)).OrderBy(c => c.Id).ToListAsync();
            return Ok(ApiResponse<List<T>>.Success(items, $"{items.Count} registros encontrados"));
        }

        // GET api/{catalogo}/count -> número de registros
        [HttpGet("count")]
        public async Task<ActionResult<ApiResponse<int>>> Count()
        {
            var total = await _context.Set<T>().CountAsync();
            return Ok(ApiResponse<int>.Success(total, "Total de registros"));
        }

        // GET api/{catalogo}/{id}/prendas -> prendas que usan este valor
        [HttpGet("{id}/prendas")]
        public async Task<ActionResult<ApiResponse<List<Prenda>>>> GetPrendas(int id)
        {
            if (!await _context.Set<T>().AnyAsync(c => c.Id == id))
                return NotFound(ApiResponse<List<Prenda>>.Error(404, "Registro no encontrado"));
            var prendas = await FiltrarPrendas(PrendaReglas.ConCatalogos(_context), id).OrderBy(p => p.Id).ToListAsync();
            return Ok(ApiResponse<List<Prenda>>.Success(prendas, $"{prendas.Count} prendas"));
        }

        // Cada catálogo indica por qué clave foránea se filtran las prendas
        protected abstract IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id);

        [HttpPost]
        public async Task<ActionResult<ApiResponse<T>>> Create([FromBody] T item)
        {
            if (string.IsNullOrWhiteSpace(item.Nombre))
                return BadRequest(ApiResponse<T>.Error(400, "Nombre requerido"));
            item.Nombre = item.Nombre.Trim();
            if (item.Nombre.Length > 50)
                return BadRequest(ApiResponse<T>.Error(400, "El nombre no puede tener más de 50 caracteres"));
            if (await _context.Set<T>().AnyAsync(c => c.Nombre == item.Nombre))
                return Conflict(ApiResponse<T>.Error(409, $"Ya existe '{item.Nombre}'"));
            item.Id = 0;
            _context.Set<T>().Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, ApiResponse<T>.Success(item, "Registro creado"));
        }

        // PUT api/{catalogo}/{id} -> renombra el valor (las prendas lo ven al instante porque usan el Id)
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<T>>> Update(int id, [FromBody] T cambios)
        {
            var item = await _context.Set<T>().FindAsync(id);
            if (item == null)
                return NotFound(ApiResponse<T>.Error(404, "Registro no encontrado"));
            if (string.IsNullOrWhiteSpace(cambios.Nombre))
                return BadRequest(ApiResponse<T>.Error(400, "Nombre requerido"));
            var nombre = cambios.Nombre.Trim();
            if (nombre.Length > 50)
                return BadRequest(ApiResponse<T>.Error(400, "El nombre no puede tener más de 50 caracteres"));
            if (await _context.Set<T>().AnyAsync(c => c.Nombre == nombre && c.Id != id))
                return Conflict(ApiResponse<T>.Error(409, $"Ya existe '{nombre}'"));
            item.Nombre = nombre;
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<T>.Success(item, "Registro actualizado"));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> Delete(int id)
        {
            var item = await _context.Set<T>().FindAsync(id);
            if (item == null)
                return NotFound(ApiResponse<string>.Error(404, "Registro no encontrado"));
            try
            {
                _context.Set<T>().Remove(item);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Clave foránea: hay prendas que usan este valor
                return Conflict(ApiResponse<string>.Error(409, $"No se puede eliminar '{item.Nombre}': hay prendas que lo usan"));
            }
            return Ok(ApiResponse<string>.Success($"Registro {id} eliminado", "Éxito"));
        }
    }

    [Route("api/categorias")]
    public class CategoriasController : CatalogoController<Categoria>
    {
        public CategoriasController(AppDbContext context) : base(context) { }
        protected override IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id) => prendas.Where(p => p.CategoriaId == id);
    }

    [Route("api/marcas")]
    public class MarcasController : CatalogoController<Marca>
    {
        public MarcasController(AppDbContext context) : base(context) { }
        protected override IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id) => prendas.Where(p => p.MarcaId == id);
    }

    [Route("api/tallas")]
    public class TallasController : CatalogoController<Talla>
    {
        public TallasController(AppDbContext context) : base(context) { }
        protected override IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id) => prendas.Where(p => p.TallaId == id);
    }

    [Route("api/colores")]
    public class ColoresController : CatalogoController<Color>
    {
        public ColoresController(AppDbContext context) : base(context) { }
        protected override IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id) => prendas.Where(p => p.ColorId == id);
    }

    [Route("api/generos")]
    public class GenerosController : CatalogoController<Genero>
    {
        public GenerosController(AppDbContext context) : base(context) { }
        protected override IQueryable<Prenda> FiltrarPrendas(IQueryable<Prenda> prendas, int id) => prendas.Where(p => p.GeneroId == id);
    }
}

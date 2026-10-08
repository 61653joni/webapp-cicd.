namespace WebApp.Models
{
    public class Prenda
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public bool Disponible { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Claves foráneas a las tablas de catálogo
        public int CategoriaId { get; set; }
        public int MarcaId { get; set; }
        public int TallaId { get; set; }
        public int ColorId { get; set; }
        public int GeneroId { get; set; }

        public Categoria? Categoria { get; set; }
        public Marca? Marca { get; set; }
        public Talla? Talla { get; set; }
        public Color? Color { get; set; }
        public Genero? Genero { get; set; }
    }

    // Body que se envía en POST/PUT: solo los Ids de los catálogos
    public class PrendaRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public int CategoriaId { get; set; }
        public int MarcaId { get; set; }
        public int TallaId { get; set; }
        public int ColorId { get; set; }
        public int GeneroId { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public bool Disponible { get; set; } = true;
    }
}

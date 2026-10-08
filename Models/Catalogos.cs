namespace WebApp.Models
{
    // Tablas de catálogo: cada valor se guarda una sola vez y las prendas lo referencian por Id
    public abstract class Catalogo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class Categoria : Catalogo { }   // Camiseta, Pantalón, Vestido...
    public class Marca : Catalogo { }       // Nike, Zara, Levi's...
    public class Talla : Catalogo { }       // XS, S, M, L, XL, 42...
    public class Color : Catalogo { }       // Negro, Blanco, Azul...
    public class Genero : Catalogo { }      // Hombre, Mujer, Unisex
}

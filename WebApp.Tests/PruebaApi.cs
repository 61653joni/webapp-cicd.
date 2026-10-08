using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace WebApp.Tests
{
    // Respuesta de la API ya leída: código HTTP + JSON { statusCode, message, data }
    public record Respuesta(HttpStatusCode Status, JsonElement Json)
    {
        public JsonElement Data => Json.GetProperty("data");
        public string Message => Json.GetProperty("message").GetString()!;
    }

    // Base de las clases de prueba: un HttpClient contra la API en memoria y atajos por verbo
    public abstract class PruebaApi : IClassFixture<ApiFactory>
    {
        protected readonly ApiFactory Factory;
        protected readonly HttpClient Client;

        protected PruebaApi(ApiFactory factory)
        {
            Factory = factory;
            Client = factory.CreateClient();
        }

        protected Task<Respuesta> Get(string url) => Enviar(Client.GetAsync(url));
        protected Task<Respuesta> Post(string url, object body) => Enviar(Client.PostAsJsonAsync(url, body));
        protected Task<Respuesta> Put(string url, object body) => Enviar(Client.PutAsJsonAsync(url, body));
        protected Task<Respuesta> Patch(string url, object? body = null) =>
            Enviar(Client.PatchAsync(url, body == null ? null : JsonContent.Create(body)));
        protected Task<Respuesta> Delete(string url) => Enviar(Client.DeleteAsync(url));

        // Envía texto tal cual (para probar JSON mal escrito)
        protected Task<Respuesta> PostRaw(string url, string texto) =>
            Enviar(Client.PostAsync(url, new StringContent(texto, Encoding.UTF8, "application/json")));

        protected static string Unico() => Guid.NewGuid().ToString("N")[..8];

        private static async Task<Respuesta> Enviar(Task<HttpResponseMessage> peticion)
        {
            using var res = await peticion;
            var texto = await res.Content.ReadAsStringAsync();
            var json = string.IsNullOrEmpty(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
            return new Respuesta(res.StatusCode, json);
        }
    }
}

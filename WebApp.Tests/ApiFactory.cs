using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Data;

namespace WebApp.Tests
{
    // Levanta la API completa en memoria. Cada instancia usa su propia BD SQLite temporal
    // y su propio puerto para el servidor socket, así las clases de prueba no se pisan.
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "webapp-tests-" + Guid.NewGuid().ToString("N"));

        public int SocketPort { get; } = PuertoLibre();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_carpeta);
            builder.UseEnvironment("Testing");
            builder.UseSetting("SocketPort", SocketPort.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(_carpeta, "test.db")}"));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_carpeta, true); } catch (IOException) { }
        }

        private static int PuertoLibre()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int puerto = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return puerto;
        }
    }

    internal static class ServiceCollectionExtensions
    {
        public static void RemoveAll<T>(this IServiceCollection services)
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
                services.Remove(d);
        }
    }
}

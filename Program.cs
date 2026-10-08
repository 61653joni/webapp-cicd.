using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Errores de formato (letras en un número, decimales en un entero, JSON mal escrito...)
        // con el mismo esquema JSON que el resto de la API: statusCode, message, data
        options.InvalidModelStateResponseFactory = context =>
        {
            var campos = context.ModelState
                .Where(e => e.Value!.Errors.Count > 0 && e.Key != "request" && e.Key != "$")
                .Select(e => e.Key.TrimStart('$', '.'))
                .Where(k => k.Length > 0)
                .Distinct()
                .ToList();
            var mensaje = campos.Count > 0
                ? "Datos con formato inválido en: " + string.Join(", ", campos)
                : "El cuerpo de la petición no es un JSON válido";
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(ApiResponse<List<string>>.Error(400, mensaje));
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Socket Server
builder.Services.AddSingleton<SocketServer>();
builder.Services.AddHostedService<SocketServerHostedService>();

var app = builder.Build();

// Create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await PrendasDemo.AplicarAsync(db);
}

// Swagger activo también en Docker/AWS para poder probar la API igual que en local
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Visible para las pruebas de integración (WebApplicationFactory<Program>)
public partial class Program { }

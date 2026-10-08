# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos del proyecto
COPY ["WebApp.csproj", "./"]
RUN dotnet restore "WebApp.csproj"

# Copiar código fuente
COPY . .
RUN dotnet build "WebApp.csproj" -c Release -o /app/build

# Etapa 2: Publish
FROM build AS publish
RUN dotnet publish "WebApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copiar archivos publicados
COPY --from=publish /app/publish .

# Crear carpetas para backups y para la base de datos SQLite (montar /app/data como volumen para no perder datos)
RUN mkdir -p /app/backups /app/data

# Exponer puertos
EXPOSE 80
EXPOSE 6061

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Default="Data Source=/app/data/app.db"

# Health check (la imagen aspnet no trae curl; se usa bash /dev/tcp)
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/80 && printf "GET /api/health HTTP/1.0\r\n\r\n" >&3 && grep -q "200 OK" <&3' || exit 1

# Comando de inicio
ENTRYPOINT ["dotnet", "WebApp.dll"]
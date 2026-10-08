# WebApp – API REST con pipeline CI/CD

API REST de una tienda de ropa (ASP.NET Core 8 + SQLite) con integración y despliegue continuo:
cada `git push` a `main` ejecuta las pruebas, publica la imagen en Docker Hub y actualiza el servidor en AWS EC2.

- **API pública:** `http://<IP_EC2>/api/health` (Swagger en `http://<IP_EC2>/swagger`)
- **Imagen:** [`joni41/webapp`](https://hub.docker.com/r/joni41/webapp) con tags `latest` y `<sha del commit>`
- **Pipeline:** [.github/workflows/main.yml](.github/workflows/main.yml)

## Arquitectura

```
 Desarrollador ──git push──▶ GitHub (main)
                               │
                               ▼
                      GitHub Actions (main.yml)
            ┌──────────────────┼──────────────────────┐
            ▼                  ▼                      ▼
   1. Pruebas y cobertura  2. Build y push        3. Despliegue
      dotnet test (xUnit)     docker build           SSH a EC2
      coverlet ≥ 70 %         :latest  :<sha>        docker pull / rm / run
                               │                      │
                               ▼                      ▼
                          Docker Hub ──pull──▶ AWS EC2 (Ubuntu + Docker)
                                               contenedor "webapp"
                                               puerto 80 → API REST
                                               puerto 6061 → socket TCP
                                               volumen webapp-data-v2 → SQLite
```

| Componente | Tecnología |
| --- | --- |
| API | ASP.NET Core 8, Entity Framework Core, SQLite, Swagger |
| Pruebas | xUnit, `WebApplicationFactory`, coverlet (umbral 70 % de líneas) |
| Contenedor | Dockerfile multi-stage (`sdk:8.0` → `aspnet:8.0`), healthcheck en `/api/health` |
| CI/CD | GitHub Actions: test → Docker Hub → deploy por SSH |
| Nube | AWS EC2 Ubuntu Server, Security Group con puertos 22 y 80 |

## Endpoints (76)

Todas las respuestas usan el esquema `{ "statusCode", "message", "data" }`.

| Grupo | Rutas |
| --- | --- |
| Sistema | `GET /api/health`, `GET /api/info`, `GET /api/estadisticas` |
| Prendas | `GET/POST /api/prendas`, `GET/PUT/DELETE /api/prendas/{id}`, `PATCH /api/prendas/{id}/stock`, `GET /api/prendas/buscar?nombre=`, `/disponibles`, `/agotadas`, `/count`, `/estadisticas` |
| Catálogos (`categorias`, `marcas`, `tallas`, `colores`, `generos`) | `GET/POST /api/{catalogo}`, `GET/PUT/DELETE /api/{catalogo}/{id}`, `GET /api/{catalogo}/{id}/prendas`, `/buscar?nombre=`, `/count` |
| Usuarios | `GET/POST /api/usuarios`, `GET/PUT/DELETE /api/usuarios/{id}`, `PATCH /api/usuarios/{id}/activar`, `/desactivar`, `GET /api/usuarios/buscar?q=`, `/activos` |
| Productos | `GET/POST /api/productos`, `GET/PUT/DELETE /api/productos/{id}`, `PATCH /api/productos/{id}/stock`, `GET /api/productos/buscar?nombre=`, `/disponibles` |
| Respaldos | `POST /api/backup`, `GET /api/backup`, `GET /api/backup/descargar`, `GET /api/backup/{archivo}`, `DELETE /api/limpiar` |

Servidor socket TCP (puerto 6061), un comando por conexión: `{get:prendas}`, `{get:prendas:1}`, `{insert:<json>}`, `{insert:marcas:<json>}`.

## Comandos locales

Requisitos: .NET SDK 8 y Docker.

```bash
# Ejecutar la API (http://localhost:5136/swagger)
dotnet run

# Pruebas con reporte de cobertura (falla si baja de 70 %)
dotnet test WebApp.Tests/WebApp.Tests.csproj

# Imagen Docker local
docker build -t webapp .
docker run -d --name webapp -p 8080:80 -v webapp-data:/app/data webapp
curl http://localhost:8080/api/health
```

## Configuración del pipeline

1. **Docker Hub:** crear un Personal Access Token (Account settings → Personal access tokens) con permiso *Read & Write*.
2. **AWS EC2:** instancia Ubuntu Server con Docker instalado. Security Group con entrada **SSH (22)** y **HTTP (80)** desde `0.0.0.0/0`.
3. **GitHub Secrets** (Settings → Secrets and variables → Actions):

   | Secret | Valor |
   | --- | --- |
   | `DOCKERHUB_USERNAME` | usuario de Docker Hub |
   | `DOCKERHUB_TOKEN` | Personal Access Token |
   | `EC2_HOST` | IP pública de la instancia |
   | `EC2_USER` | usuario SSH (`ubuntu`) |
   | `EC2_SSH_KEY` | contenido completo de la llave `.pem` |

4. Hacer `git push` a `main`. El progreso se ve en la pestaña **Actions** del repositorio.

## Qué hace el pipeline

| Job | Cuándo | Pasos |
| --- | --- | --- |
| Pruebas y cobertura | push y pull request a `main` | `dotnet test`, tabla de cobertura en el log, resumen y reporte HTML como artefacto |
| Build y push a Docker Hub | solo push a `main` | login con PAT, `docker build`, push de `:latest` y `:${{ github.sha }}` |
| Despliegue en AWS EC2 | después del push | SSH → `docker pull` → elimina el contenedor anterior → `docker run -p 80:80` con el volumen de datos → espera a `/api/health` → verificación pública |

## Seguridad

No hay contraseñas, IPs, tokens ni llaves en el código: todo se lee de GitHub Secrets o de variables de entorno
(`API_URL`, `EC2_HOST` en los scripts locales). `.gitignore` y `.dockerignore` excluyen `.env`, `*.pem`, `*.key`,
bases de datos y respaldos.

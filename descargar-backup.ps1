# Crea un backup de la BD en el servidor y lo descarga a la carpeta backups-aws del proyecto.
# Uso (desde la terminal de Visual Studio):  powershell -ExecutionPolicy Bypass -File .\descargar-backup.ps1
# Contra AWS (la URL no va en el código):  $env:API_URL = "http://<IP_EC2>"; powershell -ExecutionPolicy Bypass -File .\descargar-backup.ps1
# Sin API_URL se usa la app local (http://localhost:5136)
param([string]$Servidor = $(if ($env:API_URL) { $env:API_URL } else { "http://localhost:5136" }))

$ErrorActionPreference = "Stop"
$carpeta = Join-Path $PSScriptRoot "backups-aws"
New-Item -ItemType Directory -Force $carpeta | Out-Null

$temporal = Join-Path $carpeta "descargando.tmp"
$respuesta = Invoke-WebRequest "$Servidor/api/backup/descargar" -OutFile $temporal -PassThru -UseBasicParsing

# El nombre del archivo viene en la cabecera Content-Disposition (backup_FECHA_HORA.db)
$nombre = "backup_$(Get-Date -Format yyyyMMdd_HHmmss).db"
if ($respuesta.Headers["Content-Disposition"] -match 'filename=([^;]+)') { $nombre = $Matches[1].Trim('"', ' ') }

$destino = Join-Path $carpeta $nombre
Move-Item $temporal $destino -Force
Write-Host "Backup descargado en: $destino" -ForegroundColor Green

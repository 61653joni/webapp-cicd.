# Cliente del servidor socket TCP (puerto 6061).
# Uso interactivo (desde la terminal de Visual Studio):
#   powershell -ExecutionPolicy Bypass -File .\cliente-socket.ps1
#   y escribe comandos como:  {get:prendas:1}   {get:prendas}   {insert:{"nombre":"...", ...}}
# Contra AWS (la IP no va en el código):
#   $env:EC2_HOST = "<IP_EC2>"; powershell -ExecutionPolicy Bypass -File .\cliente-socket.ps1
# Sin EC2_HOST se usa la app local (localhost)
param(
    [string]$Servidor = $(if ($env:EC2_HOST) { $env:EC2_HOST } else { "localhost" }),
    [int]$Puerto = 6061,
    [string]$Mensaje = ""
)

function Enviar-Comando([string]$comando) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try {
        $cliente.ReceiveTimeout = 15000
        $cliente.Connect($Servidor, $Puerto)
        $stream = $cliente.GetStream()
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($comando)
        $stream.Write($bytes, 0, $bytes.Length)

        $memoria = New-Object System.IO.MemoryStream
        $buffer = New-Object byte[] 8192
        while (($leidos = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) { $memoria.Write($buffer, 0, $leidos) }
        # El servidor ya envia el JSON formateado
        [System.Text.Encoding]::UTF8.GetString($memoria.ToArray()).Trim()
    }
    catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red }
    finally { $cliente.Close() }
}

if ($Mensaje) {
    Write-Host "> $Mensaje" -ForegroundColor Cyan
    Enviar-Comando $Mensaje
    return
}

Write-Host "Cliente socket conectado a ${Servidor}:${Puerto}" -ForegroundColor Green
Write-Host "Ejemplos: {get:prendas}  {get:prendas:1}  {insert:{`"nombre`":`"Gorra`",`"categoriaId`":9,`"marcaId`":4,`"tallaId`":8,`"colorId`":5,`"generoId`":3,`"precio`":19.99,`"stock`":10}}"
Write-Host "Escribe 'salir' para terminar."
while ($true) {
    $comando = Read-Host "`nComando"
    if ($comando -eq "salir") { break }
    if ([string]::IsNullOrWhiteSpace($comando)) { continue }
    Write-Host "> $comando" -ForegroundColor Cyan
    Enviar-Comando $comando
}

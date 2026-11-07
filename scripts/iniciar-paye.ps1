# Iniciar Payé POS en modo PWA / Aplicación de Escritorio
$ErrorActionPreference = "Stop"

$projectRoot = "D:\Desarrollo\Paye_POS"
Set-Location $projectRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Iniciando Payé POS (PWA / Local)       " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Comprobar / Iniciar Base de Datos en Docker
Write-Host "[1/3] Verificando PostgreSQL en Docker..." -ForegroundColor Yellow
$containerStatus = (docker inspect -f '{{.State.Running}}' paye-postgres 2>$null)

if ($containerStatus -ne "true") {
    Write-Host " -> Arrancando contenedor PostgreSQL..." -ForegroundColor Gray
    docker compose -f docker-compose.dev.yml up -d
    Start-Sleep -Seconds 2
} else {
    Write-Host " -> PostgreSQL ya está activo en 127.0.0.1:5434." -ForegroundColor Green
}

# 2. Comprobar / Iniciar Servidor Kestrel
Write-Host "[2/3] Verificando servidor Payé POS (https://localhost:5001)..." -ForegroundColor Yellow
$serverRunning = $false

try {
    $req = [System.Net.WebRequest]::Create("https://localhost:5001/health")
    $req.Timeout = 2000
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
    $res = $req.GetResponse()
    if ($res.StatusCode -eq 200) {
        $serverRunning = $true
    }
    $res.Close()
} catch {
    $serverRunning = $false
}

if (-not $serverRunning) {
    Write-Host " -> Arrancando Kestrel en segundo plano..." -ForegroundColor Gray
    $env:Path = "C:\Users\Dario_Work\.dotnet;C:\Users\Dario_Work\.dotnet\tools;" + $env:Path
    $env:DOTNET_ROOT = "C:\Users\Dario_Work\.dotnet"
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $env:ASPNETCORE_URLS = "https://localhost:5001;http://localhost:5000"

    Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Paye.Web -c Release --no-build" -WorkingDirectory $projectRoot -WindowStyle Hidden

    # Esperar hasta que responda
    $attempts = 0
    while ($attempts -lt 15 -and -not $serverRunning) {
        Start-Sleep -Seconds 1
        $attempts++
        try {
            $req = [System.Net.WebRequest]::Create("https://localhost:5001/health")
            $req.Timeout = 1000
            $res = $req.GetResponse()
            $serverRunning = $true
            $res.Close()
        } catch {
            $serverRunning = $false
        }
    }
}

if ($serverRunning) {
    Write-Host " -> Servidor activo en https://localhost:5001." -ForegroundColor Green
} else {
    Write-Host " -> Advertencia: el servidor tardó en responder. Abriendo cliente de todos modos..." -ForegroundColor DarkYellow
}

# 3. Abrir en modo PWA / Aplicación Independiente
Write-Host "[3/3] Abriendo Payé POS en modo aplicación..." -ForegroundColor Yellow

$appUrl = "https://localhost:5001"
$edgePaths = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
)
$chromePaths = @(
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)

$launched = $false

foreach ($edge in $edgePaths) {
    if (Test-Path $edge) {
        Start-Process $edge -ArgumentList "--app=$appUrl", "--window-size=1440,900"
        $launched = $true
        break
    }
}

if (-not $launched) {
    foreach ($chrome in $chromePaths) {
        if (Test-Path $chrome) {
            Start-Process $chrome -ArgumentList "--app=$appUrl", "--window-size=1440,900"
            $launched = $true
            break
        }
    }
}

if (-not $launched) {
    Start-Process $appUrl
}

Write-Host "Payé POS iniciado correctamente." -ForegroundColor Green

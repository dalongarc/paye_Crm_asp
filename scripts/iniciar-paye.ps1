# Iniciar Paye CRM en modo PWA / Aplicacion de Escritorio (Windows 11 + Docker)
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $projectRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Iniciando Paye CRM (PWA / Windows 11)  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Preparar certificado HTTPS local para Kestrel en Docker
$certsDir = Join-Path $projectRoot "storage\certs"
$pfxPath = Join-Path $certsDir "aspnetcore-https.pfx"
if (-not (Test-Path $certsDir)) {
    New-Item -ItemType Directory -Path $certsDir -Force | Out-Null
}
if (-not (Test-Path $pfxPath)) {
    Write-Host "[0/3] Exportando certificado HTTPS de desarrollo local..." -ForegroundColor Yellow
    $env:Path = "C:\Users\Dario_Work\.dotnet;C:\Users\Dario_Work\.dotnet\tools;" + $env:Path
    $env:DOTNET_ROOT = "C:\Users\Dario_Work\.dotnet"
    dotnet dev-certs https -ep $pfxPath -p "paye-local-dev-cert" | Out-Null
}

# 2. Comprobar / Iniciar contenedores Docker (PostgreSQL + Paye.Web)
Write-Host "[1/3] Verificando servicios Docker (paye-crm-postgres + paye-crm-web)..." -ForegroundColor Yellow
$pgRunning = (docker ps -q -f "name=^paye-crm-postgres$" -f "status=running")
$webRunning = (docker ps -q -f "name=^paye-crm-web$" -f "status=running")

if (-not $pgRunning -or -not $webRunning -or ($args -contains "--rebuild")) {
    Write-Host " -> Levantando contenedores con Docker Compose..." -ForegroundColor Gray
    docker compose -f docker-compose.dev.yml up -d --build
} else {
    Write-Host " -> Contenedores Docker activos (PostgreSQL en 5435, Paye.Web en 5000/5001)." -ForegroundColor Green
}

# 3. Verificar salud del servidor y aplicar migraciones pendientes si las hubiera
Write-Host "[2/3] Verificando servidor Paye CRM (https://localhost:5001)..." -ForegroundColor Yellow
$serverRunning = $false
$attempts = 0
while ($attempts -lt 25 -and -not $serverRunning) {
    try {
        $req = [System.Net.WebRequest]::Create("http://127.0.0.1:5000/health")
        $req.Timeout = 1500
        $res = $req.GetResponse()
        if ([int]$res.StatusCode -eq 200) {
            $serverRunning = $true
        }
        $res.Close()
    } catch {
        $serverRunning = $false
        Start-Sleep -Seconds 1
        $attempts++
    }
}

if ($serverRunning) {
    $prevErr = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    docker exec paye-crm-web dotnet Paye.Web.dll --migrate *> $null
    $ErrorActionPreference = $prevErr
    Write-Host " -> Servidor activo y verificado en https://localhost:5001 (y http://localhost:5000)." -ForegroundColor Green
} else {
    Write-Host " -> Advertencia: el servidor tardo en responder. Revisa 'docker logs paye-crm-web'." -ForegroundColor DarkYellow
}

# 4. Abrir en modo PWA / Aplicacion Independiente de Windows 11
if (-not $env:PAYE_NO_BROWSER) {
    Write-Host "[3/3] Abriendo Paye CRM en modo aplicacion PWA..." -ForegroundColor Yellow

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
}

Write-Host "Paye CRM iniciado correctamente." -ForegroundColor Green

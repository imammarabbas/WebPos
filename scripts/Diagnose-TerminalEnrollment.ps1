# Diagnoses Windows Terminal enrollment against a running API (default localhost:8080).
# Usage: pwsh -File scripts/Diagnose-TerminalEnrollment.ps1
# Optional: -BaseUrl http://192.168.x.x:8080 -Password 'your-master-password'

param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$TerminalId = "00000000-0000-0000-0000-000000000010",
    [string]$Username = "admin",
    [string]$Password = "admin123"
)

$ErrorActionPreference = "Continue"
$base = $BaseUrl.TrimEnd("/")

Write-Host "=== WebPos enrollment diagnose ===" -ForegroundColor Cyan
Write-Host "API: $base"
Write-Host ""

try {
    $health = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing -TimeoutSec 10
    Write-Host "GET /health -> $($health.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "GET /health FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Start API first: docker compose up --build -d"
    exit 1
}

try {
    $pub = Invoke-RestMethod -Uri "$base/api/terminal-enrollment/public-key" -Headers @{ "X-Api-Version" = "1.0.0" } -TimeoutSec 10
    $pemLen = if ($pub.publicKeyPem) { $pub.publicKeyPem.Length } else { 0 }
    Write-Host "GET /api/terminal-enrollment/public-key -> OK (pem length $pemLen)" -ForegroundColor Green
} catch {
    Write-Host "GET public-key FAILED (rebuild API image with latest code): $($_.Exception.Message)" -ForegroundColor Yellow
}

$body = @{
    terminalId    = $TerminalId
    adminUsername = $Username
    adminPassword = $Password
} | ConvertTo-Json

Write-Host ""
Write-Host "POST /api/terminal-enrollment as $Username ..." -ForegroundColor Cyan
try {
    $enroll = Invoke-WebRequest -Uri "$base/api/terminal-enrollment" `
        -Method POST `
        -ContentType "application/json" `
        -Headers @{ "X-Api-Version" = "1.0.0" } `
        -Body $body `
        -UseBasicParsing `
        -TimeoutSec 30
    Write-Host "Enroll -> $($enroll.StatusCode) OK" -ForegroundColor Green
    $json = $enroll.Content | ConvertFrom-Json
    Write-Host "  terminalId=$($json.terminalId) tenantId=$($json.tenantId)"
    Write-Host "  token length=$($json.token.Length)"
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $text = $reader.ReadToEnd()
        Write-Host "Enroll -> $([int]$resp.StatusCode) FAILED" -ForegroundColor Red
        Write-Host $text
    } else {
        Write-Host "Enroll FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    exit 2
}

Write-Host ""
Write-Host "API enrollment works. If Terminal UI still fails, rebuild/redeploy WebPos.WindowsTerminal on that PC." -ForegroundColor Green

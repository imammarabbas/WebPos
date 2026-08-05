# Day-1 pilot smoke: enroll → login → start shift → sale → close → reconcile.
# Prerequisites: API on http://localhost:8080 (Docker or local Pilot host).
# Local API:
#   dotnet run --project WebPos --no-launch-profile --environment Pilot --urls http://localhost:8080
# Usage: powershell -File scripts/Day1-PilotSmoke.ps1

$ErrorActionPreference = "Stop"
$base = "http://localhost:8080"
$apiVersion = "1.0.0"
$terminalId = "00000000-0000-0000-0000-000000000010"
$cashierPin = if ($env:PILOT_CASHIER_PIN) { $env:PILOT_CASHIER_PIN } else { "2468" }
$adminPassword = if ($env:PILOT_ADMIN_PASSWORD) { $env:PILOT_ADMIN_PASSWORD } else { "admin123" }

# Load .env if present
$envFile = Join-Path (Split-Path -Parent $PSScriptRoot) ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $k, $v = $_.Split('=', 2)
        if ($k -eq "PILOT_CASHIER_PIN") { $cashierPin = $v }
        if ($k -eq "PILOT_ADMIN_PASSWORD") { $adminPassword = $v }
        if ($k -eq "PILOT_TERMINAL_ID") { $terminalId = $v }
    }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Bearer = $null
    )
    $headers = @{ "X-Api-Version" = $apiVersion }
    if ($Bearer) { $headers["Authorization"] = "Bearer $Bearer" }
    $params = @{
        Method      = $Method
        Uri         = "$base$Path"
        Headers     = $headers
        ContentType = "application/json; charset=utf-8"
    }
    if ($null -ne $Body) {
        # Hashtable keys are already camelCase; Compress keeps a single-line body.
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    return Invoke-RestMethod @params
}

Write-Host "1) Health..."
$health = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing
if ($health.StatusCode -ne 200) { throw "Health check failed: $($health.StatusCode)" }
Write-Host "   OK $($health.Content)"

Write-Host "2) Enroll terminal..."
$enroll = Invoke-Api -Method POST -Path "/api/terminal-enrollment" -Body @{
    terminalId     = $terminalId
    adminUsername  = "admin"
    adminPassword  = $adminPassword
}
$token = $enroll.token
if (-not $token) { throw "Enrollment returned no token" }
Write-Host "   Enrolled tenant=$($enroll.tenantId) terminal=$($enroll.terminalId)"

Write-Host "3) PIN login..."
$cashier = Invoke-Api -Method POST -Path "/api/auth/login" -Bearer $token -Body @{
    pin = $cashierPin
}
Write-Host "   Cashier $($cashier.name) ($($cashier.cashierId))"

Write-Host "4) Start shift..."
$shift = Invoke-Api -Method POST -Path "/api/shift/start" -Bearer $token -Body @{
    cashierId         = $cashier.cashierId
    terminalId        = $terminalId
    openingCashPaisa  = 0
}
Write-Host "   Shift $($shift.shiftId) status=$($shift.status)"

Write-Host "5) Products for sale..."
$products = Invoke-Api -Method GET -Path "/api/products/for-sale" -Bearer $token
if (-not $products -or $products.Count -lt 4) {
    throw "Expected at least 4 pilot products (milk + loose items), got $($products.Count)"
}
$shortCodes = @($products | ForEach-Object { $_.shortCode })
if ($shortCodes -notcontains "1001") { throw "Missing short code 1001 (Buffalo Milk)" }
if ($shortCodes -notcontains "2002") { throw "Missing short code 2002 (Samosa)" }
$samosa = $products | Where-Object { $_.shortCode -eq "2002" } | Select-Object -First 1
if (-not $samosa) { throw "Samosa product not found" }
Write-Host "   $($products.Count) products; selling $($samosa.name) (code $($samosa.shortCode)) @ $($samosa.unitPricePaisa) paisa"

Write-Host "6) Complete cash sale (2 units)..."
$qty = 2
$sale = Invoke-Api -Method POST -Path "/api/sales/complete" -Bearer $token -Body @{
    invoiceNo           = "SMOKE-$(Get-Date -Format 'yyyyMMddHHmmss')"
    shiftId             = $shift.shiftId
    terminalId          = $terminalId
    cashierId           = $cashier.cashierId
    paymentMethod       = "CASH"
    discountAmountPaisa = 0
    lines               = @(
        @{
            productId           = $samosa.productId
            batchId             = $samosa.batchId
            batchNumber         = $samosa.batchNumber
            productName         = $samosa.name
            quantity            = $qty
            unitPricePaisa      = $samosa.unitPricePaisa
            discountAppliedPaisa = 0
        }
    )
}
$expected = $sale.totalAmountPaisa
Write-Host "   Sale total $expected paisa (invoice $($sale.invoiceNo))"

Write-Host "7) Close shift..."
$report = Invoke-Api -Method POST -Path "/api/shift/close" -Bearer $token -Body @{
    shiftId          = $shift.shiftId
    cashierId        = $cashier.cashierId
    terminalId       = $terminalId
    actualCashPaisa  = $expected
}
Write-Host "   Expected=$($report.expectedCashPaisa) Actual=$($report.actualCashPaisa) Discrepancy=$($report.discrepancyPaisa) Balanced=$($report.isBalanced)"

if ($report.expectedCashPaisa -ne $expected) {
    throw "Shift expected cash $($report.expectedCashPaisa) != sale total $expected"
}
if (-not $report.isBalanced -or $report.discrepancyPaisa -ne 0) {
    throw "Cash variance not balanced"
}

Write-Host ""
Write-Host "Day-1 smoke PASSED - enroll -> login -> shift -> sale -> close reconciled."

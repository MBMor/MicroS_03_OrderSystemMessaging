param(
    [string]$OrdersBaseUrl = "http://localhost:5081",
    [string]$InventoryBaseUrl = "http://localhost:5082",
    [string]$NotificationsBaseUrl = "http://localhost:5083",
    [int]$TimeoutSeconds = 90,
    [int]$PollingIntervalSeconds = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$OrdersBaseUrl = $OrdersBaseUrl.TrimEnd("/")
$InventoryBaseUrl = $InventoryBaseUrl.TrimEnd("/")
$NotificationsBaseUrl = $NotificationsBaseUrl.TrimEnd("/")

function Write-Step {
    param(
        [string]$Message
    )

    Write-Host ""
    Write-Host "==> $Message"
}

function Write-Success {
    param(
        [string]$Message
    )

    Write-Host "[OK] $Message"
}

function Assert-Equal {
    param(
        [object]$Actual,
        [object]$Expected,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', but got '$Actual'."
    }
}

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 20

    return Invoke-RestMethod `
        -Method Post `
        -Uri $Uri `
        -ContentType "application/json" `
        -Body $json `
        -TimeoutSec 20
}

function Invoke-JsonPut {
    param(
        [string]$Uri,
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 20

    return Invoke-RestMethod `
        -Method Put `
        -Uri $Uri `
        -ContentType "application/json" `
        -Body $json `
        -TimeoutSec 20
}

function Wait-Until {
    param(
        [scriptblock]$Condition,
        [string]$Description,
        [int]$Timeout = $script:TimeoutSeconds,
        [int]$Delay = $script:PollingIntervalSeconds
    )

    $deadline = (Get-Date).AddSeconds($Timeout)

    while ((Get-Date) -lt $deadline) {
        if (& $Condition) {
            return
        }

        Start-Sleep -Seconds $Delay
    }

    throw "Timed out after $Timeout second(s): $Description"
}

function Test-HealthEndpoint {
    param(
        [string]$Name,
        [string]$BaseUrl
    )

    $response = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/health/ready" `
        -TimeoutSec 20

    $status = $response

    if ($null -ne $response.PSObject.Properties["status"]) {
        $status = $response.status
    }

    if ([string]$status -notmatch "Healthy") {
        throw "$Name health check is not healthy. Response: $($response | ConvertTo-Json -Depth 10)"
    }

    Write-Success "$Name readiness health check is healthy."
}

function Get-Order {
    param(
        [string]$OrderId
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri "$script:OrdersBaseUrl/api/v1/orders/$OrderId" `
        -TimeoutSec 20
}

function Wait-ForOrderStatus {
    param(
        [string]$OrderId,
        [string]$ExpectedStatus
    )

    Wait-Until `
        -Description "Order $OrderId should reach status $ExpectedStatus." `
        -Condition {
            $order = Get-Order -OrderId $OrderId
            return [string]$order.status -eq $ExpectedStatus
        }

    $order = Get-Order -OrderId $OrderId

    Assert-Equal `
        -Actual ([string]$order.status) `
        -Expected $ExpectedStatus `
        -Message "Unexpected order status."

    Write-Success "Order $OrderId reached status $ExpectedStatus."
}

function Get-InventoryItem {
    param(
        [string]$ProductId
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri "$script:InventoryBaseUrl/api/v1/inventory-items/$ProductId" `
        -TimeoutSec 20
}

function Get-PagedItems {
    param(
        [object]$PagedResult
    )

    if ($null -eq $PagedResult) {
        return @()
    }

    foreach ($propertyName in @("items", "Items", "data", "Data")) {
        if ($PagedResult.PSObject.Properties.Name -contains $propertyName) {
            return @($PagedResult.$propertyName)
        }
    }

    return @($PagedResult)
}

function Get-Notifications {
    return Invoke-RestMethod `
        -Method Get `
        -Uri "$script:NotificationsBaseUrl/api/v1/notifications?page=1&pageSize=100" `
        -TimeoutSec 20
}

function Test-NotificationExists {
    param(
        [string]$OrderId,
        [string]$ExpectedSubjectPart
    )

    $pagedNotifications = Get-Notifications
    $notifications = Get-PagedItems -PagedResult $pagedNotifications

    $matchingNotifications = @(
        $notifications | Where-Object {
            ([string]$_.subject).Contains($ExpectedSubjectPart) `
                -and ([string]$_.subject).Contains($OrderId)
        }
    )

    return $matchingNotifications.Count -gt 0
}

function Wait-ForNotification {
    param(
        [string]$OrderId,
        [string]$ExpectedSubjectPart
    )

    Wait-Until `
        -Description "Notification containing '$ExpectedSubjectPart' for order $OrderId should exist." `
        -Condition {
            return Test-NotificationExists `
                -OrderId $OrderId `
                -ExpectedSubjectPart $ExpectedSubjectPart
        }

    Write-Success "Notification '$ExpectedSubjectPart' for order $OrderId exists."
}

Write-Host "Starting local smoke test."
Write-Host "Orders API:        $OrdersBaseUrl"
Write-Host "Inventory API:     $InventoryBaseUrl"
Write-Host "Notifications API: $NotificationsBaseUrl"

Write-Step "Checking service readiness"

Test-HealthEndpoint -Name "Orders Service" -BaseUrl $OrdersBaseUrl
Test-HealthEndpoint -Name "Inventory Service" -BaseUrl $InventoryBaseUrl
Test-HealthEndpoint -Name "Notifications Service" -BaseUrl $NotificationsBaseUrl

Write-Step "Creating inventory item"

$productId = [Guid]::NewGuid().ToString()
$productName = "Smoke Test Keyboard"

$inventoryItem = Invoke-JsonPost `
    -Uri "$InventoryBaseUrl/api/v1/inventory-items" `
    -Body @{
        productId = $productId
        productName = $productName
        availableQuantity = 50
    }

Assert-Equal `
    -Actual ([string]$inventoryItem.productId) `
    -Expected $productId `
    -Message "Created inventory item has unexpected ProductId."

Write-Success "Inventory item $productId was created."

Write-Step "Creating order for successful stock reservation"

$successfulOrder = Invoke-JsonPost `
    -Uri "$OrdersBaseUrl/api/v1/orders" `
    -Body @{
        customerName = "Smoke Test Success"
        customerEmail = "smoke.success@example.com"
        items = @(
            @{
                productId = $productId
                productName = $productName
                quantity = 2
            }
        )
    }

$successfulOrderId = [string]$successfulOrder.id

if ([string]::IsNullOrWhiteSpace($successfulOrderId)) {
    throw "Successful order response does not contain order id."
}

Write-Success "Successful scenario order $successfulOrderId was created."

Write-Step "Waiting for successful order to become StockReserved"

Wait-ForOrderStatus `
    -OrderId $successfulOrderId `
    -ExpectedStatus "StockReserved"

Write-Step "Checking inventory quantity after successful reservation"

$inventoryAfterSuccess = Get-InventoryItem -ProductId $productId

Assert-Equal `
    -Actual ([int]$inventoryAfterSuccess.availableQuantity) `
    -Expected 48 `
    -Message "AvailableQuantity after successful reservation is invalid."

Assert-Equal `
    -Actual ([int]$inventoryAfterSuccess.reservedQuantity) `
    -Expected 2 `
    -Message "ReservedQuantity after successful reservation is invalid."

Write-Success "Inventory quantities are correct after successful reservation."

Write-Step "Checking notifications for successful scenario"

Wait-ForNotification `
    -OrderId $successfulOrderId `
    -ExpectedSubjectPart "was created"

Wait-ForNotification `
    -OrderId $successfulOrderId `
    -ExpectedSubjectPart "Stock reserved"

Write-Step "Creating order for failed stock reservation"

$failedOrder = Invoke-JsonPost `
    -Uri "$OrdersBaseUrl/api/v1/orders" `
    -Body @{
        customerName = "Smoke Test Failed"
        customerEmail = "smoke.failed@example.com"
        items = @(
            @{
                productId = $productId
                productName = $productName
                quantity = 999
            }
        )
    }

$failedOrderId = [string]$failedOrder.id

if ([string]::IsNullOrWhiteSpace($failedOrderId)) {
    throw "Failed scenario order response does not contain order id."
}

Write-Success "Failed scenario order $failedOrderId was created."

Write-Step "Waiting for failed order to become StockReservationFailed"

Wait-ForOrderStatus `
    -OrderId $failedOrderId `
    -ExpectedStatus "StockReservationFailed"

Write-Step "Checking notifications for failed scenario"

Wait-ForNotification `
    -OrderId $failedOrderId `
    -ExpectedSubjectPart "was created"

Wait-ForNotification `
    -OrderId $failedOrderId `
    -ExpectedSubjectPart "Stock reservation failed"

Write-Step "Checking inventory quantity after failed reservation"

$inventoryAfterFailure = Get-InventoryItem -ProductId $productId

Assert-Equal `
    -Actual ([int]$inventoryAfterFailure.availableQuantity) `
    -Expected 48 `
    -Message "AvailableQuantity after failed reservation is invalid."

Assert-Equal `
    -Actual ([int]$inventoryAfterFailure.reservedQuantity) `
    -Expected 2 `
    -Message "ReservedQuantity after failed reservation is invalid."

Write-Success "Inventory quantities are unchanged after failed reservation."

Write-Host ""
Write-Host "Smoke test completed successfully."
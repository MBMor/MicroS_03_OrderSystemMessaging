param(
    [string]$GatewayBaseUrl = "http://localhost:5080",
    [string]$OrdersBaseUrl = "http://localhost:5081",
    [string]$InventoryBaseUrl = "http://localhost:5082",
    [string]$NotificationsBaseUrl = "http://localhost:5083",
    [string]$AccessToken = $env:CUSTOMER_TOKEN,
    [string]$SetupAccessToken = $env:SUPPORT_TOKEN,
    [int]$TimeoutSeconds = 90,
    [int]$PollingIntervalSeconds = 2,
    [switch]$VerifyDockerLogs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$OrdersBaseUrl = $OrdersBaseUrl.TrimEnd("/")
$InventoryBaseUrl = $InventoryBaseUrl.TrimEnd("/")
$NotificationsBaseUrl = $NotificationsBaseUrl.TrimEnd("/")
if ([string]::IsNullOrWhiteSpace($SetupAccessToken) -and -not [string]::IsNullOrWhiteSpace($env:ADMIN_TOKEN)) {
    $SetupAccessToken = $env:ADMIN_TOKEN
}

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

function Write-WarningMessage {
    param(
        [string]$Message
    )

    Write-Host "[WARN] $Message" -ForegroundColor Yellow
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

function Assert-NotBlank {
    param(
        [string]$Value,
        [string]$Message
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw $Message
    }
}

function New-RequestHeaders {
    param(
        [string]$CorrelationId,
        [string]$Token,
        [bool]$RequireAuthorization = $false
    )

    $headers = @{
        "X-Correlation-Id" = $CorrelationId
    }

    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers["Authorization"] = "Bearer $Token"
    }
    elseif ($RequireAuthorization) {
        throw "Access token is required for this request."
    }

    return $headers
}

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [object]$Body,
        [string]$CorrelationId,
        [string]$Token = $script:AccessToken,
        [bool]$RequireAuthorization = $false
    )

    $json = $Body | ConvertTo-Json -Depth 20

    return Invoke-RestMethod `
        -Method Post `
        -Uri $Uri `
        -Headers (New-RequestHeaders -CorrelationId $CorrelationId -Token $Token -RequireAuthorization $RequireAuthorization) `
        -ContentType "application/json" `
        -Body $json `
        -TimeoutSec 20
}

function Invoke-JsonGet {
    param(
        [string]$Uri,
        [string]$CorrelationId,
        [string]$Token = $script:AccessToken,
        [bool]$RequireAuthorization = $false
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri $Uri `
        -Headers (New-RequestHeaders -CorrelationId $CorrelationId -Token $Token -RequireAuthorization $RequireAuthorization) `
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

function Get-OrderThroughGateway {
    param(
        [string]$OrderId,
        [string]$CorrelationId
    )

    return Invoke-JsonGet `
        -Uri "$script:GatewayBaseUrl/api/v1/orders/$OrderId" `
        -CorrelationId $CorrelationId `
        -RequireAuthorization $true
}

function Wait-ForOrderStatus {
    param(
        [string]$OrderId,
        [string]$ExpectedStatus,
        [string]$CorrelationId
    )

    Wait-Until `
        -Description "Order $OrderId should reach status $ExpectedStatus." `
        -Condition {
            $order = Get-OrderThroughGateway `
                -OrderId $OrderId `
                -CorrelationId $CorrelationId

            return [string]$order.status -eq $ExpectedStatus
        }

    $order = Get-OrderThroughGateway `
        -OrderId $OrderId `
        -CorrelationId $CorrelationId

    Assert-Equal `
        -Actual ([string]$order.status) `
        -Expected $ExpectedStatus `
        -Message "Unexpected order status."

    Write-Success "Order $OrderId reached status $ExpectedStatus."
}

function Get-InventoryItem {
    param(
        [string]$ProductId,
        [string]$CorrelationId
    )

    return Invoke-JsonGet `
        -Uri "$script:InventoryBaseUrl/api/v1/inventory-items/$ProductId" `
        -CorrelationId $CorrelationId `
        -Token $script:SetupAccessToken `
        -RequireAuthorization $true
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
    param(
        [string]$CorrelationId
    )

    return Invoke-JsonGet `
        -Uri "$script:NotificationsBaseUrl/api/v1/notifications?page=1&pageSize=100" `
        -CorrelationId $CorrelationId `
        -Token $script:SetupAccessToken `
        -RequireAuthorization $true
}

function Test-NotificationExists {
    param(
        [string]$OrderId,
        [string]$ExpectedSubjectPart,
        [string]$CorrelationId
    )

    $pagedNotifications = Get-Notifications -CorrelationId $CorrelationId
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
        [string]$ExpectedSubjectPart,
        [string]$CorrelationId
    )

    Wait-Until `
        -Description "Notification containing '$ExpectedSubjectPart' for order $OrderId should exist." `
        -Condition {
            return Test-NotificationExists `
                -OrderId $OrderId `
                -ExpectedSubjectPart $ExpectedSubjectPart `
                -CorrelationId $CorrelationId
        }

    Write-Success "Notification '$ExpectedSubjectPart' for order $OrderId exists."
}

function Test-DockerLogContains {
    param(
        [string]$ServiceName,
        [string]$CorrelationId
    )

    $logs = & docker compose logs --no-color --tail 2000 $ServiceName 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-WarningMessage "Could not read Docker logs for service '$ServiceName'. Skipping log check."
        return
    }

    $joinedLogs = $logs -join [Environment]::NewLine

    if ($joinedLogs.Contains($CorrelationId)) {
        Write-Success "Docker logs for '$ServiceName' contain correlation id '$CorrelationId'."
        return
    }

    Write-WarningMessage "Docker logs for '$ServiceName' do not contain correlation id '$CorrelationId'. Check Aspire traces/logs manually."
}

function Test-ObservabilityLogs {
    param(
        [string]$CorrelationId
    )

    foreach ($serviceName in @("orders-api", "inventory-api", "notifications-api")) {
        Test-DockerLogContains `
            -ServiceName $serviceName `
            -CorrelationId $CorrelationId
    }

    Write-WarningMessage "API Gateway correlation id is not checked in Docker logs because gateway does not log every successful proxied request. Verify gateway spans in Aspire Dashboard."
}

Write-Host "Starting observability smoke test."
Write-Host "Gateway API: $GatewayBaseUrl"
Write-Host "Orders API: $OrdersBaseUrl"
Write-Host "Inventory API: $InventoryBaseUrl"
Write-Host "Notifications API: $NotificationsBaseUrl"

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw "Customer access token is required for gateway order calls. Set CUSTOMER_TOKEN or pass -AccessToken."
}

if ([string]::IsNullOrWhiteSpace($SetupAccessToken)) {
    throw "Admin access token is required for inventory setup calls. Set ADMIN_TOKEN or pass -SetupAccessToken."
}

Write-Step "Checking service readiness"

Test-HealthEndpoint -Name "Gateway API" -BaseUrl $GatewayBaseUrl
Test-HealthEndpoint -Name "Orders Service" -BaseUrl $OrdersBaseUrl
Test-HealthEndpoint -Name "Inventory Service" -BaseUrl $InventoryBaseUrl
Test-HealthEndpoint -Name "Notifications Service" -BaseUrl $NotificationsBaseUrl

Write-Step "Creating inventory item for observability smoke test"

$setupCorrelationId = "observability-smoke-setup-$([Guid]::NewGuid().ToString("N"))"
$successCorrelationId = "observability-smoke-success-$([Guid]::NewGuid().ToString("N"))"
$failureCorrelationId = "observability-smoke-failure-$([Guid]::NewGuid().ToString("N"))"

$productId = [Guid]::NewGuid().ToString()
$productName = "Observability Smoke Test Item"

$inventoryItem = Invoke-JsonPost `
    -Uri "$InventoryBaseUrl/api/v1/inventory-items" `
    -CorrelationId $setupCorrelationId `
    -Token $SetupAccessToken `
    -RequireAuthorization $true `
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
Write-Host "Setup correlation id: $setupCorrelationId"

Write-Step "Creating successful order through API Gateway"

$successfulOrder = Invoke-JsonPost `
    -Uri "$GatewayBaseUrl/api/v1/orders" `
    -CorrelationId $successCorrelationId `
    -RequireAuthorization $true `
    -Body @{
        customerName = "Observability Smoke Success"
        customerEmail = "observability.success@example.com"
        items = @(
            @{
                productId = $productId
                productName = $productName
                quantity = 2
            }
        )
    }

$successfulOrderId = [string]$successfulOrder.id

Assert-NotBlank `
    -Value $successfulOrderId `
    -Message "Successful order response does not contain order id."

Write-Success "Successful scenario order $successfulOrderId was created through gateway."
Write-Host "Success correlation id: $successCorrelationId"

Write-Step "Waiting for successful order to become StockReserved"

Wait-ForOrderStatus `
    -OrderId $successfulOrderId `
    -ExpectedStatus "StockReserved" `
    -CorrelationId $successCorrelationId

Write-Step "Checking inventory quantity after successful reservation"

$inventoryAfterSuccess = Get-InventoryItem `
    -ProductId $productId `
    -CorrelationId $successCorrelationId

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
    -ExpectedSubjectPart "was created" `
    -CorrelationId $successCorrelationId

Wait-ForNotification `
    -OrderId $successfulOrderId `
    -ExpectedSubjectPart "Stock reserved" `
    -CorrelationId $successCorrelationId

Write-Step "Creating failed stock reservation order through API Gateway"

$failedOrder = Invoke-JsonPost `
    -Uri "$GatewayBaseUrl/api/v1/orders" `
    -CorrelationId $failureCorrelationId `
    -RequireAuthorization $true `
    -Body @{
        customerName = "Observability Smoke Failed"
        customerEmail = "observability.failed@example.com"
        items = @(
            @{
                productId = $productId
                productName = $productName
                quantity = 999
            }
        )
    }

$failedOrderId = [string]$failedOrder.id

Assert-NotBlank `
    -Value $failedOrderId `
    -Message "Failed scenario order response does not contain order id."

Write-Success "Failed scenario order $failedOrderId was created through gateway."
Write-Host "Failure correlation id: $failureCorrelationId"

Write-Step "Waiting for failed order to become StockReservationFailed"

Wait-ForOrderStatus `
    -OrderId $failedOrderId `
    -ExpectedStatus "StockReservationFailed" `
    -CorrelationId $failureCorrelationId

Write-Step "Checking notifications for failed scenario"

Wait-ForNotification `
    -OrderId $failedOrderId `
    -ExpectedSubjectPart "was created" `
    -CorrelationId $failureCorrelationId

Wait-ForNotification `
    -OrderId $failedOrderId `
    -ExpectedSubjectPart "Stock reservation failed" `
    -CorrelationId $failureCorrelationId

Write-Step "Checking inventory quantity after failed reservation"

$inventoryAfterFailure = Get-InventoryItem `
    -ProductId $productId `
    -CorrelationId $failureCorrelationId

Assert-Equal `
    -Actual ([int]$inventoryAfterFailure.availableQuantity) `
    -Expected 48 `
    -Message "AvailableQuantity after failed reservation is invalid."

Assert-Equal `
    -Actual ([int]$inventoryAfterFailure.reservedQuantity) `
    -Expected 2 `
    -Message "ReservedQuantity after failed reservation is invalid."

Write-Success "Inventory quantities are unchanged after failed reservation."

if ($VerifyDockerLogs) {
    Write-Step "Checking Docker logs for correlation ids"

    Test-ObservabilityLogs -CorrelationId $successCorrelationId
    Test-ObservabilityLogs -CorrelationId $failureCorrelationId
}
else {
    Write-Step "Skipping Docker log checks"

    Write-Host "Run with -VerifyDockerLogs to check whether correlation ids appear in container logs."
}

Write-Host ""
Write-Host "Observability smoke test completed successfully."
Write-Host ""
Write-Host "Use these correlation ids in Aspire Dashboard:"
Write-Host "  setup:   $setupCorrelationId"
Write-Host "  success: $successCorrelationId"
Write-Host "  failure: $failureCorrelationId"
Write-Host ""
Write-Host "Expected traces:"
Write-Host "  api-gateway: POST /api/v1/orders"
Write-Host "  orders-service: orders.create"
Write-Host "  orders-service: outbox.publish_message"
Write-Host "  inventory-service: rabbitmq.consume"
Write-Host "  inventory-service: inventory.reserve_stock"
Write-Host "  inventory-service: outbox.publish_message"
Write-Host "  orders-service: rabbitmq.consume"
Write-Host "  notifications-service: rabbitmq.consume"
Write-Host ""
Write-Host "Expected metrics:"
Write-Host "  orders.created.total"
Write-Host "  orders.stock_reserved.total"
Write-Host "  orders.stock_reservation_failed.total"
Write-Host "  inventory.stock_reservations.total"
Write-Host "  inventory.stock_reservation_failures.total"
Write-Host "  notifications.created.total"
Write-Host "  outbox.messages.published.total"
Write-Host "  outbox.publish.duration.ms"
Write-Host "  rabbitmq.messages.published.total"
Write-Host "  rabbitmq.messages.consumed.total"
Write-Host "  rabbitmq.consume.duration.ms"
param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$ApiBaseUrl = "https://localhost:7268",
    [string]$FrontendBaseUrl = "http://localhost:4200",
    [string]$BuyerEmail = "buyer@mabuntle.local",
    [string]$AdminEmail = "admin@mabuntle.local",
    [string]$ProductSlug = "rose-linen-midi-dress",
    [int]$Quantity = 1,
    [int]$ReservationMinutes = 15,
    [string]$WebhookSigningSecret = "development-only-webhook-secret-change-before-production",
    [switch]$AssistantOnly,
    [switch]$VisualOnly,
    [switch]$SkipCertificateCheck
)

$ErrorActionPreference = "Stop"

if ($SkipCertificateCheck) {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}

function Normalize-BaseUrl {
    param([string]$Value)
    return $Value.TrimEnd("/")
}

function ConvertTo-JsonBody {
    param([object]$Value)
    return $Value | ConvertTo-Json -Depth 20 -Compress
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body = $null,
        [hashtable]$Headers = @{},
        [string]$ContentType = "application/json"
    )

    $uri = "$script:ApiBaseUrl$Path"
    $invokeParams = @{
        Method = $Method
        Uri = $uri
        Headers = $Headers
    }

    if ($null -ne $Body) {
        $invokeParams.ContentType = $ContentType
        $invokeParams.Body = if ($Body -is [string]) { $Body } else { ConvertTo-JsonBody $Body }
    }

    try {
        return Invoke-RestMethod @invokeParams
    }
    catch {
        $message = $_.Exception.Message
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            $message = "$message $($_.ErrorDetails.Message)"
        }

        throw "API call failed: $Method $uri. $message"
    }
}

function Get-AuthHeader {
    param([string]$Email)

    $response = Invoke-Api -Method "Post" -Path "/api/auth/login" -Body @{
        email = $Email
        password = $Password
    }

    if ([string]::IsNullOrWhiteSpace($response.accessToken)) {
        throw "Login for $Email did not return an access token."
    }

    return @{ Authorization = "Bearer $($response.accessToken)" }
}

function Get-FakeWebhookSignature {
    param(
        [string]$Payload,
        [string]$Secret
    )

    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($Secret))
    try {
        $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Payload))
        return ([System.BitConverter]::ToString($hash) -replace "-", "").ToLowerInvariant()
    }
    finally {
        $hmac.Dispose()
    }
}

function Wait-ForOrderStatus {
    param(
        [hashtable]$Headers,
        [string]$OrderId,
        [string]$ExpectedStatus,
        [int]$Attempts = 10
    )

    for ($i = 0; $i -lt $Attempts; $i++) {
        $order = Invoke-Api -Method "Get" -Path "/api/buyer/orders/$OrderId" -Headers $Headers
        if ($order.status -eq $ExpectedStatus) {
            return $order
        }

        Start-Sleep -Seconds 1
    }

    throw "Order $OrderId did not reach status $ExpectedStatus after $Attempts attempts."
}

function Get-FirstInStockVariant {
    param([object]$Product)

    $variant = @($Product.variants | Where-Object { $_.inStock }) | Select-Object -First 1
    if ($null -eq $variant) {
        throw "Product $ProductSlug does not have an in-stock variant."
    }

    return $variant
}

function Record-GrowthEvent {
    param(
        [hashtable]$Headers,
        [string]$EventType,
        [string]$SourceTool,
        [string]$ProductId,
        [int]$ResultCount,
        [string]$ConfidenceBand,
        [string]$Category,
        [string]$Colour,
        [string]$Material,
        [string]$SourceRoute,
        [string]$FeedbackReason = $null
    )

    return Invoke-Api -Method "Post" -Path "/api/buyer/growth-events" -Headers $Headers -Body @{
        eventType = $EventType
        sourceTool = $SourceTool
        productId = if ([string]::IsNullOrWhiteSpace($ProductId)) { $null } else { $ProductId }
        resultCount = $ResultCount
        confidenceBand = $ConfidenceBand
        category = $Category
        colour = $Colour
        material = $Material
        sourceRoute = $SourceRoute
        feedbackReason = $FeedbackReason
    }
}

function Get-SequenceCount {
    param([object]$Value)

    if ($null -eq $Value) {
        return 0
    }

    return @($Value).Count
}

function Complete-AttributedOrder {
    param(
        [hashtable]$BuyerHeaders,
        [object]$Product,
        [object]$Variant,
        [string]$SourceTool
    )

    $cart = Invoke-Api -Method "Post" -Path "/api/cart/items" -Headers $BuyerHeaders -Body @{
        productVariantId = $Variant.variantId
        quantity = $Quantity
    }

    $deliveryAddress = @{
        recipientName = "AI Attribution Buyer"
        phoneNumber = "+27110000000"
        addressLine1 = "10 Market Street"
        addressLine2 = $null
        suburb = "Rosebank"
        city = "Johannesburg"
        province = "Gauteng"
        postalCode = "2196"
        countryCode = "ZA"
        deliveryInstructions = "$SourceTool attribution QA order."
    }

    $shippingOptions = Invoke-Api -Method "Post" -Path "/api/cart/shipping-options" -Headers $BuyerHeaders -Body @{
        cartId = $cart.cartId
        deliveryAddressId = $null
        deliveryAddress = $deliveryAddress
    }

    $selectedShipping = $shippingOptions.options | Where-Object { -not $_.requiresPickupPoint } | Sort-Object displayOrder, shippingAmount | Select-Object -First 1
    if ($null -eq $selectedShipping) {
        $selectedShipping = $shippingOptions.options | Sort-Object displayOrder, shippingAmount | Select-Object -First 1
    }

    if ($null -eq $selectedShipping) {
        throw "No shipping options were returned for cart $($cart.cartId)."
    }

    $pickupPointId = $null
    if ($selectedShipping.requiresPickupPoint) {
        $pickupPoint = $selectedShipping.pickupPoints | Select-Object -First 1
        if ($null -eq $pickupPoint) {
            throw "Selected pickup delivery method requires a pickup point, but none were returned."
        }

        $pickupPointId = $pickupPoint.pickupPointId
    }

    $order = Invoke-Api -Method "Post" -Path "/api/orders/from-cart" -Headers $BuyerHeaders -Body @{
        cartId = $cart.cartId
        reservationMinutes = $ReservationMinutes
        deliveryAddressId = $null
        deliveryAddress = $deliveryAddress
        deliveryMethodId = $selectedShipping.deliveryMethodId
        pickupPointId = $pickupPointId
    }

    $payment = Invoke-Api -Method "Post" -Path "/api/payments/initiate" -Headers $BuyerHeaders -Body @{
        orderId = $order.orderId
    }

    if ($payment.provider -ne "Fake") {
        throw "Expected Fake payment provider for this local helper, but received $($payment.provider)."
    }

    $eventId = "fake_ai_attr_$((New-Guid).ToString('N'))"
    $webhookPayload = ConvertTo-JsonBody @{
        eventId = $eventId
        eventType = "payment.paid"
        providerReference = $payment.providerReference
        status = "Paid"
        occurredAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        amount = $payment.amount
        currency = $payment.currency
    }
    $signature = Get-FakeWebhookSignature -Payload $webhookPayload -Secret $WebhookSigningSecret

    $webhookResult = Invoke-Api -Method "Post" -Path "/api/payments/webhook/Fake" -Headers @{
        "X-Mabuntle-Fake-Signature" = $signature
    } -Body $webhookPayload

    $paidOrder = Wait-ForOrderStatus -Headers $BuyerHeaders -OrderId $order.orderId -ExpectedStatus "Paid"

    return [pscustomobject]@{
        SourceTool = $SourceTool
        CartId = $cart.cartId
        OrderId = $order.orderId
        PaymentId = $payment.paymentId
        ProviderReference = $payment.providerReference
        WebhookEventId = $webhookResult.providerEventId
        FinalStatus = $paidOrder.status
        ProductId = $Product.product.productId
    }
}

$script:ApiBaseUrl = Normalize-BaseUrl $ApiBaseUrl
$FrontendBaseUrl = Normalize-BaseUrl $FrontendBaseUrl

try {
    if ($AssistantOnly -and $VisualOnly) {
        throw "Use either -AssistantOnly or -VisualOnly, not both."
    }

    Write-Host "Creating buyer AI attribution demo state against $script:ApiBaseUrl"

    $buyerHeaders = Get-AuthHeader -Email $BuyerEmail
    $adminHeaders = Get-AuthHeader -Email $AdminEmail
    $product = Invoke-Api -Method "Get" -Path "/api/products/$ProductSlug"
    $variant = Get-FirstInStockVariant -Product $product
    $productId = $product.product.productId
    $category = if ($product.product.categoryName) { $product.product.categoryName } else { "Dresses" }
    $colour = if ($variant.colour) { $variant.colour } else { "Rose" }

    $runs = @()

    if (-not $VisualOnly) {
        $assistant = Invoke-Api -Method "Post" -Path "/api/buyer/ai/shopping-assistant" -Headers $buyerHeaders -Body @{
            message = "Find a rose linen midi dress in size $($variant.size)."
        }
        $assistantResultCount = Get-SequenceCount $assistant.products
        if ($assistantResultCount -eq 0) {
            Write-Warning "Assistant returned no products. Continuing because this helper validates telemetry/outcome attribution rather than AI ranking."
        }

        Record-GrowthEvent -Headers $buyerHeaders -EventType "AssistantSearchSubmitted" -SourceTool "Assistant" -ProductId $null -ResultCount $assistantResultCount -ConfidenceBand "High" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/assistant" | Out-Null
        Record-GrowthEvent -Headers $buyerHeaders -EventType "AssistantProductOpened" -SourceTool "Assistant" -ProductId $productId -ResultCount $assistantResultCount -ConfidenceBand "High" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/assistant" | Out-Null
        Record-GrowthEvent -Headers $buyerHeaders -EventType "AssistantShopHandoff" -SourceTool "Assistant" -ProductId $null -ResultCount $assistantResultCount -ConfidenceBand "High" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/assistant" | Out-Null

        $runs += Complete-AttributedOrder -BuyerHeaders $buyerHeaders -Product $product -Variant $variant -SourceTool "Assistant"
    }

    if (-not $AssistantOnly) {
        $visual = Invoke-Api -Method "Post" -Path "/api/buyer/ai/visual-search" -Headers $buyerHeaders -Body @{
            imageReference = "rose linen midi dress with soft tailored shape"
            imageDataBase64 = $null
            fileName = $null
            contentType = $null
        }
        $visualResultCount = Get-SequenceCount $visual.products
        if ($visualResultCount -eq 0) {
            Write-Warning "Visual search returned no products. Continuing because this helper validates telemetry/outcome attribution rather than AI ranking."
        }

        Record-GrowthEvent -Headers $buyerHeaders -EventType "VisualSearchSubmitted" -SourceTool "VisualSearch" -ProductId $null -ResultCount $visualResultCount -ConfidenceBand "Medium" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/visual-search" | Out-Null
        Record-GrowthEvent -Headers $buyerHeaders -EventType "VisualProductOpened" -SourceTool "VisualSearch" -ProductId $productId -ResultCount $visualResultCount -ConfidenceBand "Medium" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/visual-search" | Out-Null
        Record-GrowthEvent -Headers $buyerHeaders -EventType "VisualShopHandoff" -SourceTool "VisualSearch" -ProductId $null -ResultCount $visualResultCount -ConfidenceBand "Medium" -Category $category -Colour $colour -Material "Linen" -SourceRoute "/visual-search" | Out-Null

        $runs += Complete-AttributedOrder -BuyerHeaders $buyerHeaders -Product $product -Variant $variant -SourceTool "VisualSearch"
    }

    $fromUtc = [Uri]::EscapeDataString(([DateTimeOffset]::UtcNow.AddDays(-1)).ToString("o"))
    $toUtc = [Uri]::EscapeDataString(([DateTimeOffset]::UtcNow.AddDays(1)).ToString("o"))
    $report = Invoke-Api -Method "Get" -Path "/api/admin/reports/buyer-growth?fromUtc=$fromUtc&toUtc=$toUtc&bucket=Day" -Headers $adminHeaders

    Write-Host ""
    Write-Host "Buyer AI attribution demo complete."
    foreach ($run in $runs) {
        Write-Host ""
        Write-Host "$($run.SourceTool) order:"
        Write-Host "  Cart ID:            $($run.CartId)"
        Write-Host "  Order ID:           $($run.OrderId)"
        Write-Host "  Payment ID:         $($run.PaymentId)"
        Write-Host "  Provider reference: $($run.ProviderReference)"
        Write-Host "  Webhook event ID:   $($run.WebhookEventId)"
        Write-Host "  Final status:       $($run.FinalStatus)"
    }

    Write-Host ""
    Write-Host "Buyer growth outcome summary from admin report:"
    Write-Host "  Product opens:      $($report.outcomeSummary.productOpenedCount)"
    Write-Host "  Cart adds:          $($report.outcomeSummary.addToCartCount)"
    Write-Host "  Checkout starts:    $($report.outcomeSummary.checkoutStartedCount)"
    Write-Host "  Orders created:     $($report.outcomeSummary.orderCreatedCount)"
    Write-Host "  Paid orders:        $($report.outcomeSummary.paidOrderCount)"
    if ($report.outcomeSourceToolBreakdown) {
        Write-Host ""
        Write-Host "Buyer growth outcome source rows:"
        foreach ($row in $report.outcomeSourceToolBreakdown) {
            Write-Host "  $($row.name): opens=$($row.productOpenedCount), cart=$($row.addToCartCount), checkout=$($row.checkoutStartedCount), orders=$($row.orderCreatedCount), paid=$($row.paidOrderCount)"
        }
    }
    Write-Host ""
    Write-Host "Admin report URL:"
    Write-Host "  $FrontendBaseUrl/admin/reports"
    Write-Host ""
    Write-Host "The helper used only public/buyer/admin APIs and did not write attribution rows directly."
}
catch {
    Write-Error $_
    exit 1
}

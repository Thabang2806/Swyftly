param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$ApiBaseUrl = "https://localhost:7268",
    [string]$FrontendBaseUrl = "http://localhost:4200",
    [string]$BuyerEmail = "buyer@mabuntle.local",
    [string]$SellerEmail = "seller@mabuntle.local",
    [string]$ProductSlug = "rose-linen-midi-dress",
    [int]$Quantity = 1,
    [int]$ReservationMinutes = 15,
    [string]$WebhookSigningSecret = "development-only-webhook-secret-change-before-production",
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

$script:ApiBaseUrl = Normalize-BaseUrl $ApiBaseUrl
$FrontendBaseUrl = Normalize-BaseUrl $FrontendBaseUrl

try {
    Write-Host "Creating buyer post-purchase demo state against $script:ApiBaseUrl"

    $buyerHeaders = Get-AuthHeader -Email $BuyerEmail
    $product = Invoke-Api -Method "Get" -Path "/api/products/$ProductSlug"
    $variant = @($product.variants | Where-Object { $_.inStock }) | Select-Object -First 1
    if ($null -eq $variant) {
        throw "Product $ProductSlug does not have an in-stock variant."
    }

    $cart = Invoke-Api -Method "Post" -Path "/api/cart/items" -Headers $buyerHeaders -Body @{
        productVariantId = $variant.variantId
        quantity = $Quantity
    }

    $deliveryAddress = @{
        recipientName = "Demo Buyer"
        phoneNumber = "+27110000000"
        addressLine1 = "10 Market Street"
        addressLine2 = $null
        suburb = "Rosebank"
        city = "Johannesburg"
        province = "Gauteng"
        postalCode = "2196"
        countryCode = "ZA"
        deliveryInstructions = "Buyer post-purchase demo order."
    }

    $shippingOptions = Invoke-Api -Method "Post" -Path "/api/cart/shipping-options" -Headers $buyerHeaders -Body @{
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

    $order = Invoke-Api -Method "Post" -Path "/api/orders/from-cart" -Headers $buyerHeaders -Body @{
        cartId = $cart.cartId
        reservationMinutes = $ReservationMinutes
        deliveryAddressId = $null
        deliveryAddress = $deliveryAddress
        deliveryMethodId = $selectedShipping.deliveryMethodId
        pickupPointId = $pickupPointId
    }

    $payment = Invoke-Api -Method "Post" -Path "/api/payments/initiate" -Headers $buyerHeaders -Body @{
        orderId = $order.orderId
    }

    if ($payment.provider -ne "Fake") {
        throw "Expected Fake payment provider for this local helper, but received $($payment.provider)."
    }

    if ([string]::IsNullOrWhiteSpace($payment.providerReference)) {
        throw "Payment $($payment.paymentId) did not return a provider reference."
    }

    $eventId = "fake_demo_$((New-Guid).ToString('N'))"
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

    $paidOrder = Wait-ForOrderStatus -Headers $buyerHeaders -OrderId $order.orderId -ExpectedStatus "Paid"

    $sellerHeaders = Get-AuthHeader -Email $SellerEmail
    $fulfilledOrder = Invoke-Api -Method "Post" -Path "/api/seller/orders/$($order.orderId)/mark-processing" -Headers $sellerHeaders -Body @{}
    $fulfilledOrder = Invoke-Api -Method "Post" -Path "/api/seller/orders/$($order.orderId)/mark-ready-to-ship" -Headers $sellerHeaders -Body @{}
    $fulfilledOrder = Invoke-Api -Method "Post" -Path "/api/seller/orders/$($order.orderId)/tracking" -Headers $sellerHeaders -Body @{
        carrierName = "Demo Courier"
        trackingNumber = "DEMO-$($order.orderId.Substring(0, 8).ToUpperInvariant())"
        trackingUrl = "$FrontendBaseUrl/fake-tracking/DEMO-$($order.orderId.Substring(0, 8).ToUpperInvariant())"
        note = "Created by buyer post-purchase demo helper."
    }
    $fulfilledOrder = Invoke-Api -Method "Post" -Path "/api/seller/orders/$($order.orderId)/mark-shipped" -Headers $sellerHeaders -Body @{}
    $fulfilledOrder = Invoke-Api -Method "Post" -Path "/api/seller/orders/$($order.orderId)/mark-delivered" -Headers $sellerHeaders -Body @{}

    if ($fulfilledOrder.status -ne "Delivered") {
        throw "Seller fulfilment completed without reaching Delivered. Current status: $($fulfilledOrder.status)"
    }

    Write-Host ""
    Write-Host "Buyer post-purchase demo order created."
    Write-Host "Order ID:              $($order.orderId)"
    Write-Host "Payment ID:            $($payment.paymentId)"
    Write-Host "Provider reference:    $($payment.providerReference)"
    Write-Host "Webhook event ID:      $($webhookResult.providerEventId)"
    Write-Host "Final order status:    $($fulfilledOrder.status)"
    Write-Host "Product:               $($product.product.title)"
    Write-Host "Variant:               $($variant.size) / $($variant.colour)"
    Write-Host ""
    Write-Host "Buyer URLs:"
    Write-Host "  $FrontendBaseUrl/account/orders"
    Write-Host "  $FrontendBaseUrl/account/orders/$($order.orderId)"
    Write-Host "  $FrontendBaseUrl/account/returns"
    Write-Host "  $FrontendBaseUrl/account/reviews"
    Write-Host "  $FrontendBaseUrl/account/notifications"
    Write-Host "  $FrontendBaseUrl/account/support"
    Write-Host "  $FrontendBaseUrl/account/disputes"
    Write-Host ""
    Write-Host "Seller URL:"
    Write-Host "  $FrontendBaseUrl/seller/orders/$($order.orderId)"
    Write-Host ""
    Write-Host "Use the buyer order detail page to create a return request and submit a verified-purchase review."
}
catch {
    Write-Error $_
    exit 1
}

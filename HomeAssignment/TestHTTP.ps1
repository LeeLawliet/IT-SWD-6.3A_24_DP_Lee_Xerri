# Localhost Base URLs
$customerUrl = "https://localhost:44386/api/User"
$bookingUrl = "https://localhost:44325/api/Booking"
$paymentUrl = "https://localhost:44374/api/Payment"
$locationUrl = "https://localhost:44377/api/Location"

# 1. Register New Account
Write-Host "==== STEP 1: Registering New User ===="
$registerBody = '{
  "username": "testuser",
  "email": "xyxyxaaa@example.com",
  "password": "password"
}'
$registerResponse = Invoke-RestMethod -Method POST -Uri "$customerUrl/register" -ContentType "application/json" -Body $registerBody
$registerResponse | ConvertTo-Json -Depth 10
$uid = $registerResponse.uid
Write-Host "Registered UID: $uid"
Start-Sleep -Seconds 1

# 2. Login
Write-Host "`n==== STEP 2: Logging In ===="
$loginBody = '{
  "email": "xyxyxaaa@example.com",
  "password": "password"
}'
$loginResponse = Invoke-RestMethod -Method POST -Uri "$customerUrl/login" -ContentType "application/json" -Body $loginBody
$token = $loginResponse.idToken
Write-Host "Token received."
$header = @{ Authorization = "Bearer $token" }
Start-Sleep -Seconds 1

# 3. Create Booking x3
For ($i = 1; $i -le 3; $i++) {
    Write-Host "`n==== STEP 3. Creating Booking $i ===="
    $createBooking = '{
      "startLocation": "Valletta",
      "endLocation": "Sliema",
      "passengers": 4,
      "cabType": "Premium"
    }'
    $createResponse = Invoke-RestMethod -Method POST -Uri "$bookingUrl" -ContentType "application/json" -Headers $header -Body $createBooking
    $bookingId = $createResponse.id
    Write-Host "Booking ID: $bookingId"
    Start-Sleep -Seconds 1

    if ($i -eq 3)
    {
      Start-Sleep -Seconds 5
      # 4. Show Inbox Notifications
      Write-Host "`n==== Retrieving Discount Notification ===="
      $notifications = Invoke-RestMethod -Method GET -Uri "$customerUrl/$uid/notifications" -Headers $header
      $notifications | ConvertTo-Json -Depth 10
      Start-Sleep -Seconds 2
    }

    Write-Host "`n==== STEP 4. Paying Booking $i ===="
    $payBooking = "{ `"bookingId`": `"$bookingId`" }"
    $payResponse = Invoke-RestMethod -Method POST -Uri "$paymentUrl/pay" -ContentType "application/json" -Headers $header -Body $payBooking
    $payResponse | ConvertTo-Json -Depth 10
    Start-Sleep -Seconds 2
}

# 4. Show Inbox Notifications
Write-Host "`n==== STEP 5: Retrieving Notifications ===="
$notifications = Invoke-RestMethod -Method GET -Uri "$customerUrl/$uid/notifications" -Headers $header
$notifications | ConvertTo-Json -Depth 10
Start-Sleep -Seconds 2

# 5. Add Favorite Location
Write-Host "`n==== STEP 6: Add Favorite Location ===="
$favLocation = '{
  "name": "Fgura"
}'
$favResponse = Invoke-RestMethod -Method POST -Uri "$locationUrl" -ContentType "application/json" -Headers $header -Body $favLocation
$favResponse | ConvertTo-Json -Depth 10
$favId = $favResponse.id
Start-Sleep -Seconds 1

# Retrieve Favorite Location
$getFavLocationResponse = Invoke-RestMethod -Method GET -Uri "$locationUrl/$favId" -ContentType "application/json" -Headers $header
$getFavLocationResponse | ConvertTo-Json -Depth 10

# 6. Update Favorite Location
Write-Host "`n==== STEP 7: Update Favorite Location ===="
$updateLocation = '{
  "name": "Il-Marsa"
}'
Invoke-RestMethod -Method PUT -Uri "$locationUrl/$favId" -ContentType "application/json" -Headers $header -Body $updateLocation
Write-Host "Updated location ID: $favId"

# Retrieve Favorite Location Again
$getFavLocationResponse = Invoke-RestMethod -Method GET -Uri "$locationUrl/$favId" -ContentType "application/json" -Headers $header
$getFavLocationResponse | ConvertTo-Json -Depth 10

# Retrieve Favorite Location Weather
$getFavLocationResponse = Invoke-RestMethod -Method GET -Uri "$locationUrl/$favId/weather" -ContentType "application/json" -Headers $header
$getFavLocationResponse | ConvertTo-Json -Depth 10
Start-Sleep -Seconds 1

# 7. Delete Favorite Location
Write-Host "`n==== STEP 8 Delete Favorite Location ===="
Invoke-RestMethod -Method DELETE -Uri "$locationUrl/$favId" -Headers $header
Write-Host "Deleted location ID: $favId"

# 8. Retrieve all favorites
$getFavLocationResponse = Invoke-RestMethod -Method GET -Uri "$locationUrl" -ContentType "application/json" -Headers $header
$getFavLocationResponse | ConvertTo-Json -Depth 10
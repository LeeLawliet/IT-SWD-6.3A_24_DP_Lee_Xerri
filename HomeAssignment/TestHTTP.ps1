$loginBody = '{
"email": "a@a.com",
"password": "password"}'

$loginResponse = Invoke-RestMethod -Method POST -Uri "https://localhost:44305/customer/login" -ContentType "application/json" -Body $loginBody

write-host($loginResponse.IdToken)

$header = @{
    Authorization = "Bearer $($loginResponse.IdToken)"
}

$payBooking = '
{
  "bookingId": "9c8f37f6-dc36-43d0-a22f-f7db17b54452"
}
'
$payResponse = Invoke-RestMethod -Method POST -Uri "https://localhost:44374/api/Payment/pay" -ContentType "application/json" -Headers $header -Body $payBooking

$payResponse = Invoke-RestMethod -Method POST -Uri "https://localhost:44305/payment/pay" -ContentType "application/json" -Headers $header -Body $payBooking

write-host($payResponse)
# $createBooking = '{
#   "startLocation": "Paola",
#   "endLocation": "Fgura",
#   "passengers": 5,
#   "cabType": "Premium"
# }'

# $createResponse = Invoke-RestMethod -Method POST -Uri "https://localhost:44305/booking" -ContentType "application/json" -Headers $header -Body $createBooking

# write-host($createResponse)

# $getPastBooking = Invoke-RestMethod -Method GET -Uri "https://localhost:44305/booking/past" -ContentType "application/json" -Headers $header

# $getPastBooking | ConvertTo-Json -Depth 10

# $bookingId = "0d50ddc4-54da-4f50-805c-e086021d7200"
# $body = '{
# "bookingId": "0d50ddc4-54da-4f50-805c-e086021d7200"
# }'

# $getBookingById = Invoke-RestMethod -Method GET -Uri "https://localhost:44305/booking/0d50ddc4-54da-4f50-805c-e086021d7200" -ContentType "application/json" -Headers $header 

# $getBookingById | ConvertTo-Json -Depth 10
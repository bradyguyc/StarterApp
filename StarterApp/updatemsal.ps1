# Define the paths to the files
$appSettingsPath = "appsettings.json"
$msalActivityPath = "Platforms/Android/MsalActivity.cs"
$androidManifestPath = "Platforms/Android/AndroidManifest.xml"


# Check if the appsettings.json file exists
if (-Not (Test-Path -Path $appSettingsPath)) {
    Write-Host "Error: appsettings.json file not found at path: $appSettingsPath"
    exit 1
}

# Read the appsettings.json file
$appSettings = Get-Content -Raw -Path $appSettingsPath | ConvertFrom-Json

# Extract the ClientId
$clientId = $appSettings.AzureAD.ClientId

# Check if the ClientId is not null or empty
if ([string]::IsNullOrEmpty($clientId)) {
    Write-Host "Error: ClientId not found in appsettings.json"
    exit 1
}

# Read the MsalActivity.cs file
$msalActivityContent = Get-Content -Path $msalActivityPath

# Define the new DataScheme value
$newDataScheme = "msal" + $clientId

# Replace the DataScheme value in the MsalActivity.cs file
$updatedMsalActivityContent = $msalActivityContent -replace 'DataScheme = "msal[^"]*"', "DataScheme = `"$newDataScheme`""

# Write the updated content back to the MsalActivity.cs file
Set-Content -Path $msalActivityPath -Value $updatedMsalActivityContent

Write-Host "MsalActivity.cs has been updated with the new DataScheme value: $newDataScheme"

# Check if the AndroidManifest.xml file exists
if (-Not (Test-Path -Path $androidManifestPath)) {
    Write-Host "Error: AndroidManifest.xml file not found at path: $androidManifestPath"
    exit 1
}

# Read the AndroidManifest.xml file
$androidManifestContent = Get-Content -Path $androidManifestPath

# Replace the android:scheme value in the AndroidManifest.xml file
$updatedAndroidManifestContent = $androidManifestContent -replace 'android:scheme="msal[^"]*"', "android:scheme=`"$newDataScheme`""

# Write the updated content back to the AndroidManifest.xml file
Set-Content -Path $androidManifestPath -Value $updatedAndroidManifestContent

Write-Host "AndroidManifest.xml has been updated with the new android:scheme value: $newDataScheme"





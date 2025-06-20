Write-Host "Attempting to stop .NET and MSBuild processes that might be locking files..."
Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
Stop-Process -Name MSBuild -Force -ErrorAction SilentlyContinue

# Give the processes a moment to terminate
Start-Sleep -Seconds 2

Write-Host "Attempting to delete bin and obj folders..."
Get-ChildItem -Path . -Include bin,obj -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Removing $_.FullName"
    Remove-Item -Recurse -Force -Path $_.FullName -ErrorAction Continue
}

Write-Host "Cleanup finished. If you still see errors about locked files, please close Visual Studio and run this script again from an external PowerShell terminal."

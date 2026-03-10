# Publish & Build installers for all architectures
# Run: .\publish-all.ps1

$archs = @("x64", "x86", "arm64")

Write-Host "=== Publishing GuessWho ===" -ForegroundColor Cyan

foreach ($arch in $archs) {
    Write-Host "`n--- Publishing win-$arch ---" -ForegroundColor Yellow
    dotnet publish -c Release -r "win-$arch" --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "bin\publish\win-$arch"
}

# Check if Inno Setup compiler is available
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Write-Host "`nPliki opublikowane do bin\publish\win-*" -ForegroundColor Green
    Write-Host "Inno Setup nie znaleziony. Skompiluj recznie:" -ForegroundColor Yellow
    foreach ($arch in $archs) {
        Write-Host "  iscc /DArch=$arch setup.iss"
    }
    exit
}

Write-Host "`n=== Building installers ===" -ForegroundColor Cyan
foreach ($arch in $archs) {
    Write-Host "`n--- Installer win-$arch ---" -ForegroundColor Yellow
    & $iscc "/DArch=$arch" "setup.iss"
}

Write-Host "`n=== Done! ===" -ForegroundColor Green
Get-ChildItem installer\*.exe | ForEach-Object {
    Write-Host "  $($_.Name) - $([math]::Round($_.Length/1MB,1)) MB"
}

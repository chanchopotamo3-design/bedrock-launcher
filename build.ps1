# Compila el launcher, ejecuta pruebas y genera el .exe + instalador. Ejecutar en PowerShell, en Windows 10/11.
# Requisitos: .NET 8 SDK (https://dotnet.microsoft.com/download) e Inno Setup 6 (opcional, para el instalador).
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==> Pruebas" -ForegroundColor Cyan
dotnet test tests/BedrockLauncher.Tests/BedrockLauncher.Tests.csproj -c Release

Write-Host "==> Publicando .exe (autocontenido, no requiere instalar .NET)" -ForegroundColor Cyan
dotnet publish src/BedrockLauncher/BedrockLauncher.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/app

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
  Write-Host "==> Generando instalador" -ForegroundColor Cyan
  & $iscc installer/installer.iss
  Write-Host "Instalador: dist\BedrockUltimateLauncher-Setup.exe" -ForegroundColor Green
} else {
  Write-Host "Inno Setup 6 no encontrado: se omite el instalador. Ejecutable listo en dist\app\BedrockUltimateLauncher.exe" -ForegroundColor Yellow
}

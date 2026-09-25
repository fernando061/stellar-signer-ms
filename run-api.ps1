[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configurationPath = Join-Path $PSScriptRoot "StellarSigner.Api\appsettings.Local.json"
if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
    throw "No se encontró '$configurationPath'. Copia appsettings.Local.example.json y completa sus valores."
}

$configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($configuration.ConnectionStrings.SignerDb)) {
    throw "ConnectionStrings:SignerDb es obligatorio en appsettings.Local.json."
}
if ([string]::IsNullOrWhiteSpace($configuration.MasterKey.WrapKey) -or
    [string]::IsNullOrWhiteSpace($configuration.MasterKey.FilePath)) {
    throw "MasterKey:WrapKey y MasterKey:FilePath son obligatorios en appsettings.Local.json."
}

$wrapKeyBytes = $null
try {
    $wrapKeyBytes = [Convert]::FromBase64String($configuration.MasterKey.WrapKey)
    if ($wrapKeyBytes.Length -ne 32) { throw "MasterKey:WrapKey debe representar exactamente 32 bytes." }
}
catch [FormatException] {
    throw "MasterKey:WrapKey debe ser base64 válido."
}
finally {
    if ($null -ne $wrapKeyBytes) { [Array]::Clear($wrapKeyBytes, 0, $wrapKeyBytes.Length) }
}

$masterKeyFile = $configuration.MasterKey.FilePath
if (-not [IO.Path]::IsPathRooted($masterKeyFile)) {
    $masterKeyFile = Join-Path (Split-Path $configurationPath) $masterKeyFile
}
$masterKeyFile = [IO.Path]::GetFullPath($masterKeyFile)
if (-not (Test-Path -LiteralPath $masterKeyFile -PathType Leaf)) {
    throw "No existe el payload maestro '$masterKeyFile'."
}

Write-Host "Configuración válida: StellarSigner.Api/appsettings.Local.json"
if ($ValidateOnly) { return }

$dotnetArguments = @("run", "--project", "StellarSigner.Api/StellarSigner.Api.csproj")
if ($NoBuild) { $dotnetArguments += "--no-build" }

Push-Location $PSScriptRoot
try {
    & dotnet @dotnetArguments
    if ($LASTEXITCODE -ne 0) { throw "La API terminó con el código $LASTEXITCODE." }
}
finally {
    Pop-Location
}

[CmdletBinding()]
param(
    [string]$EnvironmentFile = (Join-Path $PSScriptRoot ".env"),
    [string]$Urls = "http://127.0.0.1:5294",
    [switch]$NoBuild,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Import-DotEnv {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "No se encontró el archivo de entorno '$Path'. Copia .env.example a .env y completa sus valores."
    }

    foreach ($line in [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $Path))) {
        $entry = $line.Trim()
        if ($entry.Length -eq 0 -or $entry.StartsWith("#")) {
            continue
        }
        if ($entry.StartsWith("export ", [StringComparison]::OrdinalIgnoreCase)) {
            $entry = $entry.Substring(7).TrimStart()
        }

        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) {
            throw "Entrada inválida en '$Path': cada línea debe tener el formato NOMBRE=valor."
        }

        $name = $entry.Substring(0, $separator).Trim()
        $value = $entry.Substring($separator + 1).Trim()
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
            throw "Nombre de variable inválido en '$Path': '$name'."
        }
        if ($value.Length -ge 2 -and (($value[0] -eq '"' -and $value[-1] -eq '"') -or
                ($value[0] -eq "'" -and $value[-1] -eq "'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [Environment]::SetEnvironmentVariable($name, $value, [EnvironmentVariableTarget]::Process)
    }
}

function Get-RequiredEnvironmentValue {
    param([Parameter(Mandatory)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name, [EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "La variable obligatoria '$Name' no tiene valor en '$EnvironmentFile'."
    }
    return $value
}

function Set-ProcessEnvironmentValue {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value
    )

    [Environment]::SetEnvironmentVariable($Name, $Value, [EnvironmentVariableTarget]::Process)
}

Import-DotEnv -Path $EnvironmentFile

$databasePassword = Get-RequiredEnvironmentValue "SIGNER_DB_PASSWORD"
$wrapKey = Get-RequiredEnvironmentValue "SIGNER_WRAP_KEY"
$stellarIssuer = Get-RequiredEnvironmentValue "STELLAR_ISSUER"
$stellarContractId = Get-RequiredEnvironmentValue "STELLAR_CONTRACT_ID"
$jwtIssuer = Get-RequiredEnvironmentValue "JWT_ISSUER"
$jwtAudience = Get-RequiredEnvironmentValue "JWT_AUDIENCE"
$jwtSigningKey = Get-RequiredEnvironmentValue "JWT_SIGNING_KEY"
$jwtClientId = Get-RequiredEnvironmentValue "JWT_ALLOWED_CLIENT_ID"

$wrapKeyBytes = $null
try {
    $wrapKeyBytes = [Convert]::FromBase64String($wrapKey)
    if ($wrapKeyBytes.Length -ne 32) {
        throw "SIGNER_WRAP_KEY debe contener exactamente 32 bytes codificados en base64."
    }
}
catch [FormatException] {
    throw "SIGNER_WRAP_KEY debe ser un valor base64 válido de 32 bytes."
}
finally {
    if ($null -ne $wrapKeyBytes) {
        [Array]::Clear($wrapKeyBytes, 0, $wrapKeyBytes.Length)
    }
}

if ($jwtSigningKey.Trim().Length -lt 32) {
    throw "JWT_SIGNING_KEY debe tener al menos 32 caracteres."
}

$masterKeyFile = [Environment]::GetEnvironmentVariable("SIGNER_MASTER_KEY_FILE", [EnvironmentVariableTarget]::Process)
if ([string]::IsNullOrWhiteSpace($masterKeyFile)) {
    $masterKeyFile = Join-Path $PSScriptRoot "private\master.enc"
}
elseif (-not [IO.Path]::IsPathRooted($masterKeyFile)) {
    $masterKeyFile = Join-Path $PSScriptRoot $masterKeyFile
}
$masterKeyFile = [IO.Path]::GetFullPath($masterKeyFile)

if (-not (Test-Path -LiteralPath $masterKeyFile -PathType Leaf)) {
    throw "No existe el payload maestro '$masterKeyFile'. Configura SIGNER_MASTER_KEY_FILE y ejecuta primero: dotnet run --project StellarSigner.Bootstrap/StellarSigner.Bootstrap.csproj"
}

$maxAmount = [Environment]::GetEnvironmentVariable("STELLAR_MAX_AMOUNT", [EnvironmentVariableTarget]::Process)
if ([string]::IsNullOrWhiteSpace($maxAmount)) { $maxAmount = "10000" }
$maxRemainingSeconds = [Environment]::GetEnvironmentVariable("STELLAR_MAX_REMAINING_SECONDS", [EnvironmentVariableTarget]::Process)
if ([string]::IsNullOrWhiteSpace($maxRemainingSeconds)) { $maxRemainingSeconds = "3600" }

Set-ProcessEnvironmentValue "ASPNETCORE_ENVIRONMENT" "Development"
Set-ProcessEnvironmentValue "ASPNETCORE_URLS" $Urls
Set-ProcessEnvironmentValue "ConnectionStrings__SignerDb" "Host=localhost;Port=54329;Database=stellar_signer;Username=signer;Password=$databasePassword"
Set-ProcessEnvironmentValue "SIGNER_MASTER_KEY_FILE" $masterKeyFile
Set-ProcessEnvironmentValue "Stellar__Issuer" $stellarIssuer
Set-ProcessEnvironmentValue "Stellar__ContractId" $stellarContractId
Set-ProcessEnvironmentValue "Stellar__NetworkPassphrase" "Test SDF Network ; September 2015"
Set-ProcessEnvironmentValue "Stellar__MaxAmount" $maxAmount
Set-ProcessEnvironmentValue "Stellar__MaxOperations" "1"
Set-ProcessEnvironmentValue "Stellar__MaxRemainingSeconds" $maxRemainingSeconds
Set-ProcessEnvironmentValue "Jwt__Issuer" $jwtIssuer
Set-ProcessEnvironmentValue "Jwt__Audience" $jwtAudience
Set-ProcessEnvironmentValue "Jwt__SigningKey" $jwtSigningKey
Set-ProcessEnvironmentValue "Jwt__AllowedClientId" $jwtClientId
Set-ProcessEnvironmentValue "Jwt__RequiredScope" "stellar-signer.execute"
Set-ProcessEnvironmentValue "RateLimit__PermitLimit" "30"

Write-Host "Configuración local válida. API: $Urls"
Write-Host "PostgreSQL: localhost:54329/stellar_signer"
Write-Host "Cliente JWT permitido: $jwtClientId"

if ($ValidateOnly) {
    return
}

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No se encontró dotnet en PATH. Instala el SDK de .NET 9."
}

$dotnetArguments = @("run", "--project", "StellarSigner.Api/StellarSigner.Api.csproj")
if ($NoBuild) {
    $dotnetArguments += "--no-build"
}

Push-Location $PSScriptRoot
try {
    & dotnet @dotnetArguments
    if ($LASTEXITCODE -ne 0) {
        throw "La API terminó con el código $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

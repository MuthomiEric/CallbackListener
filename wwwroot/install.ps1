param(
    [string]$ApiKey = "",
    [string]$Server = "https://callback.erickmuthomi.dev"
)

if (-not $ApiKey) {
    Write-Error "ApiKey is required.`nUsage: & ([scriptblock]::Create((irm $Server/install.ps1))) -ApiKey YOUR_KEY"
    exit 1
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator (right-click PowerShell → Run as Administrator)."
    exit 1
}

$arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLower()
$archSlug = switch ($arch) {
    "x64"   { "x64" }
    "arm64" { "arm64" }
    default { Write-Error "Unsupported architecture: $arch. Download manually from $Server/downloads/"; exit 1 }
}

$binaryUrl  = "$Server/downloads/CallbackAgent-win-$archSlug.exe"
$installDir = "$env:ProgramData\CallbackAgent"
$exePath    = "$installDir\CallbackAgent.exe"

Write-Host "Downloading CallbackAgent (win-$archSlug)..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
try {
    Invoke-WebRequest -Uri $binaryUrl -OutFile $exePath -UseBasicParsing
} catch {
    Write-Error "Download failed: $_`nVisit $Server/downloads/ for manual installation."
    exit 1
}

Write-Host "Installing service..."
& $exePath install --server $Server --api-key $ApiKey

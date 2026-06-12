param(
    [switch]$SkipPythonCheck,
    [switch]$SkipCodegen,
    [switch]$SkipDotnetBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $PSScriptRoot
$ConfigDir = Join-Path $Root "public\config\client"
$UnityDir = Join-Path $Root "client\UnityClient"
$GeneratedRootDir = Join-Path $UnityDir "Assets\Scripts\Generated"
$RuntimeConfigDir = Join-Path $UnityDir "Assets\StreamingAssets\Config"
$GeneratedConfigDir = Join-Path $GeneratedRootDir "Data"
$ProtoDir = Join-Path $Root "public\proto"
$ProtoOutputDir = Join-Path $GeneratedRootDir "Network\Protobuf"
$HandlerDir = Join-Path $GeneratedRootDir "Network\Handlers"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Require-Command($Name, $InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found. $InstallHint"
    }
}

Write-Host "Game client framework initialization"
Write-Host "Root: $Root"

Write-Step "Ensuring project directories"
$dirs = @(
    $ConfigDir,
    $RuntimeConfigDir,
    $GeneratedConfigDir,
    $ProtoOutputDir,
    (Join-Path $ProtoOutputDir "Generated"),
    $HandlerDir,
    (Join-Path $UnityDir "Assets"),
    (Join-Path $UnityDir "Assets\Scripts"),
    $GeneratedRootDir,
    (Join-Path $UnityDir "ProjectSettings"),
    (Join-Path $UnityDir "Packages")
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

if (-not $SkipPythonCheck) {
    Write-Step "Checking Python toolchain"
    Require-Command "python" "Install Python 3.11+ and add it to PATH."
    python --version
    python -c "import openpyxl; import google.protobuf" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Missing Python packages. Run: pip install -r public/tools/requirements.txt"
    }
}

if (-not $SkipCodegen) {
    Write-Step "Validating config tables"
    python (Join-Path $Root "public\tools\data\validators.py") --config-dir $ConfigDir

    Write-Step "Generating config C# code"
    Get-ChildItem -Path $ConfigDir -Filter *.xlsx -File | ForEach-Object {
        python (Join-Path $Root "public\tools\codegen\config_codegen.py") `
            --input $_.FullName `
            --output-dir $GeneratedConfigDir `
            --target client
    }

    Write-Step "Exporting runtime config data"
    Get-ChildItem -Path $ConfigDir -Filter *.xlsx -File | ForEach-Object {
        python (Join-Path $Root "public\tools\codegen\config_exporter.py") `
            --input $_.FullName `
            --output-dir $RuntimeConfigDir `
            --target client
    }

    Write-Step "Generating protobuf C# code"
    python (Join-Path $Root "public\tools\codegen\proto_codegen.py") `
        --proto-dir $ProtoDir `
        --output-dir $ProtoOutputDir `
        --handler-dir $HandlerDir
}

if (-not $SkipDotnetBuild) {
    Write-Step "Checking .NET SDK"
    Require-Command "dotnet" "Install .NET 8 SDK."
    $sdkVersions = dotnet --list-sdks | ForEach-Object { ($_ -split " ")[0] }
    $hasCompatibleSdk = $false
    foreach ($version in $sdkVersions) {
        $major = 0
        if ([int]::TryParse(($version -split "\.")[0], [ref]$major) -and $major -ge 8) {
            $hasCompatibleSdk = $true
            break
        }
    }

    if (-not $hasCompatibleSdk) {
        Write-Warning ".NET SDK 8 or newer was not found. Skipping build. Install .NET 8 SDK or newer to build client/GameFramework.sln."
    }
    else {
        dotnet build (Join-Path $Root "client\GameFramework.sln")
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }
}

Write-Step "Initialization complete"
Write-Host "Generated config data: $RuntimeConfigDir"
Write-Host "Generated C# code: $GeneratedRootDir"

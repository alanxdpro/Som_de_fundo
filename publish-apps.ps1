$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$userProject = Join-Path $root "SomDeFundoCSharp\SomDeFundoCSharp.csproj"
$adminProject = Join-Path $root "SomDeFundoCSharp\SomDeFundoCSharp.Admin.csproj"

dotnet publish $userProject -c Release -r win-x64 --self-contained false -o (Join-Path $root "publish-user")
dotnet publish $adminProject -c Release -r win-x64 --self-contained false -o (Join-Path $root "publish-admin")

$envPath = Join-Path $root ".env"
if (Test-Path $envPath) {
    Copy-Item $envPath (Join-Path $root "publish-user\.env") -Force
    Copy-Item $envPath (Join-Path $root "publish-admin\.env") -Force
}

Write-Host "Gerado: publish-user\Som de Fundo Pro.exe"
Write-Host "Gerado: publish-admin\Som de Fundo Pro Admin.exe"

# Lança uma versão do 2D Editor: compila, empacota, gera o instalador, o
# update.json e (com -Publish) publica a release no GitHub. Os usuários
# recebem o aviso de atualização automaticamente.
#
# 1. Escreva as novidades em releases\<versao>.json (veja releases\1.0.0.json)
# 2. powershell -ExecutionPolicy Bypass -File scripts\release.ps1 -Version 1.0.1 -Publish
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Publish,
    [switch]$Mandatory
)
$ErrorActionPreference = 'Stop'
$root  = Split-Path $PSScriptRoot -Parent
$repo  = 'alanaraujo-bit/2d-editor'
$gh    = Join-Path $root 'tools\gh\bin\gh.exe'
$iscc  = Join-Path $root 'tools\InnoSetup\ISCC.exe'
$dotnet = Join-Path $root 'tools\dotnet\dotnet.exe'
$mingw = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin'
$utf8  = New-Object Text.UTF8Encoding $false

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Versão deve ser no formato 1.2.3" }
$notesFile = Join-Path $root "releases\$Version.json"
if (-not (Test-Path $notesFile)) { throw "Crie primeiro $notesFile com as novidades" }
$notes = [IO.File]::ReadAllText($notesFile, [Text.Encoding]::UTF8) | ConvertFrom-Json

# 1. versão no código
$rh = Join-Path $root 'npp-src\PowerEditor\src\resource.h'
$v = $Version.Split('.')
$t = [IO.File]::ReadAllText($rh)
$t = $t -replace 'BRAND_VERSION L"[^"]+"', "BRAND_VERSION L`"$Version`""
$t = $t -replace 'NOTEPAD_PLUS_VERSION L"2D Editor v[^"]+"', "NOTEPAD_PLUS_VERSION L`"2D Editor v$Version`""
$t = $t -replace 'VERSION_PRODUCT_VALUE L"[^"]+"', "VERSION_PRODUCT_VALUE L`"$Version\0`""
$t = $t -replace 'VERSION_DIGITALVALUE [0-9, ]+', "VERSION_DIGITALVALUE $($v[0]), $($v[1]), $($v[2]), 0"
[IO.File]::WriteAllText($rh, $t, $utf8)

# 2. compilar editor e atualizador
$env:PATH = "$mingw;$env:PATH"
Push-Location (Join-Path $root 'npp-src\PowerEditor\gcc')
(Get-Item ..\src\Notepad_plus.rc).LastWriteTime = Get-Date
mingw32-make -j12 | Out-Null
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Falha ao compilar o editor" }
Pop-Location
& $dotnet build (Join-Path $root 'updater\2DUpdater.csproj') -c Release -o (Join-Path $root 'updater\bin\out') "/p:Version=$Version" -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o atualizador" }

# 3. pacote, instalador
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'package.ps1')
& $iscc /Q "/DAppVersion=$Version" (Join-Path $root 'installer\2DEditor.iss')
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o instalador" }

# 4. update.json
$dist = Join-Path $root 'dist'
$setup = Join-Path $dist '2D-Editor-Setup.exe'
$zip = Join-Path $dist '2D-Editor-Portable.zip'
$base = "https://github.com/$repo/releases/download/v$Version"
$feed = [ordered]@{
    version   = $Version
    date      = (Get-Date -Format 'yyyy-MM-dd')
    headline  = $notes.headline
    mandatory = [bool]$Mandatory
    notes     = @($notes.notes | ForEach-Object { [ordered]@{ type = $_.type; text = $_.text } })
    setup     = [ordered]@{ url = "$base/2D-Editor-Setup.exe"; sha256 = (Get-FileHash $setup).Hash; size = (Get-Item $setup).Length }
    portable  = [ordered]@{ url = "$base/2D-Editor-Portable.zip"; sha256 = (Get-FileHash $zip).Hash; size = (Get-Item $zip).Length }
}
$feedPath = Join-Path $dist 'update.json'
[IO.File]::WriteAllText($feedPath, ($feed | ConvertTo-Json -Depth 5), $utf8)

# 5. texto da release
$labels = @{ novo = 'Novo'; melhoria = 'Melhoria'; correcao = 'Correção' }
$md = "## 2D Editor $Version`n`n$($notes.headline)`n`n"
foreach ($n in $notes.notes) { $k = ($n.type -replace 'ç', 'c').ToLower(); $md += "- **$($labels[$k])** $($n.text)`n" }
$md += "`n### Downloads`n`n| Arquivo | Para quê |`n|---|---|`n| **2D-Editor-Setup.exe** | Instalador do Windows (recomendado) |`n| 2D-Editor-Portable.zip | Versão portátil, sem instalar |`n`nQuem já usa o 2D Editor recebe esta versão automaticamente."
$mdPath = Join-Path $dist 'release-notes.md'
[IO.File]::WriteAllText($mdPath, $md, $utf8)

Write-Output "Pronto: $setup"
Write-Output "        $zip"
Write-Output "        $feedPath"

if ($Publish) {
    # sincroniza a pasta 2d/ do repositório com o ambiente de trabalho
    $r2d = Join-Path $root 'npp-src\2d'
    foreach ($d in 'updater', 'installer', 'brand', 'releases') {
        $dst = Join-Path $r2d $d
        if (Test-Path $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
        robocopy (Join-Path $root $d) $dst /E /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    }
    New-Item -ItemType Directory -Force (Join-Path $r2d 'scripts') | Out-Null
    foreach ($f in 'package.ps1', 'release.ps1', 'mkwizard.py') { Copy-Item (Join-Path $PSScriptRoot $f) (Join-Path $r2d 'scripts') -Force }
    Push-Location (Join-Path $root 'npp-src')
    git add -A | Out-Null
    git -c "user.name=Alan Araújo" -c user.email=199187659+alanaraujo-bit@users.noreply.github.com commit -q -m "2D Editor $Version" 2>$null
    $helper = '!' + ($gh -replace '\\', '/') + ' auth git-credential'
    git -c credential.helper= -c "credential.https://github.com.helper=$helper" push -q github HEAD:main
    Pop-Location
    & $gh release create "v$Version" --repo $repo --target main --title "2D Editor $Version" --notes-file $mdPath --latest $setup $zip $feedPath
    Write-Output "Publicado: https://github.com/$repo/releases/tag/v$Version"
}

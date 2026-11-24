# Empacota o 2D Editor (versão portátil + pasta usada pelo instalador).
# Uso:  powershell -ExecutionPolicy Bypass -File scripts\package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin  = Join-Path $root 'npp-src\PowerEditor\gcc\bin.gcc.x86_64'
$upd  = Join-Path $root 'updater\bin\out'
$dist = Join-Path $root 'dist\2D Editor'
$utf8 = New-Object Text.UTF8Encoding $true

if (-not (Test-Path (Join-Path $bin '2DEditor.exe'))) { throw "Compile primeiro: $bin\2DEditor.exe não encontrado" }
if (-not (Test-Path (Join-Path $upd '2DUpdater.exe'))) { throw "Compile o atualizador primeiro: $upd\2DUpdater.exe" }

$version = ([regex]::Match((Get-Content (Join-Path $root 'npp-src\PowerEditor\src\resource.h') -Raw), 'BRAND_VERSION L"([^"]+)"')).Groups[1].Value

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $bin '*') $dist -Recurse -Force

# Atualização automática ligada: o pacote não leva o arquivo que a desativa
Remove-Item (Join-Path $dist 'disableNppAutoUpdate.xml') -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $dist 'updater') | Out-Null
Copy-Item (Join-Path $upd '2DUpdater.exe') (Join-Path $dist 'updater') -Force
if (Test-Path (Join-Path $upd '2DUpdater.exe.config')) { Copy-Item (Join-Path $upd '2DUpdater.exe.config') (Join-Path $dist 'updater') -Force }

# Idiomas: português do Brasil (padrão), português de Portugal e inglês (reserva)
$keepLang = @('brazilian_portuguese.xml', 'portuguese.xml', 'english.xml')
Get-ChildItem (Join-Path $dist 'localization') -File | Where-Object { $keepLang -notcontains $_.Name } | Remove-Item -Force
Copy-Item (Join-Path $dist 'localization\brazilian_portuguese.xml') (Join-Path $dist 'nativeLang.xml') -Force

# Licença: cabeçalho 2D + texto integral da GNU GPL v3 (obrigatória ao distribuir)
$npl = Get-Content (Join-Path $root 'npp-src\LICENSE') -Encoding UTF8
$start = ($npl | Select-String -Pattern '^GNU GENERAL PUBLIC LICENSE' | Select-Object -First 1).LineNumber - 1
$header = @'
2D Editor
(C) 2026 2D Tecnologia - https://2dtecnologia.com/

Este programa é software livre, distribuído sob a GNU General Public License
versão 3 (texto integral abaixo). Inclui código licenciado sob a GPL,
(C) 1998-2026 Don HO e colaboradores.

Este programa é distribuído na esperança de ser útil, mas SEM NENHUMA
GARANTIA, nem mesmo a garantia implícita de COMERCIALIZAÇÃO ou ADEQUAÇÃO A
UM PROPÓSITO ESPECÍFICO.

O código-fonte está em https://github.com/alanaraujo-bit/2d-editor

******************************************************************


'@
[IO.File]::WriteAllText((Join-Path $dist 'LICENSE'), $header + (($npl[$start..($npl.Count - 1)]) -join "`r`n"), $utf8)

$readme = @"
2D Editor $version
2D Tecnologia - Tecnologia em favor da sua empresa
https://2dtecnologia.com/

Editor de texto e código para Windows: realce de sintaxe para dezenas de
linguagens, abas, busca e substituição com expressões regulares, macros e
muito mais. Atualiza-se sozinho quando sai uma versão nova.

Suporte 2D Tecnologia
  WhatsApp: +55 (31) 99778-8973  -  https://wa.me/5531997788973
  E-mail:   contato@2dtecnologia.com
  Site:     https://2dtecnologia.com/

Versão portátil: as configurações ficam nesta própria pasta (arquivo
doLocalConf.xml). Para guardar as configurações em %APPDATA%\2D Editor,
apague o doLocalConf.xml.

Licença: GNU GPL v3 (arquivo LICENSE).
"@
[IO.File]::WriteAllText((Join-Path $dist 'readme.txt'), $readme, $utf8)

$notesFile = Join-Path $root "releases\$version.json"
$changelog = "2D Editor $version - novidades`r`n`r`n"
if (Test-Path $notesFile) {
    $n = [IO.File]::ReadAllText($notesFile, [Text.Encoding]::UTF8) | ConvertFrom-Json
    $i = 1
    foreach ($item in $n.notes) { $changelog += "$i. $($item.text)`r`n"; $i++ }
}
[IO.File]::WriteAllText((Join-Path $dist 'change.log'), $changelog, $utf8)

$zip = Join-Path $root 'dist\2D-Editor-Portable.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $dist -DestinationPath $zip
Write-Output "Versão: $version"
Write-Output "Pacote: $dist"
Write-Output "Zip:    $zip ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)"

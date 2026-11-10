# ==============================================================================
#  build.ps1 — compile OutilTraductionWings sans Visual Studio
#                (compilateurs .NET Framework livrés avec Windows)
#  Usage :  powershell -ExecutionPolicy Bypass -File .\build.ps1
#  Sortie : .\out\WingsLangCore.dll, .\out\SmokeTest.exe, .\out\WingsLangTool.exe
# ==============================================================================
param()

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path "$fw\csc.exe")) {
    $fw = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
}
$csc  = Join-Path $fw "csc.exe"
$vbc  = Join-Path $fw "vbc.exe"
$out  = Join-Path $root "out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$coreDir = Join-Path $root "WingsLangCore"
$toolDir = Join-Path $root "WingsLangTool"
$testDir = Join-Path $root "tests\SmokeTest"

Write-Host "==> Compilation du noyau (C#) ..."
& $csc /nologo /target:library /codepage:65001 /warn:4 `
    /out:"$out\WingsLangCore.dll" `
    /r:System.dll /r:System.Core.dll /r:System.Xml.dll `
    "$coreDir\Erlang\ErlDocument.cs" `
    "$coreDir\Erlang\ErlEscape.cs" `
    "$coreDir\Erlang\ErlLexer.cs" `
    "$coreDir\Erlang\ErlNode.cs" `
    "$coreDir\Erlang\ErlParser.cs" `
    "$coreDir\Lang\LangComparer.cs" `
    "$coreDir\Lang\LangFile.cs" `
    "$coreDir\Properties\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { Write-Error "Échec compilation noyau."; exit 1 }

Write-Host "==> Compilation du SmokeTest (console) ..."
& $csc /nologo /target:exe /codepage:65001 /warn:4 `
    /out:"$out\SmokeTest.exe" `
    /r:System.dll /r:System.Core.dll /r:"$out\WingsLangCore.dll" `
    "$testDir\Program.cs"
if ($LASTEXITCODE -ne 0) { Write-Error "Échec compilation SmokeTest."; exit 1 }

if (Test-Path (Join-Path $toolDir "frmMain.vb")) {
    Write-Host "==> Compilation de l'outil graphique (VB.NET WinForms) ..."
    & $vbc /nologo /target:winexe /codepage:65001 `
        /out:"$out\WingsLangTool.exe" `
        /r:System.dll /r:System.Core.dll `
        /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        /r:"$fw\Microsoft.VisualBasic.dll" `
        /r:"$out\WingsLangCore.dll" `
        (Join-Path $toolDir "Application.vb") `
        (Join-Path $toolDir "Localization.vb") `
        (Join-Path $toolDir "frmMain.vb") `
        (Join-Path $toolDir "frmPair.vb") `
        (Join-Path $toolDir "frmSettings.vb") `
        (Join-Path $toolDir "LangSettings.vb")
    if ($LASTEXITCODE -ne 0) { Write-Error "Échec compilation de l'outil graphique."; exit 1 }
    $langSrc = Join-Path $toolDir "lang"
    if (Test-Path (Join-Path $langSrc "fr.txt")) {
        Copy-Item $langSrc (Join-Path $out "lang") -Recurse -Force
    }
} else {
    Write-Host "==> (outil graphique ignoré : sources VB.NET absentes)"
}

Write-Host
Write-Host "Terminé. Binaires dans $out"
Write-Host "  - WingsLangTool.exe : outil graphique (voyez le dossier de langues de Wings)"
Write-Host "  - SmokeTest.exe : validation console"
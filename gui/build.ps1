$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path $Csc)) {
    $Csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (!(Test-Path $Csc)) {
    throw "Compilador C# do .NET Framework nao encontrado."
}

& $Csc `
    /nologo `
    /target:winexe `
    /platform:x86 `
    /optimize+ `
    /win32icon:"$PSScriptRoot\assets\TekFarmaInstaller.ico" `
    /win32manifest:"$PSScriptRoot\app.manifest" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /resource:"$PSScriptRoot\assets\logo_display.png",TekFarmaLogo `
    /resource:"$PSScriptRoot\assets\TekFarmaInstaller.ico",TekFarmaIcon `
    /out:"$RepoRoot\TekFarmaInstaller.exe" `
    "$PSScriptRoot\TekFarmaInstallerGui.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar TekFarmaInstaller.exe."
}

Get-Item "$RepoRoot\TekFarmaInstaller.exe" | Select-Object FullName,Length,LastWriteTime

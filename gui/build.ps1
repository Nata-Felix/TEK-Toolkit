$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path $Csc)) {
    $Csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (!(Test-Path $Csc)) {
    throw "Compilador C# do .NET Framework nao encontrado."
}

function Build-WinFormsExe {
    param(
        [string]$SourceFile,
        [string]$OutputFile,
        [string]$ErrorMessage
    )

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
        /out:$OutputFile `
        $SourceFile

    if ($LASTEXITCODE -ne 0) {
        throw $ErrorMessage
    }
}

Build-WinFormsExe `
    -SourceFile "$PSScriptRoot\TekFarmaInstallerGui.cs" `
    -OutputFile "$RepoRoot\TekFarmaInstaller.exe" `
    -ErrorMessage "Falha ao compilar TekFarmaInstaller.exe."

Build-WinFormsExe `
    -SourceFile "$PSScriptRoot\TekSoftwareSuporteGui.cs" `
    -OutputFile "$RepoRoot\TekSoftwareSuporte.exe" `
    -ErrorMessage "Falha ao compilar TekSoftwareSuporte.exe."

Get-Item "$RepoRoot\TekFarmaInstaller.exe", "$RepoRoot\TekSoftwareSuporte.exe" | Select-Object FullName,Length,LastWriteTime

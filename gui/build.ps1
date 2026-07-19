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
        /win32manifest:"$PSScriptRoot\app.manifest" `
        /win32icon:"$PSScriptRoot\assets\solppe_handshake.ico" `
        /reference:System.dll `
        /reference:System.Core.dll `
        /reference:System.Drawing.dll `
        /reference:System.Web.Extensions.dll `
        /reference:System.Windows.Forms.dll `
        /resource:"$PSScriptRoot\assets\solppe_logo.png",ToolkitAll.Assets.SolppeLogo.png `
        /resource:"$PSScriptRoot\assets\solppe_handshake_icon.png",ToolkitAll.Assets.SolppeHandshake.png `
        /resource:"$PSScriptRoot\assets\solppe_handshake.ico",ToolkitAll.Assets.SolppeHandshake.ico `
        /out:$OutputFile `
        $SourceFile

    if ($LASTEXITCODE -ne 0) {
        throw $ErrorMessage
    }
}

Build-WinFormsExe `
    -SourceFile "$PSScriptRoot\ToolkitAllGui.cs" `
    -OutputFile "$RepoRoot\SOLPPE_toolkit.exe" `
    -ErrorMessage "Falha ao compilar SOLPPE_toolkit.exe."

Build-WinFormsExe `
    -SourceFile "$PSScriptRoot\SolppeUpdater.cs" `
    -OutputFile "$RepoRoot\SOLPPE_updater.exe" `
    -ErrorMessage "Falha ao compilar SOLPPE_updater.exe."

Get-Item "$RepoRoot\SOLPPE_toolkit.exe", "$RepoRoot\SOLPPE_updater.exe" | Select-Object FullName,Length,LastWriteTime

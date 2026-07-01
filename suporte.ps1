$ErrorActionPreference = "Stop"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Version = "v1.0"
$Repo = "Nata-Felix/TEK-Toolkit"

$BaseUrl = "https://github.com/$Repo/releases/download/$Version"
$RawUrl = "https://raw.githubusercontent.com/$Repo/refs/heads/main"

$RunId = "{0}_{1}" -f (Get-Date -Format "yyyyMMddHHmmss"), $PID
$Destino = Join-Path ([System.IO.Path]::GetTempPath()) "TekSoftwareSuporteGui_$RunId"
$GuiExe = Join-Path $Destino "TekSoftwareSuporte.exe"
$DotNetInstaller = Join-Path $Destino "dotnet48.exe"

function LimparHistoricoPowerShell {
    try {
        Clear-History -ErrorAction SilentlyContinue
    }
    catch {
    }

    try {
        if ("Microsoft.PowerShell.PSConsoleReadLine" -as [type]) {
            [Microsoft.PowerShell.PSConsoleReadLine]::ClearHistory()
        }
    }
    catch {
    }

    try {
        $HistoryPath = (Get-PSReadLineOption -ErrorAction SilentlyContinue).HistorySavePath

        if (![string]::IsNullOrWhiteSpace($HistoryPath)) {
            Remove-Item -LiteralPath $HistoryPath -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
    }
}

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class ConsoleWindow {
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

function Set-ConsoleVisible {
    param([bool]$Visible)

    try {
        $handle = [ConsoleWindow]::GetConsoleWindow()

        if ($handle -ne [IntPtr]::Zero) {
            if ($Visible) {
                [ConsoleWindow]::ShowWindow($handle, 5) | Out-Null
            }
            else {
                [ConsoleWindow]::ShowWindow($handle, 0) | Out-Null
            }
        }
    }
    catch {
    }
}

function Test-DotNet48 {
    $release = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Release -ErrorAction SilentlyContinue).Release

    if ($null -eq $release) {
        return $false
    }

    return ([int]$release -ge 528040)
}

function BaixarArquivo {
    param(
        [string]$Url,
        [string]$DestinoArquivo,
        [string]$Nome
    )

    Write-Host ""
    Write-Host "Baixando: $Nome"

    $Request = [System.Net.HttpWebRequest]::Create($Url)
    $Response = $Request.GetResponse()
    $TotalBytes = $Response.ContentLength

    $Stream = $Response.GetResponseStream()
    $FileStream = [System.IO.File]::Create($DestinoArquivo)

    $Buffer = New-Object byte[] 1048576
    $TotalLido = 0

    try {
        do {
            $Lido = $Stream.Read($Buffer, 0, $Buffer.Length)

            if ($Lido -gt 0) {
                $FileStream.Write($Buffer, 0, $Lido)
                $TotalLido += $Lido

                if ($TotalBytes -gt 0) {
                    $Percentual = [math]::Round(($TotalLido / $TotalBytes) * 100, 2)
                    $MBLido = [math]::Round($TotalLido / 1MB, 2)
                    $MBTotal = [math]::Round($TotalBytes / 1MB, 2)

                    Write-Progress -Activity "Baixando suporte" -Status "$Nome - $MBLido MB de $MBTotal MB" -PercentComplete $Percentual
                }
            }

        } while ($Lido -gt 0)
    }
    finally {
        if ($FileStream) {
            $FileStream.Close()
        }

        if ($Stream) {
            $Stream.Close()
        }

        if ($Response) {
            $Response.Close()
        }
    }

    Write-Progress -Activity "Baixando suporte" -Completed
    Write-Host "Concluido: $Nome"
}

Set-ConsoleVisible -Visible $false
LimparHistoricoPowerShell

Clear-Host

Write-Host "====================================="
Write-Host " SUPORTE TEKSOFTWARE"
Write-Host "====================================="
Write-Host ""
Write-Host "Preparando interface de suporte..."

New-Item -ItemType Directory -Path $Destino -Force | Out-Null

if (!(Test-DotNet48)) {
    Write-Host ""
    Write-Host ".NET Framework 4.8 nao encontrado. Instalando dependencia..."
    BaixarArquivo -Url "$BaseUrl/dotnet48.exe" -DestinoArquivo $DotNetInstaller -Nome "dotnet48.exe"

    try {
        $DotNetProcesso = Start-Process -FilePath $DotNetInstaller -Verb RunAs -ArgumentList "/q /norestart" -Wait -PassThru -ErrorAction Stop
        Write-Host ".NET Framework 4.8 finalizado. ExitCode: $($DotNetProcesso.ExitCode)"
    }
    catch {
        Set-ConsoleVisible -Visible $true
        Write-Host "ERRO: Nao foi possivel iniciar o instalador do .NET Framework 4.8."
        Write-Host $_.Exception.Message
        Start-Sleep -Seconds 5
        LimparHistoricoPowerShell
        [Environment]::Exit(1)
    }

    if (!(Test-DotNet48)) {
        Set-ConsoleVisible -Visible $true
        Write-Host ""
        Write-Host "AVISO: .NET Framework 4.8 ainda nao foi detectado. Pode ser necessario reiniciar e executar novamente."
        Start-Sleep -Seconds 8
        LimparHistoricoPowerShell
        [Environment]::Exit(1)
    }
}

BaixarArquivo -Url "$RawUrl/TekSoftwareSuporte.exe" -DestinoArquivo $GuiExe -Nome "TekSoftwareSuporte.exe"

Write-Host ""
Write-Host "Abrindo suporte TekSoftware..."

try {
    $GuiProcesso = Start-Process -FilePath $GuiExe -Wait -PassThru -ErrorAction Stop
    $CodigoSaida = 0

    if ($null -ne $GuiProcesso.ExitCode) {
        $CodigoSaida = $GuiProcesso.ExitCode
    }

    Remove-Item -LiteralPath $Destino -Recurse -Force -ErrorAction SilentlyContinue
    LimparHistoricoPowerShell
    [Environment]::Exit($CodigoSaida)
}
catch {
    Set-ConsoleVisible -Visible $true
    Write-Host "ERRO: Nao foi possivel abrir a interface de suporte."
    Write-Host $_.Exception.Message
    Start-Sleep -Seconds 8
    LimparHistoricoPowerShell
    [Environment]::Exit(1)
}

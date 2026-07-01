$ErrorActionPreference = "Stop"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Version = "v1.0"
$Repo = "Nata-Felix/TEK-Toolkit"

$BaseUrl = "https://github.com/$Repo/releases/download/$Version"
$RawUrl = "https://raw.githubusercontent.com/$Repo/refs/heads/main"

$RunId = "{0}_{1}" -f (Get-Date -Format "yyyyMMddHHmmss"), $PID
$TempUsuario = [System.IO.Path]::GetTempPath()
$Destino = Join-Path $TempUsuario "InstalacaoCrystalGui_$RunId"
$CacheDir = Join-Path $TempUsuario "TEK-Toolkit_Cache"
$GuiExe = Join-Path $Destino "TekFarmaInstaller.exe"
$DotNetInstaller = Join-Path $Destino "dotnet48.exe"
$GuiCache = Join-Path $CacheDir "TekFarmaInstaller.exe"
$DotNetCache = Join-Path $CacheDir "dotnet48.exe"

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

function Test-ArquivoDownloadValido {
    param(
        [string]$Caminho,
        [int]$MaxAgeMinutos,
        [string]$VersaoMinima = ""
    )

    try {
        if (![System.IO.File]::Exists($Caminho)) {
            return $false
        }

        $Arquivo = New-Object System.IO.FileInfo($Caminho)

        if ($Arquivo.Length -lt 1024) {
            return $false
        }

        if ($MaxAgeMinutos -gt 0 -and $Arquivo.LastWriteTimeUtc -lt [DateTime]::UtcNow.AddMinutes(-$MaxAgeMinutos)) {
            return $false
        }

        $Extensao = $Arquivo.Extension.ToLowerInvariant()

        if ($Extensao -eq ".exe") {
            $Stream = [System.IO.File]::OpenRead($Caminho)

            try {
                if (!($Stream.ReadByte() -eq 0x4D -and $Stream.ReadByte() -eq 0x5A)) {
                    return $false
                }
            }
            finally {
                $Stream.Dispose()
            }

            if (![string]::IsNullOrWhiteSpace($VersaoMinima)) {
                $VersaoArquivoTexto = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Caminho).FileVersion
                $VersaoArquivo = New-Object Version
                $VersaoNecessaria = New-Object Version

                if (![Version]::TryParse($VersaoArquivoTexto, [ref]$VersaoArquivo) -or
                    ![Version]::TryParse($VersaoMinima, [ref]$VersaoNecessaria) -or
                    $VersaoArquivo -lt $VersaoNecessaria) {
                    return $false
                }
            }

            return $true
        }

        return $true
    }
    catch {
        return $false
    }
}

function EncontrarArquivoTemporarioReutilizavel {
    param(
        [string]$NomeArquivo,
        [string]$CacheArquivo,
        [int]$MaxAgeMinutos,
        [string]$VersaoMinima = ""
    )

    if ($MaxAgeMinutos -le 0) {
        return ""
    }

    if (Test-ArquivoDownloadValido -Caminho $CacheArquivo -MaxAgeMinutos $MaxAgeMinutos -VersaoMinima $VersaoMinima) {
        return $CacheArquivo
    }

    try {
        $PastasAnteriores = @([System.IO.Directory]::GetDirectories($TempUsuario, "InstalacaoCrystalGui_*") | Sort-Object {
            [System.IO.Directory]::GetLastWriteTimeUtc($_)
        } -Descending)

        foreach ($PastaAnterior in $PastasAnteriores) {
            if ([string]::Equals($PastaAnterior, $Destino, [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $Candidato = Join-Path $PastaAnterior $NomeArquivo

            if (Test-ArquivoDownloadValido -Caminho $Candidato -MaxAgeMinutos $MaxAgeMinutos -VersaoMinima $VersaoMinima) {
                [System.IO.Directory]::CreateDirectory($CacheDir) | Out-Null
                [System.IO.File]::Copy($Candidato, $CacheArquivo, $true)
                return $CacheArquivo
            }
        }
    }
    catch {
    }

    return ""
}

function BaixarArquivo {
    param(
        [string]$Url,
        [string]$DestinoArquivo,
        [string]$Nome,
        [string]$CacheArquivo = "",
        [int]$MaxAgeMinutos = 0,
        [string]$VersaoMinima = ""
    )

    $Reutilizavel = ""

    if (![string]::IsNullOrWhiteSpace($CacheArquivo)) {
        $Reutilizavel = EncontrarArquivoTemporarioReutilizavel `
            -NomeArquivo ([System.IO.Path]::GetFileName($DestinoArquivo)) `
            -CacheArquivo $CacheArquivo `
            -MaxAgeMinutos $MaxAgeMinutos `
            -VersaoMinima $VersaoMinima
    }

    if (![string]::IsNullOrWhiteSpace($Reutilizavel)) {
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($DestinoArquivo)) | Out-Null
        [System.IO.File]::Copy($Reutilizavel, $DestinoArquivo, $true)
        Write-Host ""
        Write-Host "Reutilizando arquivo ja baixado: $Nome"
        Write-Host "Cache: $Reutilizavel"
        return
    }

    Write-Host ""
    Write-Host "Baixando: $Nome"

    $IdParcial = "{0}_{1}" -f $PID, [guid]::NewGuid().ToString("N")
    $DestinoParcial = if (![string]::IsNullOrWhiteSpace($CacheArquivo)) {
        "$CacheArquivo.partial.$IdParcial"
    }
    else {
        "$DestinoArquivo.partial.$IdParcial"
    }

    try {
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($DestinoParcial)) | Out-Null

        $Request = [System.Net.HttpWebRequest]::Create($Url)
        $Response = $Request.GetResponse()
        $TotalBytes = $Response.ContentLength
        $Stream = $Response.GetResponseStream()
        $FileStream = [System.IO.File]::Create($DestinoParcial)
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

                        Write-Progress -Activity "Baixando instalador" -Status "$Nome - $MBLido MB de $MBTotal MB" -PercentComplete $Percentual
                    }
                }

            } while ($Lido -gt 0)
        }
        finally {
            if ($FileStream) {
                $FileStream.Dispose()
            }

            if ($Stream) {
                $Stream.Dispose()
            }

            if ($Response) {
                $Response.Dispose()
            }
        }

        if ($TotalBytes -gt 0 -and (New-Object System.IO.FileInfo($DestinoParcial)).Length -ne $TotalBytes) {
            throw "O download de $Nome ficou incompleto."
        }

        if (!(Test-ArquivoDownloadValido -Caminho $DestinoParcial -MaxAgeMinutos 0 -VersaoMinima $VersaoMinima)) {
            throw "O arquivo baixado para $Nome nao passou na validacao."
        }

        if (![string]::IsNullOrWhiteSpace($CacheArquivo)) {
            if ([System.IO.File]::Exists($CacheArquivo)) {
                [System.IO.File]::Delete($CacheArquivo)
            }

            [System.IO.File]::Move($DestinoParcial, $CacheArquivo)
            [System.IO.File]::Copy($CacheArquivo, $DestinoArquivo, $true)
        }
        else {
            if ([System.IO.File]::Exists($DestinoArquivo)) {
                [System.IO.File]::Delete($DestinoArquivo)
            }

            [System.IO.File]::Move($DestinoParcial, $DestinoArquivo)
        }

        Write-Host "Concluido: $Nome"
    }
    finally {
        Write-Progress -Activity "Baixando instalador" -Completed

        if ([System.IO.File]::Exists($DestinoParcial)) {
            [System.IO.File]::Delete($DestinoParcial)
        }
    }
}

Set-ConsoleVisible -Visible $false
LimparHistoricoPowerShell

Clear-Host

Write-Host "====================================="
Write-Host " INSTALADOR TEKFARMA / CRYSTAL"
Write-Host "====================================="
Write-Host ""
Write-Host "Preparando interface grafica..."

[System.IO.Directory]::CreateDirectory($Destino) | Out-Null
[System.IO.Directory]::CreateDirectory($CacheDir) | Out-Null

foreach ($ParcialAntigo in @([System.IO.Directory]::GetFiles($CacheDir, "*.partial.*"))) {
    try {
        if ([System.IO.File]::GetLastWriteTimeUtc($ParcialAntigo) -lt [DateTime]::UtcNow.AddDays(-1)) {
            [System.IO.File]::Delete($ParcialAntigo)
        }
    }
    catch {
    }
}

Write-Host "Cache de downloads: $CacheDir"

if (!(Test-DotNet48)) {
    Write-Host ""
    Write-Host ".NET Framework 4.8 nao encontrado. Instalando dependencia..."
    BaixarArquivo `
        -Url "$BaseUrl/dotnet48.exe" `
        -DestinoArquivo $DotNetInstaller `
        -Nome "dotnet48.exe" `
        -CacheArquivo $DotNetCache `
        -MaxAgeMinutos 43200

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

BaixarArquivo `
    -Url "$RawUrl/TekFarmaInstaller.exe" `
    -DestinoArquivo $GuiExe `
    -Nome "TekFarmaInstaller.exe" `
    -CacheArquivo $GuiCache `
    -MaxAgeMinutos 15 `
    -VersaoMinima "1.0.2.0"

Write-Host ""
Write-Host "Abrindo interface grafica..."

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
    Write-Host "ERRO: Nao foi possivel abrir a interface grafica."
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "Fallback em modo texto:"
    Write-Host "irm https://raw.githubusercontent.com/$Repo/refs/heads/main/install_console.ps1 | iex"
    Start-Sleep -Seconds 8
    LimparHistoricoPowerShell
    [Environment]::Exit(1)
}

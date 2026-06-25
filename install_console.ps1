$ErrorActionPreference = "Stop"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Version = "v1.0"
$Repo = "Nata-Felix/Instalacao_crystal_adv"

$BaseUrl = "https://github.com/$Repo/releases/download/$Version"
$RawUrl = "https://raw.githubusercontent.com/$Repo/main"

$Destino = Join-Path ([System.IO.Path]::GetTempPath()) "InstalacaoCrystal"

$UrlVersaoNormal = "https://files.tekfarma.com.br/versao/TekFarma50.exe"
$UrlVersaoI = "https://files.tekfarma.com.br/versao/TekFarma50i.exe"
$UrlBancoTekFarma = "https://files.tekfarma.com.br/util/TEKFARMA(NOV-2020).zip"

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

                Write-Progress -Activity "Baixando dependencias" -Status "$Nome - $MBLido MB de $MBTotal MB" -PercentComplete $Percentual
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

Write-Progress -Activity "Baixando dependencias" -Completed
Write-Host "Concluido: $Nome"

}

LimparHistoricoPowerShell

Clear-Host

Write-Host "====================================="
Write-Host " INSTALADOR TEKFARMA / CRYSTAL"
Write-Host "====================================="
Write-Host ""
Write-Host "Digite sua opcao:"
Write-Host ""
Write-Host "1 - Instalacao somente versao"
Write-Host "2 - Instalacao somente Crystal"
Write-Host "3 - Instalacao FULL: versao + VS + DotNet + Crystal"
Write-Host "4 - Instalacao SEMI-FULL: versao + Crystal"
Write-Host "99 - Instalacao TekFarma servidor/terminal"
Write-Host ""

$Modo = Read-Host "Opcao"

if ($Modo -notin @("1", "2", "3", "4", "99")) {
Write-Host "Opcao invalida."
exit 1
}

$TipoVersao = ""
$PerfilTek = ""

if ($Modo -eq "1" -or $Modo -eq "3" -or $Modo -eq "4" -or $Modo -eq "99") {
Write-Host ""
Write-Host "Escolha a versao do TekFarma:"
Write-Host ""
Write-Host "1 - Versao normal"
Write-Host "2 - Versao i"
Write-Host ""

$EscolhaVersao = Read-Host "Digite sua opcao"

if ($EscolhaVersao -eq "1") {
    $TipoVersao = "normal"
}
elseif ($EscolhaVersao -eq "2") {
    $TipoVersao = "i"
}
else {
    Write-Host "Opcao de versao invalida."
    exit 1
}

}

if ($Modo -eq "99") {
Write-Host ""
Write-Host "Escolha o tipo de instalacao TekFarma:"
Write-Host ""
Write-Host "1 - Servidor"
Write-Host "2 - Terminal"
Write-Host ""

$EscolhaPerfilTek = Read-Host "Digite sua opcao"

if ($EscolhaPerfilTek -eq "1") {
    $PerfilTek = "servidor"
}
elseif ($EscolhaPerfilTek -eq "2") {
    $PerfilTek = "terminal"
}
else {
    Write-Host "Opcao de instalacao TekFarma invalida."
    exit 1
}

}

Write-Host ""
Write-Host "Preparando pasta temporaria..."

if (Test-Path $Destino) {
Remove-Item -LiteralPath $Destino -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Path $Destino -Force | Out-Null

$ArquivosRelease = @()

if ($Modo -eq "2" -or $Modo -eq "3" -or $Modo -eq "4") {
$ArquivosRelease += "CRRuntime_32bit_13_0_39.msi"
$ArquivosRelease += "crdb_adoplus.zip"
}

if ($Modo -eq "2") {
$ArquivosRelease += "VC_redist.x86.exe"
$ArquivosRelease += "VC_redist.x64.exe"
}

if ($Modo -eq "3") {
$ArquivosRelease += "dotnet48.exe"
$ArquivosRelease += "VC_redist.x86.exe"
$ArquivosRelease += "VC_redist.x64.exe"
}

if ($Modo -eq "99") {
$ArquivosRelease += "CRRuntime_32bit_13_0_39.msi"
$ArquivosRelease += "crdb_adoplus.zip"
$ArquivosRelease += "dotnet48.exe"
$ArquivosRelease += "VC_redist.x86.exe"
$ArquivosRelease += "VC_redist.x64.exe"

if ($PerfilTek -eq "servidor") {
    $ArquivosRelease += "Firebird-2.5.9.exe"
    $ArquivosRelease += "TekFarmaPasta.zip"
    $ArquivosRelease += "pastastekfarma.zip"
    $ArquivosRelease += "DLLS.zip"
}

}

foreach ($Arquivo in $ArquivosRelease) {
$UrlArquivo = "$BaseUrl/$Arquivo"
$DestinoArquivo = Join-Path $Destino $Arquivo

BaixarArquivo -Url $UrlArquivo -DestinoArquivo $DestinoArquivo -Nome $Arquivo

}

if ($TipoVersao -eq "normal" -and $Modo -ne "99") {
BaixarArquivo -Url $UrlVersaoNormal -DestinoArquivo "$Destino\TekFarma50.exe" -Nome "TekFarma50.exe"
}

if ($TipoVersao -eq "i" -and $Modo -ne "99") {
BaixarArquivo -Url $UrlVersaoI -DestinoArquivo "$Destino\TekFarma50i.exe" -Nome "TekFarma50i.exe"
}

if ($Modo -eq "99" -and $PerfilTek -eq "servidor") {
    if ($TipoVersao -eq "normal") {
        BaixarArquivo -Url $UrlVersaoNormal -DestinoArquivo "$Destino\TekFarma50.exe" -Nome "TekFarma50.exe"
    }

    if ($TipoVersao -eq "i") {
        BaixarArquivo -Url $UrlVersaoI -DestinoArquivo "$Destino\TekFarma50i.exe" -Nome "TekFarma50i.exe"
    }

    BaixarArquivo -Url $UrlBancoTekFarma -DestinoArquivo "$Destino\TEKFARMA(NOV-2020).zip" -Nome "TEKFARMA(NOV-2020).zip"
}

if ($Modo -eq "99") {
    BaixarArquivo -Url "$RawUrl/instalar_tekfarma.ps1" -DestinoArquivo "$Destino\instalar_tekfarma.ps1" -Nome "instalar_tekfarma.ps1"
}
else {
    BaixarArquivo -Url "$RawUrl/instalar.ps1" -DestinoArquivo "$Destino\instalar.ps1" -Nome "instalar.ps1"
}

Write-Host ""
Write-Host "Downloads concluidos."
Write-Host "Iniciando instalador como Administrador..."

if ($Modo -eq "99") {
    $InstaladorLocal = "$Destino\instalar_tekfarma.ps1"
    $Argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$InstaladorLocal`" -TipoVersao `"$TipoVersao`" -PerfilTek `"$PerfilTek`""
}
else {
    $InstaladorLocal = "$Destino\instalar.ps1"
    $Argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$InstaladorLocal`" -Modo $Modo -TipoVersao `"$TipoVersao`""
}

try {
    $ProcessoElevado = Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $Argumentos -Wait -PassThru -ErrorAction Stop
    $CodigoSaida = 0

    if ($null -ne $ProcessoElevado.ExitCode) {
        $CodigoSaida = $ProcessoElevado.ExitCode
    }

    if ($CodigoSaida -eq 0) {
        Write-Host "Instalador finalizado com sucesso. Fechando esta janela..."
    }
    else {
        Write-Host "Instalador finalizado com erro. Codigo de saida: $CodigoSaida"
    }

    Start-Sleep -Seconds 2
    [Environment]::Exit($CodigoSaida)
}
catch {
    Write-Host "ERRO: Nao foi possivel iniciar o instalador como Administrador."
    Write-Host $_.Exception.Message
    Start-Sleep -Seconds 5
    [Environment]::Exit(1)
}

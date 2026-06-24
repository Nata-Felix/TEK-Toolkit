$ErrorActionPreference = "Stop"

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Version = "v1.0"
$Repo = "Nata-Felix/Instalacao_crystal_adv"

$BaseUrl = "https://github.com/$Repo/releases/download/$Version"
$RawUrl = "https://raw.githubusercontent.com/$Repo/main"

$Destino = "C:\Windows\Temp\InstalacaoCrystal"

$UrlVersaoNormal = "https://files.tekfarma.com.br/versao/TekFarma50.exe"
$UrlVersaoI = "https://files.tekfarma.com.br/versao/TekFarma50i.exe"

function BaixarArquivo {
param(
[string]$Url,
[string]$DestinoArquivo,
[string]$Nome
)

```
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
```

}

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
Write-Host ""

$Modo = Read-Host "Opcao"

if ($Modo -notin @("1", "2", "3", "4")) {
Write-Host "Opcao invalida."
exit 1
}

$TipoVersao = ""

if ($Modo -eq "1" -or $Modo -eq "3" -or $Modo -eq "4") {
Write-Host ""
Write-Host "Escolha a versao do TekFarma:"
Write-Host ""
Write-Host "1 - Versao normal"
Write-Host "2 - Versao i"
Write-Host ""

```
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
```

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

if ($Modo -eq "3") {
$ArquivosRelease += "dotnet48.exe"
$ArquivosRelease += "VC_redist.x86.exe"
$ArquivosRelease += "VC_redist.x64.exe"
}

foreach ($Arquivo in $ArquivosRelease) {
$UrlArquivo = "$BaseUrl/$Arquivo"
$DestinoArquivo = "$Destino$Arquivo"

```
BaixarArquivo -Url $UrlArquivo -DestinoArquivo $DestinoArquivo -Nome $Arquivo
```

}

if ($TipoVersao -eq "normal") {
BaixarArquivo -Url $UrlVersaoNormal -DestinoArquivo "$Destino\TekFarma50.exe" -Nome "TekFarma50.exe"
}

if ($TipoVersao -eq "i") {
BaixarArquivo -Url $UrlVersaoI -DestinoArquivo "$Destino\TekFarma50i.exe" -Nome "TekFarma50i.exe"
}

BaixarArquivo -Url "$RawUrl/instalar.ps1" -DestinoArquivo "$Destino\instalar.ps1" -Nome "instalar.ps1"

Write-Host ""
Write-Host "Downloads concluidos."
Write-Host "Iniciando instalador como Administrador..."

$InstaladorLocal = "$Destino\instalar.ps1"
$Argumentos = "-ExecutionPolicy Bypass -NoExit -File `"$InstaladorLocal`" -Modo $Modo -TipoVersao `"$TipoVersao`""

Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $Argumentos

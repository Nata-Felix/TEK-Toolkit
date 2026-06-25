param(
    [string]$Acoes = "",
    [string]$HostServidor = "SERVIDOR"
)

$ErrorActionPreference = "Stop"

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path

$Log = Join-Path $Base "suporte_teksoftware_log.txt"
$FirebirdExe = Join-Path $Base "Firebird-2.5.9.exe"
$CertificadoZip = Join-Path $Base "CADEIA_CERTIFICADO.zip"
$CertificadoZipUrl = "https://github.com/Nata-Felix/Instalacao_crystal_adv/releases/download/v1.0/CADEIA_CERTIFICADO.zip"
$GbasZip = Join-Path $Base "GBAS_FP_NOVO.zip"
$GbasZipUrl = "https://github.com/Nata-Felix/Instalacao_crystal_adv/releases/download/v1.0/GBAS_FP_NOVO.zip"
$FarmaciaPopularPortalUrl = "https://farmaciapopular-portal.saude.gov.br/farmaciapopular-portal/login.jsf"

$RaizTekSoftware = "C:\TekSoftware"
$DestinoSistema = Join-Path $RaizTekSoftware "TekFarma"
$DestinoFirebird = "C:\Program Files\Firebird\Firebird_2_5"

function LogMsg {
    param([string]$Texto)

    $Linha = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Texto"
    Add-Content -Path $Log -Value $Linha
    Write-Host $Linha
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ExecutarPasso {
    param(
        [string]$Nome,
        [scriptblock]$Acao
    )

    LogMsg "====================================="
    LogMsg "INICIANDO: $Nome"

    try {
        & $Acao
        LogMsg "FINALIZADO: $Nome"
    }
    catch {
        LogMsg "ERRO no passo '$Nome': $($_.Exception.Message)"
        LogMsg "Continuando para o proximo passo..."
    }
}

function ExecutarPassoAdmin {
    param(
        [string]$Nome,
        [scriptblock]$Acao
    )

    if (-not $script:ExecutandoComoAdmin) {
        LogMsg "====================================="
        LogMsg "INICIANDO: $Nome"
        LogMsg "ERRO no passo '$Nome': esta acao precisa executar como Administrador."
        LogMsg "Continuando para o proximo passo..."
        LogMsg "FINALIZADO: $Nome"
        return
    }

    ExecutarPasso -Nome $Nome -Acao $Acao
}

function Set-Dword {
    param(
        [string]$Path,
        [string]$Name,
        [int]$Value
    )

    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }

    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType DWord -Force | Out-Null
}

function NormalizarCaminho {
    param([string]$Caminho)

    if ([string]::IsNullOrWhiteSpace($Caminho)) {
        return ""
    }

    try {
        return [System.IO.Path]::GetFullPath($Caminho).TrimEnd("\")
    }
    catch {
        return $Caminho.TrimEnd("\")
    }
}

function Test-CaminhoDentroOuIgual {
    param(
        [string]$BasePath,
        [string]$Path
    )

    $BaseNormalizada = NormalizarCaminho $BasePath
    $PathNormalizado = NormalizarCaminho $Path

    if ([string]::IsNullOrWhiteSpace($BaseNormalizada) -or [string]::IsNullOrWhiteSpace($PathNormalizado)) {
        return $false
    }

    return $PathNormalizado.Equals($BaseNormalizada, [System.StringComparison]::OrdinalIgnoreCase) -or
        $PathNormalizado.StartsWith("$BaseNormalizada\", [System.StringComparison]::OrdinalIgnoreCase)
}

function ConfigurarRedeAvancada {
    $services = @(
        "LanmanServer",
        "LanmanWorkstation",
        "fdPHost",
        "FDResPub",
        "SSDPSRV",
        "upnphost"
    )

    foreach ($svc in $services) {
        try {
            $service = Get-Service -Name $svc -ErrorAction SilentlyContinue

            if ($null -ne $service) {
                Set-Service -Name $svc -StartupType Automatic
                Start-Service -Name $svc -ErrorAction SilentlyContinue
                LogMsg "Servico de rede OK: $svc"
            }
        }
        catch {
            LogMsg "AVISO: Falha ao configurar servico ${svc}: $($_.Exception.Message)"
        }
    }

    try {
        $regexGrupos = "(?i)(Network Discovery|Descoberta.*Rede|File and Printer Sharing|Compartilhamento.*Arquiv.*Impressor)"
        $rules = Get-NetFirewallRule | Where-Object {
            $_.DisplayGroup -match $regexGrupos -or
            $_.DisplayName -match $regexGrupos -or
            $_.Name -like "NETDIS-*" -or
            $_.Name -like "FPS-*"
        }

        if ($rules) {
            $rules | Sort-Object Name -Unique | ForEach-Object {
                Set-NetFirewallRule -Name $_.Name -Enabled True
            }
        }

        LogMsg "Regras de firewall de rede/compartilhamento ativadas."
    }
    catch {
        LogMsg "AVISO: Falha ao configurar firewall: $($_.Exception.Message)"
    }

    try {
        Get-NetAdapterBinding -ComponentID ms_server | Where-Object { $_.Enabled -eq $false } | Enable-NetAdapterBinding
        Get-NetAdapterBinding -ComponentID ms_msclient | Where-Object { $_.Enabled -eq $false } | Enable-NetAdapterBinding
        LogMsg "Bindings de rede ativados."
    }
    catch {
        LogMsg "AVISO: Falha ao configurar bindings de rede: $($_.Exception.Message)"
    }

    Set-Dword -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\NcdAutoSetup\Private" -Name "AutoSetup" -Value 1

    $ntlmPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0"
    if (!(Test-Path $ntlmPath)) {
        New-Item -Path $ntlmPath -Force | Out-Null
    }

    $clientSec = (Get-ItemProperty -Path $ntlmPath -Name "NtlmMinClientSec" -ErrorAction SilentlyContinue).NtlmMinClientSec
    $serverSec = (Get-ItemProperty -Path $ntlmPath -Name "NtlmMinServerSec" -ErrorAction SilentlyContinue).NtlmMinServerSec

    if ($null -eq $clientSec) { $clientSec = 0 }
    if ($null -eq $serverSec) { $serverSec = 0 }

    Set-Dword -Path $ntlmPath -Name "NtlmMinClientSec" -Value ($clientSec -bor 0x20000000)
    Set-Dword -Path $ntlmPath -Name "NtlmMinServerSec" -Value ($serverSec -bor 0x20000000)

    Set-Dword -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa" -Name "EveryoneIncludesAnonymous" -Value 1
    Set-Dword -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa" -Name "RestrictAnonymous" -Value 0
    Set-Dword -Path "HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters" -Name "RestrictNullSessAccess" -Value 0
    Set-Dword -Path "HKLM:\SYSTEM\CurrentControlSet\Services\LanmanWorkstation\Parameters" -Name "AllowInsecureGuestAuth" -Value 1

    New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters" -Name "NullSessionShares" -PropertyType MultiString -Value @("Public", "TekSoftware") -Force | Out-Null

    Restart-Service -Name FDResPub -Force -ErrorAction SilentlyContinue
    Restart-Service -Name LanmanServer -Force -ErrorAction SilentlyContinue

    LogMsg "Configuracoes avancadas de rede aplicadas."
}

function CriarCredencialServidor {
    $HostCredencial = "SERVIDOR"
    $UsuarioCredencial = "convidado"

    LogMsg "Configurando credencial do Windows para ${HostCredencial} com usuario ${UsuarioCredencial}."
    & cmdkey.exe /delete:$HostCredencial 2>&1 | Out-Null
    & cmd.exe /c "echo.|cmdkey.exe /add:$HostCredencial /user:$UsuarioCredencial" 2>&1 | Out-Null
    LogMsg "Credencial do Windows configurada: host=$HostCredencial usuario=$UsuarioCredencial senha=vazia"
}

function CriarCredencialConvidadoParaHost {
    param([string]$HostCredencial)

    if ([string]::IsNullOrWhiteSpace($HostCredencial)) {
        return
    }

    $HostCredencial = $HostCredencial.Trim("\").Trim()

    try {
        LogMsg "Configurando credencial convidado para ${HostCredencial}."
        & cmdkey.exe /delete:$HostCredencial 2>&1 | Out-Null
        & cmd.exe /c "echo.|cmdkey.exe /add:$HostCredencial /user:convidado" 2>&1 | Out-Null
        LogMsg "Credencial configurada para ${HostCredencial}: usuario=convidado senha=vazia"
    }
    catch {
        LogMsg "AVISO: Falha ao configurar credencial para ${HostCredencial}: $($_.Exception.Message)"
    }
}

function HabilitarMapeamentoElevadoNoExplorer {
    try {
        Set-Dword -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" -Name "EnableLinkedConnections" -Value 1
        LogMsg "EnableLinkedConnections configurado como 1. Mapeamentos elevados ficam disponiveis ao usuario apos logoff/reinicio."
    }
    catch {
        LogMsg "AVISO: Falha ao configurar EnableLinkedConnections: $($_.Exception.Message)"
    }
}

function BaixarCadeiaCertificadoSeNecessario {
    if (Test-Path $CertificadoZip) {
        return
    }

    LogMsg "Arquivo CADEIA_CERTIFICADO.zip nao encontrado na pasta temporaria. Baixando do release..."
    Invoke-WebRequest -UseBasicParsing -Uri $CertificadoZipUrl -OutFile $CertificadoZip
    LogMsg "CADEIA_CERTIFICADO.zip baixado."
}

function ImportarArquivoCertificado {
    param(
        [System.IO.FileInfo]$Arquivo,
        [System.Security.Cryptography.X509Certificates.X509Store]$Store
    )

    $Colecao = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection

    try {
        $Colecao.Import($Arquivo.FullName)
    }
    catch {
        LogMsg "AVISO: Falha ao ler certificado $($Arquivo.Name): $($_.Exception.Message)"
        return
    }

    if ($Colecao.Count -eq 0) {
        LogMsg "AVISO: Nenhum certificado encontrado em $($Arquivo.Name)"
        return
    }

    foreach ($Certificado in $Colecao) {
        try {
            $Existente = $Store.Certificates.Find(
                [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
                $Certificado.Thumbprint,
                $false
            )

            if ($Existente.Count -gt 0) {
                LogMsg "Certificado ja instalado: $($Certificado.Subject) [$($Certificado.Thumbprint)]"
                continue
            }

            $Store.Add($Certificado)
            LogMsg "Certificado importado: $($Certificado.Subject) [$($Certificado.Thumbprint)]"
        }
        catch {
            LogMsg "AVISO: Falha ao importar $($Certificado.Subject): $($_.Exception.Message)"
        }
    }
}

function InstalarCadeiaCertificado {
    BaixarCadeiaCertificadoSeNecessario

    if (!(Test-Path $CertificadoZip)) {
        throw "Arquivo de cadeia de certificado nao encontrado: $CertificadoZip"
    }

    $DestinoTemp = Join-Path $Base ("cadeia_certificado_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $DestinoTemp -Force | Out-Null

    try {
        LogMsg "Extraindo cadeia de certificado: $CertificadoZip"
        Expand-Archive -LiteralPath $CertificadoZip -DestinationPath $DestinoTemp -Force

        $Arquivos = @(Get-ChildItem -Path $DestinoTemp -Recurse -File | Where-Object {
            $_.Extension -match "^\.(cer|crt|sst|p7b|p7c)$"
        })

        if ($Arquivos.Count -eq 0) {
            LogMsg "AVISO: Nenhum arquivo .cer, .crt, .sst, .p7b ou .p7c encontrado no zip."
            return
        }

        LogMsg "Importando $($Arquivos.Count) arquivo(s) para LocalMachine\Root."

        $Store = New-Object System.Security.Cryptography.X509Certificates.X509Store(
            [System.Security.Cryptography.X509Certificates.StoreName]::Root,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
        )

        $Store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

        try {
            foreach ($Arquivo in $Arquivos) {
                LogMsg "Processando certificado: $($Arquivo.Name)"
                ImportarArquivoCertificado -Arquivo $Arquivo -Store $Store
            }
        }
        finally {
            $Store.Close()
        }

        LogMsg "Cadeia de certificado instalada em Autoridades de Certificacao Raiz Confiaveis."
    }
    finally {
        try {
            if ([System.IO.Directory]::Exists($DestinoTemp)) {
                [System.IO.Directory]::Delete($DestinoTemp, $true)
            }
        }
        catch {
            LogMsg "AVISO: Falha ao limpar pasta temporaria de certificados: $($_.Exception.Message)"
        }
    }
}

function BaixarGbasSeNecessario {
    if (Test-Path $GbasZip) {
        return
    }

    LogMsg "Arquivo GBAS_FP_NOVO.zip nao encontrado na pasta temporaria. Baixando do release..."
    Invoke-WebRequest -UseBasicParsing -Uri $GbasZipUrl -OutFile $GbasZip
    LogMsg "GBAS_FP_NOVO.zip baixado."
}

function AdicionarCandidatoTekFarma {
    param(
        [System.Collections.ArrayList]$Lista,
        [string]$Caminho
    )

    if ([string]::IsNullOrWhiteSpace($Caminho)) {
        return
    }

    $Normalizado = NormalizarCaminho $Caminho

    if ([string]::IsNullOrWhiteSpace($Normalizado)) {
        return
    }

    foreach ($Item in $Lista) {
        if ($Item.Equals($Normalizado, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    [void]$Lista.Add($Normalizado)
}

function ObterCandidatosTekFarmaGbas {
    $Candidatos = New-Object System.Collections.ArrayList

    foreach ($Drive in @(Get-CimInstance Win32_LogicalDisk -Filter "DriveType=4" -ErrorAction SilentlyContinue)) {
        $Letra = $Drive.DeviceID

        if ([string]::IsNullOrWhiteSpace($Letra)) {
            continue
        }

        AdicionarCandidatoTekFarma -Lista $Candidatos -Caminho (Join-Path "$Letra\" "TekSoftware\TekFarma")
        AdicionarCandidatoTekFarma -Lista $Candidatos -Caminho (Join-Path "$Letra\" "TekFarma")
    }

    foreach ($Codigo in ([int][char]'Z')..([int][char]'D')) {
        $Letra = [char]$Codigo
        $Root = "$Letra`:\"

        if (Test-Path $Root) {
            AdicionarCandidatoTekFarma -Lista $Candidatos -Caminho (Join-Path $Root "TekSoftware\TekFarma")
            AdicionarCandidatoTekFarma -Lista $Candidatos -Caminho (Join-Path $Root "TekFarma")
        }
    }

    AdicionarCandidatoTekFarma -Lista $Candidatos -Caminho $DestinoSistema

    return @($Candidatos)
}

function ObterDestinoTekFarmaGbas {
    foreach ($Candidato in @(ObterCandidatosTekFarmaGbas)) {
        if (Test-Path $Candidato) {
            LogMsg "Destino TekFarma encontrado: $Candidato"
            return $Candidato
        }
    }

    LogMsg "Nenhuma pasta TekFarma existente encontrada. Criando destino local: $DestinoSistema"
    New-Item -ItemType Directory -Path $DestinoSistema -Force | Out-Null
    return $DestinoSistema
}

function CopiarConteudoPasta {
    param(
        [string]$Origem,
        [string]$Destino
    )

    $Arquivos = @(Get-ChildItem -LiteralPath $Origem -Recurse -File -Force)

    foreach ($Arquivo in $Arquivos) {
        $Relativo = $Arquivo.FullName.Substring($Origem.Length).TrimStart("\")
        $DestinoArquivo = Join-Path $Destino $Relativo
        $DestinoPasta = Split-Path -Parent $DestinoArquivo

        if (!(Test-Path $DestinoPasta)) {
            New-Item -ItemType Directory -Path $DestinoPasta -Force | Out-Null
        }

        Copy-Item -LiteralPath $Arquivo.FullName -Destination $DestinoArquivo -Force
        LogMsg "Arquivo copiado: $Relativo"
    }

    LogMsg "$($Arquivos.Count) arquivo(s) copiado(s) para $Destino"
}

function EncontrarIdentificacaoTerminal {
    param([string]$Destino)

    $Nomes = @(
        "Identificacao_Terminal.exe",
        "Identificar_Terminal.exe",
        "Identicacao_Terminal.exe"
    )

    foreach ($Nome in $Nomes) {
        $Caminho = Join-Path $Destino $Nome

        if (Test-Path $Caminho) {
            return $Caminho
        }
    }

    $Encontrado = Get-ChildItem -LiteralPath $Destino -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "(?i)Ident.*Terminal.*\.exe$" } |
        Select-Object -First 1

    if ($null -ne $Encontrado) {
        return $Encontrado.FullName
    }

    return ""
}

function AbrirArquivoPeloShellUsuario {
    param([string]$Caminho)

    $Pasta = Split-Path -Parent $Caminho

    try {
        $Shell = New-Object -ComObject Shell.Application
        $Shell.ShellExecute($Caminho, "", $Pasta, "open", 1)
        LogMsg "Processo aberto pelo shell do usuario: $Caminho"
    }
    catch {
        LogMsg "AVISO: Falha ao abrir pelo shell do usuario. Abrindo direto: $($_.Exception.Message)"
        Start-Process -FilePath $Caminho -WorkingDirectory $Pasta
    }
}

function AbrirUrlPeloShellUsuario {
    param([string]$Url)

    try {
        $Shell = New-Object -ComObject Shell.Application
        $Shell.ShellExecute($Url, "", "", "open", 1)
        LogMsg "URL aberta pelo shell do usuario: $Url"
    }
    catch {
        LogMsg "AVISO: Falha ao abrir URL pelo shell do usuario. Abrindo direto: $($_.Exception.Message)"
        Start-Process $Url
    }
}

function InstalarFarmaciaPopularGbas {
    BaixarGbasSeNecessario

    if (!(Test-Path $GbasZip)) {
        throw "Arquivo GBAS_FP_NOVO.zip nao encontrado: $GbasZip"
    }

    $DestinoTekFarma = ObterDestinoTekFarmaGbas
    New-Item -ItemType Directory -Path $DestinoTekFarma -Force | Out-Null

    $DestinoTemp = Join-Path $Base ("gbas_fp_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $DestinoTemp -Force | Out-Null

    try {
        LogMsg "Extraindo GBAS_FP_NOVO.zip para pasta temporaria."
        Expand-Archive -LiteralPath $GbasZip -DestinationPath $DestinoTemp -Force

        $ItensRaiz = @(Get-ChildItem -LiteralPath $DestinoTemp -Force)
        $OrigemArquivos = $DestinoTemp

        if ($ItensRaiz.Count -eq 1 -and $ItensRaiz[0].PSIsContainer) {
            $OrigemArquivos = $ItensRaiz[0].FullName
            LogMsg "Pasta interna detectada no zip: $($ItensRaiz[0].Name)"
        }

        CopiarConteudoPasta -Origem $OrigemArquivos -Destino $DestinoTekFarma

        $IdentificacaoTerminal = EncontrarIdentificacaoTerminal -Destino $DestinoTekFarma

        if ([string]::IsNullOrWhiteSpace($IdentificacaoTerminal)) {
            LogMsg "AVISO: Executavel de identificacao do terminal nao encontrado em $DestinoTekFarma"
        }
        else {
            LogMsg "Abrindo identificacao do terminal: $IdentificacaoTerminal"
            AbrirArquivoPeloShellUsuario -Caminho $IdentificacaoTerminal
        }

        LogMsg "Abrindo portal Farmacia Popular: $FarmaciaPopularPortalUrl"
        AbrirUrlPeloShellUsuario -Url $FarmaciaPopularPortalUrl
    }
    finally {
        try {
            if ([System.IO.Directory]::Exists($DestinoTemp)) {
                [System.IO.Directory]::Delete($DestinoTemp, $true)
            }
        }
        catch {
            LogMsg "AVISO: Falha ao limpar pasta temporaria GBAS: $($_.Exception.Message)"
        }
    }
}

function ExecutarComandoManutencao {
    param(
        [string]$Nome,
        [string]$Arquivo,
        [string[]]$Argumentos = @(),
        [switch]$IgnorarErro
    )

    $LinhaComando = "$Arquivo $($Argumentos -join ' ')".Trim()
    LogMsg "Executando ${Nome}: $LinhaComando"

    $Saida = & $Arquivo @Argumentos 2>&1
    $Codigo = $LASTEXITCODE

    foreach ($Linha in @($Saida)) {
        if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
            LogMsg "$Linha"
        }
    }

    if ($Codigo -ne 0) {
        $Mensagem = "${Nome} retornou codigo ${Codigo}."

        if ($IgnorarErro) {
            LogMsg "AVISO: $Mensagem"
        }
        else {
            throw $Mensagem
        }
    }

    return $Codigo
}

function PararServicoManutencao {
    param([string]$Nome)

    try {
        $Servico = Get-Service -Name $Nome -ErrorAction SilentlyContinue

        if ($null -eq $Servico) {
            LogMsg "AVISO: Servico nao encontrado: $Nome"
            return
        }

        if ($Servico.Status -ne "Stopped") {
            LogMsg "Parando servico: $Nome"
            Stop-Service -Name $Nome -Force -ErrorAction Stop
        }
        else {
            LogMsg "Servico ja parado: $Nome"
        }
    }
    catch {
        LogMsg "AVISO: Falha ao parar servico ${Nome}: $($_.Exception.Message)"
    }
}

function IniciarServicoManutencao {
    param([string]$Nome)

    try {
        $Servico = Get-Service -Name $Nome -ErrorAction SilentlyContinue

        if ($null -eq $Servico) {
            LogMsg "AVISO: Servico nao encontrado: $Nome"
            return
        }

        LogMsg "Iniciando servico: $Nome"
        Start-Service -Name $Nome -ErrorAction Stop
    }
    catch {
        LogMsg "AVISO: Falha ao iniciar servico ${Nome}: $($_.Exception.Message)"
    }
}

function InstalarNet35 {
    $ArgumentosOriginais = @("/online", "/enable-feature", "/featurename:NetFX3", "/All", "/source:C:\", "/LimitAccess")
    $Codigo = ExecutarComandoManutencao -Nome ".NET Framework 3.5 via DISM com source C:\" -Arquivo "Dism.exe" -Argumentos $ArgumentosOriginais -IgnorarErro

    if ($Codigo -ne 0) {
        LogMsg "Tentando instalar .NET 3.5 novamente usando Windows Update como origem."
        ExecutarComandoManutencao -Nome ".NET Framework 3.5 via DISM online" -Arquivo "Dism.exe" -Argumentos @("/online", "/enable-feature", "/featurename:NetFX3", "/All", "/NoRestart")
    }

    LogMsg ".NET Framework 3.5 processado."
}

function ResetarPortasCom {
    $Path = "HKLM:\SYSTEM\CurrentControlSet\Control\COM Name Arbiter"
    $Name = "ComDB"

    if (Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $Path -Name $Name -Force
        LogMsg "ComDB removido. Todas as portas COM reservadas foram liberadas."
    }
    else {
        LogMsg "ComDB nao encontrado. Nada para remover."
    }

    LogMsg "Reinicie o computador para aplicar totalmente o reset de portas COM."
}

function RemoverSenhaCompartilhamento {
    Set-Dword -Path "HKLM:\System\CurrentControlSet\Control\Lsa" -Name "LimitBlankPasswordUse" -Value 0
    LogMsg "LimitBlankPasswordUse configurado como 0."
}

function CorrigirWindowsUpdate {
    foreach ($Servico in @("bits", "wuauserv", "appidsvc", "cryptsvc")) {
        PararServicoManutencao -Nome $Servico
    }

    $DownloaderPaths = @(
        (Join-Path $env:ALLUSERSPROFILE "Application Data\Microsoft\Network\Downloader"),
        (Join-Path $env:ALLUSERSPROFILE "Microsoft\Network\Downloader")
    )

    foreach ($Path in $DownloaderPaths) {
        try {
            if (Test-Path $Path) {
                LogMsg "Limpando downloader do Windows Update: $Path"
                Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
            }
        }
        catch {
            LogMsg "AVISO: Falha ao limpar ${Path}: $($_.Exception.Message)"
        }
    }

    foreach ($Path in @((Join-Path $env:SystemRoot "SoftwareDistribution"), (Join-Path $env:SystemRoot "System32\catroot2"))) {
        try {
            if (Test-Path $Path) {
                LogMsg "Removendo pasta: $Path"
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            }
        }
        catch {
            LogMsg "AVISO: Falha ao remover ${Path}: $($_.Exception.Message)"
        }
    }

    $SdBits = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;AU)(A;;CCLCSWRPWPDTLOCRRC;;;PU)"
    $SdWuauserv = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;AU)(A;;CCLCSWRPWPDTLOCRRC;;;PU)"

    ExecutarComandoManutencao -Nome "Restaurar permissao BITS" -Arquivo "sc.exe" -Argumentos @("sdset", "bits", $SdBits) -IgnorarErro | Out-Null
    ExecutarComandoManutencao -Nome "Restaurar permissao Windows Update" -Arquivo "sc.exe" -Argumentos @("sdset", "wuauserv", $SdWuauserv) -IgnorarErro | Out-Null

    $Dlls = @(
        "atl.dll", "urlmon.dll", "mshtml.dll", "shdocvw.dll", "browseui.dll",
        "jscript.dll", "vbscript.dll", "scrrun.dll", "msxml.dll", "msxml3.dll",
        "msxml6.dll", "actxprxy.dll", "softpub.dll", "wintrust.dll", "dssenh.dll",
        "rsaenh.dll", "gpkcsp.dll", "sccbase.dll", "slbcsp.dll", "cryptdlg.dll",
        "oleaut32.dll", "ole32.dll", "shell32.dll", "initpki.dll", "wuapi.dll",
        "wuaueng.dll", "wuaueng1.dll", "wucltui.dll", "wups.dll", "wups2.dll",
        "wuweb.dll", "qmgr.dll", "qmgrprxy.dll", "wucltux.dll", "muweb.dll", "wuwebv.dll"
    )

    $System32 = Join-Path $env:windir "System32"

    foreach ($Dll in $Dlls) {
        $DllPath = Join-Path $System32 $Dll

        if (Test-Path $DllPath) {
            ExecutarComandoManutencao -Nome "Registrar $Dll" -Arquivo "regsvr32.exe" -Argumentos @("/s", $DllPath) -IgnorarErro | Out-Null
        }
        else {
            LogMsg "AVISO: DLL nao encontrada para registrar: $DllPath"
        }
    }

    ExecutarComandoManutencao -Nome "Reset Winsock" -Arquivo "netsh.exe" -Argumentos @("winsock", "reset") -IgnorarErro | Out-Null
    ExecutarComandoManutencao -Nome "Reset proxy WinHTTP" -Arquivo "netsh.exe" -Argumentos @("winhttp", "reset", "proxy") -IgnorarErro | Out-Null

    foreach ($Servico in @("bits", "wuauserv", "appidsvc", "cryptsvc")) {
        IniciarServicoManutencao -Nome $Servico
    }

    LogMsg "Windows Update fix finalizado."
}

function AumentarCacheIcone {
    $RegPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer"

    if (!(Test-Path $RegPath)) {
        New-Item -Path $RegPath -Force | Out-Null
    }

    New-ItemProperty -Path $RegPath -Name "Max Cached Icons" -Value "4096" -PropertyType String -Force | Out-Null
    LogMsg "Max Cached Icons configurado como 4096."

    Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    $ArquivosCache = @(
        (Join-Path $env:LOCALAPPDATA "IconCache.db")
    )

    $ExplorerCache = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\Explorer"
    if (Test-Path $ExplorerCache) {
        $ArquivosCache += @(Get-ChildItem -LiteralPath $ExplorerCache -Filter "iconcache*" -Force -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    }

    foreach ($Arquivo in $ArquivosCache) {
        Remove-Item -LiteralPath $Arquivo -Force -ErrorAction SilentlyContinue
    }

    Start-Process explorer.exe
    LogMsg "Cache de icones limpo e Explorer reiniciado."
}

function DesativarFirewallWindows {
    ExecutarComandoManutencao -Nome "Desativar firewall em todos os perfis" -Arquivo "netsh.exe" -Argumentos @("advfirewall", "set", "allprofiles", "state", "off")
    LogMsg "Firewall do Windows desativado em todos os perfis."
}

function ResetarImpressora {
    PararServicoManutencao -Nome "spooler"

    $Fila = Join-Path $env:SystemRoot "System32\Spool\Printers"

    if (Test-Path $Fila) {
        LogMsg "Limpando fila de impressao: $Fila"
        Get-ChildItem -LiteralPath $Fila -Force -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
    }
    else {
        LogMsg "Pasta da fila de impressao nao encontrada: $Fila"
    }

    IniciarServicoManutencao -Nome "spooler"
    LogMsg "Spooler de impressao resetado."
}

function InstalarGpeditMsc {
    $PackagesPath = Join-Path $env:SystemRoot "servicing\Packages"

    if (!(Test-Path $PackagesPath)) {
        throw "Pasta de pacotes do Windows nao encontrada: $PackagesPath"
    }

    $Pacotes = @(
        Get-ChildItem -LiteralPath $PackagesPath -Filter "Microsoft-Windows-GroupPolicy-ClientExtensions-Package~3*.mum" -File -ErrorAction SilentlyContinue
        Get-ChildItem -LiteralPath $PackagesPath -Filter "Microsoft-Windows-GroupPolicy-ClientTools-Package~3*.mum" -File -ErrorAction SilentlyContinue
    )

    if ($Pacotes.Count -eq 0) {
        throw "Nenhum pacote do GPEDIT.MSC encontrado em $PackagesPath"
    }

    foreach ($Pacote in $Pacotes) {
        ExecutarComandoManutencao -Nome "Instalar pacote GPEDIT $($Pacote.Name)" -Arquivo "Dism.exe" -Argumentos @("/online", "/norestart", "/add-package:$($Pacote.FullName)") -IgnorarErro | Out-Null
    }

    LogMsg "Instalacao do GPEDIT.MSC processada."
}

function ObterMapeamentosTekSoftware {
    @(Get-CimInstance Win32_LogicalDisk -Filter "DriveType=4" -ErrorAction SilentlyContinue | Where-Object {
        $_.ProviderName -match "^\\\\[^\\]+\\TekSoftware\\?$"
    })
}

function ObterDrivesTekSoftwareEmTextoNetUse {
    param([string[]]$Linhas)

    $Drives = @()

    foreach ($Linha in @($Linhas)) {
        if ($Linha -match "(?i)\b([A-Z]:)\s+\\\\[^\\]+\\TekSoftware\\b") {
            $Drive = $Matches[1].ToUpperInvariant()

            if ($Drives -notcontains $Drive) {
                $Drives += $Drive
            }
        }
    }

    return $Drives
}

function RemoverMapeamentosTekSoftware {
    foreach ($Mapeamento in @(ObterMapeamentosTekSoftware)) {
        try {
            $DriveMapeado = $Mapeamento.DeviceID
            LogMsg "Removendo mapeamento existente: $DriveMapeado -> $($Mapeamento.ProviderName)"
            & net.exe use $DriveMapeado /delete /y | Out-Null
        }
        catch {
            LogMsg "AVISO: Falha ao remover mapeamento $($Mapeamento.DeviceID): $($_.Exception.Message)"
        }
    }

    try {
        $ResultadoNetUseUsuario = ExecutarCmdShellUsuarioComTimeout -Comandos @("net use") -TimeoutSegundos 8
        $DrivesUsuario = @(ObterDrivesTekSoftwareEmTextoNetUse -Linhas $ResultadoNetUseUsuario.Output)

        foreach ($DriveUsuario in $DrivesUsuario) {
            LogMsg "Removendo mapeamento TekSoftware do usuario: $DriveUsuario"
            $ResultadoRemocao = ExecutarCmdShellUsuarioComTimeout -Comandos @("net use $DriveUsuario /delete /y") -TimeoutSegundos 8

            foreach ($Linha in @($ResultadoRemocao.Output)) {
                if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                    LogMsg "$Linha"
                }
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao remover mapeamentos TekSoftware do usuario: $($_.Exception.Message)"
    }
}

function ExecutarProcessoMapeamentoComTimeout {
    param(
        [string]$Arquivo,
        [string]$Argumentos,
        [int]$TimeoutSegundos = 12
    )

    $Psi = New-Object System.Diagnostics.ProcessStartInfo
    $Psi.FileName = $Arquivo
    $Psi.Arguments = $Argumentos
    $Psi.UseShellExecute = $false
    $Psi.RedirectStandardOutput = $true
    $Psi.RedirectStandardError = $true
    $Psi.CreateNoWindow = $true

    $Processo = New-Object System.Diagnostics.Process
    $Processo.StartInfo = $Psi

    try {
        [void]$Processo.Start()

        if (-not $Processo.WaitForExit($TimeoutSegundos * 1000)) {
            try {
                $Processo.Kill()
            }
            catch {
            }

            return [pscustomobject]@{
                ExitCode = 124
                TimedOut = $true
                Output = @("Tempo limite de ${TimeoutSegundos}s atingido.")
            }
        }

        $StdOut = $Processo.StandardOutput.ReadToEnd()
        $StdErr = $Processo.StandardError.ReadToEnd()
        $Linhas = @()

        if (![string]::IsNullOrWhiteSpace($StdOut)) {
            $Linhas += @($StdOut -split "(`r`n|`n|`r)" | Where-Object { $_.Trim().Length -gt 0 })
        }

        if (![string]::IsNullOrWhiteSpace($StdErr)) {
            $Linhas += @($StdErr -split "(`r`n|`n|`r)" | Where-Object { $_.Trim().Length -gt 0 })
        }

        return [pscustomobject]@{
            ExitCode = $Processo.ExitCode
            TimedOut = $false
            Output = $Linhas
        }
    }
    finally {
        if ($Processo) {
            $Processo.Dispose()
        }
    }
}

function ObterPastaPublicaSuporte {
    $Publico = $env:PUBLIC

    if ([string]::IsNullOrWhiteSpace($Publico)) {
        $Publico = "C:\Users\Public"
    }

    $Pasta = Join-Path $Publico "TekSoftwareSuporte"

    if (!(Test-Path $Pasta)) {
        New-Item -ItemType Directory -Path $Pasta -Force | Out-Null
    }

    return $Pasta
}

function ExecutarCmdShellUsuarioComTimeout {
    param(
        [string[]]$Comandos,
        [int]$TimeoutSegundos = 20
    )

    $Pasta = ObterPastaPublicaSuporte
    $Id = [guid]::NewGuid().ToString("N")
    $CmdPath = Join-Path $Pasta "run_$Id.cmd"
    $OutPath = Join-Path $Pasta "run_$Id.out"
    $ExitPath = Join-Path $Pasta "run_$Id.exit"

    $Conteudo = @(
        "@echo off",
        "setlocal EnableExtensions",
        "chcp 65001 >nul",
        "call :main > `"$OutPath`" 2>&1",
        "echo %ERRORLEVEL%> `"$ExitPath`"",
        "exit /b %ERRORLEVEL%",
        ":main"
    ) + $Comandos + @(
        "exit /b %ERRORLEVEL%"
    )

    Set-Content -LiteralPath $CmdPath -Value $Conteudo -Encoding ASCII -Force

    try {
        $Shell = New-Object -ComObject Shell.Application
        $Shell.ShellExecute("cmd.exe", "/c `"$CmdPath`"", "", "open", 0)
    }
    catch {
        Remove-Item -LiteralPath $CmdPath, $OutPath, $ExitPath -Force -ErrorAction SilentlyContinue
        throw "Falha ao executar comando pelo shell do usuario: $($_.Exception.Message)"
    }

    $Limite = (Get-Date).AddSeconds($TimeoutSegundos)

    while ((Get-Date) -lt $Limite) {
        if (Test-Path $ExitPath) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    $TimedOut = !(Test-Path $ExitPath)
    $Output = @()
    $ExitCode = 124

    if (Test-Path $OutPath) {
        $Output = @(Get-Content -LiteralPath $OutPath -ErrorAction SilentlyContinue | Where-Object { $_.Trim().Length -gt 0 })
    }

    if ($TimedOut) {
        $Output += "Tempo limite de ${TimeoutSegundos}s atingido."
    }
    else {
        $ExitText = (Get-Content -LiteralPath $ExitPath -ErrorAction SilentlyContinue | Select-Object -First 1)

        if (![int]::TryParse($ExitText, [ref]$ExitCode)) {
            $ExitCode = 1
        }
    }

    Remove-Item -LiteralPath $CmdPath, $OutPath, $ExitPath -Force -ErrorAction SilentlyContinue

    return [pscustomobject]@{
        ExitCode = $ExitCode
        TimedOut = $TimedOut
        Output = $Output
    }
}

function AdicionarLetraOcupada {
    param(
        [hashtable]$Letras,
        [string]$Valor
    )

    if ([string]::IsNullOrWhiteSpace($Valor)) {
        return
    }

    foreach ($Match in [regex]::Matches($Valor, "(?i)\b([A-Z]):")) {
        $Letras[$Match.Groups[1].Value.ToUpperInvariant()] = $true
    }

    if ($Valor -match "^[A-Za-z]$") {
        $Letras[$Valor.ToUpperInvariant()] = $true
    }
}

function ObterLetrasOcupadasMapeamento {
    $Letras = @{}

    try {
        foreach ($Drive in @(Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue)) {
            AdicionarLetraOcupada -Letras $Letras -Valor $Drive.Name
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar Get-PSDrive: $($_.Exception.Message)"
    }

    try {
        foreach ($Drive in @(Get-CimInstance Win32_LogicalDisk -ErrorAction SilentlyContinue)) {
            AdicionarLetraOcupada -Letras $Letras -Valor $Drive.DeviceID
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar Win32_LogicalDisk: $($_.Exception.Message)"
    }

    try {
        foreach ($Drive in @([System.IO.DriveInfo]::GetDrives())) {
            AdicionarLetraOcupada -Letras $Letras -Valor $Drive.Name
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar DriveInfo: $($_.Exception.Message)"
    }

    try {
        if (Test-Path "HKCU:\Network") {
            foreach ($Item in @(Get-ChildItem "HKCU:\Network" -ErrorAction SilentlyContinue)) {
                AdicionarLetraOcupada -Letras $Letras -Valor $Item.PSChildName
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar HKCU:\Network: $($_.Exception.Message)"
    }

    try {
        $ResultadoNetUse = ExecutarProcessoMapeamentoComTimeout -Arquivo "cmd.exe" -Argumentos "/c net use" -TimeoutSegundos 6

        if ($ResultadoNetUse.TimedOut) {
            LogMsg "AVISO: Consulta net use travou. Seguindo com as demais fontes."
        }
        else {
            foreach ($Linha in @($ResultadoNetUse.Output)) {
                AdicionarLetraOcupada -Letras $Letras -Valor $Linha
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar net use: $($_.Exception.Message)"
    }

    try {
        $ResultadoNetUseUsuario = ExecutarCmdShellUsuarioComTimeout -Comandos @("net use") -TimeoutSegundos 8

        if ($ResultadoNetUseUsuario.TimedOut) {
            LogMsg "AVISO: Consulta net use do usuario travou. Seguindo com as demais fontes."
        }
        else {
            foreach ($Linha in @($ResultadoNetUseUsuario.Output)) {
                AdicionarLetraOcupada -Letras $Letras -Valor $Linha
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar net use do usuario: $($_.Exception.Message)"
    }

    try {
        $ResultadoSubst = ExecutarProcessoMapeamentoComTimeout -Arquivo "cmd.exe" -Argumentos "/c subst" -TimeoutSegundos 6

        if (-not $ResultadoSubst.TimedOut) {
            foreach ($Linha in @($ResultadoSubst.Output)) {
                AdicionarLetraOcupada -Letras $Letras -Valor $Linha
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar subst: $($_.Exception.Message)"
    }

    return $Letras
}

function ObterLetrasLivresMapeamento {
    $Ocupadas = ObterLetrasOcupadasMapeamento
    $OcupadasTexto = @($Ocupadas.Keys | Sort-Object | ForEach-Object { "$_`:" }) -join ", "

    if ([string]::IsNullOrWhiteSpace($OcupadasTexto)) {
        $OcupadasTexto = "nenhuma"
    }

    LogMsg "Letras ocupadas detectadas: $OcupadasTexto"

    $Livres = @()

    foreach ($Codigo in ([int][char]'Z')..([int][char]'D')) {
        $Letra = [char]$Codigo
        $Chave = $Letra.ToString().ToUpperInvariant()

        if (-not $Ocupadas.ContainsKey($Chave)) {
            $Livres += "$Chave`:"
        }
    }

    $LivresTexto = @($Livres) -join ", "

    if ([string]::IsNullOrWhiteSpace($LivresTexto)) {
        $LivresTexto = "nenhuma"
    }

    LogMsg "Letras livres candidatas: $LivresTexto"
    return $Livres
}

function MapearDriveNoShellUsuario {
    param(
        [string]$Drive,
        [string]$CaminhoRede,
        [string]$HostRede
    )

    $Comandos = @()

    if (![string]::IsNullOrWhiteSpace($HostRede)) {
        $Comandos += "cmdkey.exe /delete:$HostRede"
        $Comandos += "echo.|cmdkey.exe /add:$HostRede /user:convidado"
    }

    $Comandos += "net.exe use $Drive `"$CaminhoRede`" /persistent:yes"

    return ExecutarCmdShellUsuarioComTimeout -Comandos $Comandos -TimeoutSegundos 20
}

function Test-MapeamentoCriadoNoShellUsuario {
    param(
        [string]$Drive,
        [string]$CaminhoRede
    )

    try {
        $Resultado = ExecutarCmdShellUsuarioComTimeout -Comandos @("net use $Drive") -TimeoutSegundos 8
        $Texto = (@($Resultado.Output) -join "`n")

        return $Texto -match [regex]::Escape($CaminhoRede)
    }
    catch {
        LogMsg "AVISO: Falha ao validar mapeamento $Drive no usuario: $($_.Exception.Message)"
        return $false
    }
}

function ReiniciarExplorerNoShellUsuario {
    try {
        LogMsg "Reiniciando Explorer para atualizar unidades de rede..."
        $Resultado = ExecutarCmdShellUsuarioComTimeout -Comandos @(
            "taskkill /f /im explorer.exe",
            "timeout /t 2 /nobreak >nul",
            "start `"`" explorer.exe",
            "exit /b 0"
        ) -TimeoutSegundos 15

        foreach ($Linha in @($Resultado.Output)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "$Linha"
            }
        }

        if ($Resultado.TimedOut) {
            LogMsg "AVISO: Reinicio do Explorer excedeu o tempo limite."
        }
        else {
            LogMsg "Explorer reiniciado para atualizar o mapeamento."
        }
    }
    catch {
        LogMsg "AVISO: Falha ao reiniciar Explorer: $($_.Exception.Message)"
    }
}

function EncontrarTekAplicacaoEmDrive {
    param([string]$Drive)

    $Candidatos = @(
        (Join-Path "$Drive\" "TekFarma\TekAplicacao.exe"),
        (Join-Path "$Drive\" "TekSoftware\TekFarma\TekAplicacao.exe"),
        (Join-Path "$Drive\" "TekAplicacao.exe")
    )

    foreach ($Candidato in $Candidatos) {
        if (Test-Path $Candidato) {
            return $Candidato
        }
    }

    return ""
}

function EncontrarTekAplicacaoEmRede {
    param([string]$CaminhoRede)

    $Candidatos = @(
        (Join-Path $CaminhoRede "TekFarma\TekAplicacao.exe"),
        (Join-Path $CaminhoRede "TekSoftware\TekFarma\TekAplicacao.exe"),
        (Join-Path $CaminhoRede "TekAplicacao.exe")
    )

    foreach ($Candidato in $Candidatos) {
        if (Test-Path $Candidato) {
            return $Candidato
        }
    }

    return ""
}

function CriarAtalhoTekFarmaMapeado {
    param(
        [string]$Drive,
        [string]$CaminhoRede = ""
    )

    $Alvo = ""

    if (![string]::IsNullOrWhiteSpace($CaminhoRede)) {
        $Alvo = EncontrarTekAplicacaoEmRede -CaminhoRede $CaminhoRede
    }

    if ([string]::IsNullOrWhiteSpace($Alvo)) {
        $Alvo = EncontrarTekAplicacaoEmDrive -Drive $Drive
    }

    $Desktop = [Environment]::GetFolderPath("Desktop")
    $Atalho = Join-Path $Desktop "TekFarma.lnk"

    if ([string]::IsNullOrWhiteSpace($Alvo)) {
        LogMsg "AVISO: Executavel TekFarma nao encontrado no mapeamento $Drive nem em $CaminhoRede"
        return
    }

    $Shell = New-Object -ComObject WScript.Shell
    $Shortcut = $Shell.CreateShortcut($Atalho)
    $Shortcut.TargetPath = $Alvo
    $Shortcut.WorkingDirectory = Split-Path -Parent $Alvo
    $Shortcut.IconLocation = "$Alvo,0"
    $Shortcut.Save()

    LogMsg "Atalho criado na area de trabalho: $Atalho"
}

function TentarMapearTekSoftware {
    param(
        [string]$CaminhoRede,
        [string]$HostRede
    )

    $LetrasLivres = @(ObterLetrasLivresMapeamento)

    if ($LetrasLivres.Count -eq 0) {
        LogMsg "AVISO: Nenhuma letra livre encontrada entre Z: e D:."
        return ""
    }

    foreach ($Drive in $LetrasLivres) {
        LogMsg "Tentando mapear $CaminhoRede em $Drive no shell do usuario"
        $Resultado = MapearDriveNoShellUsuario -Drive $Drive -CaminhoRede $CaminhoRede -HostRede $HostRede

        foreach ($Linha in @($Resultado.Output)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "$Linha"
            }
        }

        if (Test-MapeamentoCriadoNoShellUsuario -Drive $Drive -CaminhoRede $CaminhoRede) {
            LogMsg "Mapeamento criado: $Drive -> $CaminhoRede"
            return $Drive
        }

        if ($Resultado.TimedOut) {
            LogMsg "AVISO: Mapeamento em $Drive travou no shell do usuario. Tentando proxima letra..."
            continue
        }

        if ($Resultado.ExitCode -eq 0) {
            LogMsg "AVISO: net use retornou sucesso, mas o mapeamento $Drive nao apareceu no usuario. Tentando proxima letra..."
            continue
        }

        LogMsg "AVISO: Falha ao mapear em $Drive. Codigo: $($Resultado.ExitCode). Tentando proxima letra..."
    }

    return ""
}

function ObterCandidatosRedeTekSoftware {
    param([string]$HostInformado)

    $Valor = ""

    if ($null -ne $HostInformado) {
        $Valor = $HostInformado.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($Valor)) {
        $Valor = "SERVIDOR"
    }

    if ($Valor.StartsWith("\\")) {
        return @($Valor.TrimEnd("\"))
    }

    if ($Valor.Contains("\")) {
        return @("\\" + $Valor.Trim("\"))
    }

    $HostLimpo = $Valor.Trim("\")

    return @(
        "\\$HostLimpo\TekSoftware",
        "\\$HostLimpo\Tek"
    )
}

function ObterHostDeCaminhoRede {
    param([string]$CaminhoRede)

    if ($CaminhoRede -match "^\\\\([^\\]+)\\") {
        return $Matches[1]
    }

    return ""
}

function MapearTekSoftware {
    param([string]$HostInformado)

    HabilitarMapeamentoElevadoNoExplorer
    RemoverMapeamentosTekSoftware

    foreach ($CaminhoRede in @(ObterCandidatosRedeTekSoftware -HostInformado $HostInformado)) {
        $HostRede = ObterHostDeCaminhoRede -CaminhoRede $CaminhoRede
        CriarCredencialConvidadoParaHost -HostCredencial $HostRede

        LogMsg "Candidato de rede: $CaminhoRede"

        $LetraLivre = TentarMapearTekSoftware -CaminhoRede $CaminhoRede -HostRede $HostRede

        if (![string]::IsNullOrWhiteSpace($LetraLivre)) {
            CriarAtalhoTekFarmaMapeado -Drive $LetraLivre -CaminhoRede $CaminhoRede
            ReiniciarExplorerNoShellUsuario
            return
        }

        LogMsg "AVISO: Nao foi possivel mapear $CaminhoRede. Tentando proximo candidato..."
    }

    LogMsg "AVISO: Nao foi possivel mapear TekSoftware entre Z: e D:."
}

function ObterRaizesBuscaTek {
    $Raizes = New-Object System.Collections.Generic.List[string]

    foreach ($Path in @($Base, $RaizTekSoftware, $DestinoSistema)) {
        if (![string]::IsNullOrWhiteSpace($Path) -and (Test-Path $Path)) {
            $Raizes.Add($Path)
        }
    }

    foreach ($Mapeamento in @(Get-CimInstance Win32_LogicalDisk -Filter "DriveType=4" -ErrorAction SilentlyContinue)) {
        if ($Mapeamento.ProviderName -match "^\\\\[^\\]+\\TekSoftware\\?$") {
            $Raizes.Add($Mapeamento.DeviceID + "\")
        }
    }

    $Raizes | Select-Object -Unique
}

function EncontrarArquivoTek {
    param([string]$Nome)

    $Encontrados = @()

    foreach ($Raiz in @(ObterRaizesBuscaTek)) {
        try {
            $Encontrados += @(Get-ChildItem -LiteralPath $Raiz -Filter $Nome -File -Recurse -ErrorAction SilentlyContinue)
        }
        catch {
            LogMsg "AVISO: Falha ao procurar $Nome em $Raiz."
        }
    }

    $Encontrados | Select-Object -ExpandProperty FullName -Unique
}

function ExecutarBatsFirewall {
    foreach ($NomeBat in @("00.Permitir Aplicativo.bat", "00.TekOnline.bat")) {
        $Bats = @(EncontrarArquivoTek -Nome $NomeBat)

        if ($Bats.Count -eq 0) {
            LogMsg "AVISO: BAT nao encontrado: $NomeBat"
            continue
        }

        foreach ($Bat in $Bats) {
            try {
                LogMsg "Executando BAT: $Bat"
                $ComandoBat = "call `"$Bat`" < nul"
                $Proc = Start-Process -FilePath "cmd.exe" -ArgumentList "/c $ComandoBat" -WorkingDirectory (Split-Path -Parent $Bat) -Wait -PassThru
                LogMsg "BAT finalizado: $NomeBat ExitCode: $($Proc.ExitCode)"
            }
            catch {
                LogMsg "AVISO: Falha ao executar ${Bat}: $($_.Exception.Message)"
            }
        }
    }
}

function CriarRegraFirewallPrograma {
    param([string]$Programa)

    if (!(Test-Path $Programa)) {
        return
    }

    $NomeBase = [System.IO.Path]::GetFileNameWithoutExtension($Programa)

    foreach ($Direcao in @("Inbound", "Outbound")) {
        $Sufixo = if ($Direcao -eq "Inbound") { "Entrada" } else { "Saida" }
        $DisplayName = "TekSoftware - $NomeBase - $Sufixo"

        try {
            Get-NetFirewallRule -DisplayName $DisplayName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
            New-NetFirewallRule -DisplayName $DisplayName -Direction $Direcao -Program $Programa -Action Allow -Profile Any -Enabled True | Out-Null
            LogMsg "Regra firewall criada: $DisplayName -> $Programa"
        }
        catch {
            LogMsg "AVISO: Falha ao criar regra firewall para ${Programa}: $($_.Exception.Message)"
        }
    }
}

function AdicionarExcecoesFirewall {
    ExecutarBatsFirewall

    foreach ($Exe in @("TekAplicacao.exe", "TekAtalho.exe", "TekGerenciador.exe", "TekUpdate.exe", "TekIFood.exe", "Sync.exe", "TekSync.exe")) {
        $Arquivos = @(EncontrarArquivoTek -Nome $Exe)

        if ($Arquivos.Count -eq 0) {
            LogMsg "AVISO: Executavel nao encontrado para firewall: $Exe"
            continue
        }

        foreach ($Arquivo in $Arquivos) {
            CriarRegraFirewallPrograma -Programa $Arquivo
        }
    }
}

function InstalarExe {
    param(
        [string]$Caminho,
        [string]$Argumentos,
        [string]$Nome
    )

    LogMsg "Arquivo: $Caminho"
    LogMsg "Argumentos: $Argumentos"

    if (!(Test-Path $Caminho)) {
        LogMsg "AVISO: Arquivo nao encontrado: $Caminho"
        return $false
    }

    try {
        Unblock-File $Caminho -ErrorAction SilentlyContinue
        $Processo = Start-Process -FilePath $Caminho -ArgumentList $Argumentos -Wait -PassThru
        LogMsg "$Nome finalizado. ExitCode: $($Processo.ExitCode)"
        return ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 3010)
    }
    catch {
        LogMsg "ERRO ao executar ${Nome}: $($_.Exception.Message)"
        return $false
    }
}

function ObterInstalacoesFirebird {
    @(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "*Firebird*" })
}

function PararServicosEProcessosFirebird {
    $ServicosFirebird = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })

    foreach ($Servico in $ServicosFirebird) {
        try {
            if ($Servico.Status -ne "Stopped") {
                LogMsg "Parando servico Firebird: $($Servico.Name)"
                Stop-Service -Name $Servico.Name -Force -ErrorAction Stop
            }
        }
        catch {
            LogMsg "AVISO: Nao foi possivel parar servico Firebird $($Servico.Name): $($_.Exception.Message)"
        }
    }

    $NomesProcessos = @("fbserver", "fbguard", "fb_inet_server", "firebird")
    $ProcessosFirebird = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $NomesProcessos -contains $_.ProcessName -or $_.ProcessName -like "Firebird*"
    })

    foreach ($Processo in $ProcessosFirebird) {
        try {
            LogMsg "Finalizando processo Firebird: $($Processo.ProcessName).exe PID $($Processo.Id)"
            Stop-Process -Id $Processo.Id -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel finalizar processo Firebird $($Processo.ProcessName): $($_.Exception.Message)"
        }
    }
}

function RemoverServicoFirebirdRestante {
    $ServicosFirebird = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })

    foreach ($Servico in $ServicosFirebird) {
        try {
            $NomeServico = $Servico.Name
            LogMsg "Removendo servico Firebird restante: $NomeServico"
            & sc.exe delete $NomeServico | Out-Null
        }
        catch {
            LogMsg "AVISO: Nao foi possivel remover servico Firebird $($Servico.Name): $($_.Exception.Message)"
        }
    }
}

function RemoverDiretorioFirebirdSeguro {
    param([string]$Caminho)

    if ([string]::IsNullOrWhiteSpace($Caminho)) {
        return
    }

    $CaminhoNormalizado = NormalizarCaminho $Caminho
    $RaizesPermitidas = @(
        "C:\Program Files\Firebird",
        "C:\Program Files (x86)\Firebird"
    )

    $Permitido = $false

    foreach ($RaizPermitida in $RaizesPermitidas) {
        if (Test-CaminhoDentroOuIgual -BasePath $RaizPermitida -Path $CaminhoNormalizado) {
            $Permitido = $true
            break
        }
    }

    if (!$Permitido) {
        LogMsg "AVISO: Remocao de pasta Firebird ignorada por seguranca: $CaminhoNormalizado"
        return
    }

    if (Test-Path -LiteralPath $CaminhoNormalizado) {
        try {
            LogMsg "Removendo pasta Firebird: $CaminhoNormalizado"
            Remove-Item -LiteralPath $CaminhoNormalizado -Recurse -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel remover pasta Firebird ${CaminhoNormalizado}: $($_.Exception.Message)"
        }
    }
}

function DesinstalarEntradaFirebird {
    param($Entrada)

    LogMsg "Firebird encontrado: $($Entrada.DisplayName) $($Entrada.DisplayVersion)"

    $Comando = if (![string]::IsNullOrWhiteSpace($Entrada.QuietUninstallString)) {
        $Entrada.QuietUninstallString
    }
    else {
        $Entrada.UninstallString
    }

    $Guid = $null

    if ($Entrada.PSChildName -match "^\{[0-9A-Fa-f-]{36}\}$") {
        $Guid = $Entrada.PSChildName
    }
    elseif ($Comando -match "\{[0-9A-Fa-f-]{36}\}") {
        $Guid = $Matches[0]
    }

    try {
        if ($Guid) {
            LogMsg "Desinstalando Firebird via MSI: $Guid"
            $ProcMsi = Start-Process -FilePath "msiexec.exe" -ArgumentList @("/x", $Guid, "/qn", "/norestart") -Wait -PassThru
            LogMsg "Desinstalacao Firebird MSI ExitCode: $($ProcMsi.ExitCode)"
            return
        }

        if ([string]::IsNullOrWhiteSpace($Comando)) {
            LogMsg "AVISO: Firebird sem comando de desinstalacao registrado."
            return
        }

        if ($Comando -notmatch "/VERYSILENT|/SILENT|/quiet|/qn|/s(\s|$)") {
            $Comando = "$Comando /VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
        }

        LogMsg "Desinstalando Firebird via comando registrado: $Comando"
        $ProcCmd = Start-Process -FilePath "cmd.exe" -ArgumentList @("/c", $Comando) -Wait -PassThru
        LogMsg "Desinstalacao Firebird ExitCode: $($ProcCmd.ExitCode)"
    }
    catch {
        LogMsg "AVISO: Falha ao desinstalar Firebird $($Entrada.DisplayName): $($_.Exception.Message)"
    }
}

function RemoverFirebirdExistente {
    PararServicosEProcessosFirebird

    $FirebirdInstalados = @(ObterInstalacoesFirebird)

    if ($FirebirdInstalados.Count -eq 0) {
        LogMsg "Nenhuma instalacao do Firebird encontrada no registro."
    }

    foreach ($Firebird in $FirebirdInstalados) {
        DesinstalarEntradaFirebird -Entrada $Firebird
    }

    Start-Sleep -Seconds 2
    PararServicosEProcessosFirebird
    RemoverServicoFirebirdRestante

    foreach ($Firebird in $FirebirdInstalados) {
        RemoverDiretorioFirebirdSeguro -Caminho $Firebird.InstallLocation
    }

    RemoverDiretorioFirebirdSeguro -Caminho $DestinoFirebird
    RemoverDiretorioFirebirdSeguro -Caminho "C:\Program Files (x86)\Firebird"
}

function ConfigurarRecuperacaoFirebird {
    $ServicosFirebird = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })

    foreach ($Servico in $ServicosFirebird) {
        try {
            $NomeServico = $Servico.Name
            Set-Service -Name $NomeServico -StartupType Automatic -ErrorAction Stop
            & sc.exe failure $NomeServico reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
            & sc.exe failureflag $NomeServico 1 | Out-Null
            LogMsg "Recuperacao Firebird configurada: $NomeServico"
        }
        catch {
            LogMsg "AVISO: Falha ao configurar recuperacao do Firebird $($Servico.Name): $($_.Exception.Message)"
        }
    }
}

function IniciarServicosFirebird {
    $ServicosFirebird = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })

    foreach ($Servico in $ServicosFirebird) {
        $Iniciado = $false

        for ($Tentativa = 1; $Tentativa -le 3; $Tentativa++) {
            try {
                $Atual = Get-Service -Name $Servico.Name -ErrorAction Stop

                if ($Atual.Status -ne "Running") {
                    LogMsg "Iniciando Firebird $($Servico.Name), tentativa $Tentativa de 3."
                    Start-Service -Name $Servico.Name -ErrorAction Stop
                    Start-Sleep -Seconds 2
                }

                $Atual = Get-Service -Name $Servico.Name -ErrorAction Stop
                if ($Atual.Status -eq "Running") {
                    LogMsg "Servico Firebird OK: $($Servico.Name) - Running"
                    $Iniciado = $true
                    break
                }
            }
            catch {
                LogMsg "AVISO: Falha ao iniciar Firebird $($Servico.Name) tentativa ${Tentativa}: $($_.Exception.Message)"
                Start-Sleep -Seconds 2
            }
        }

        if (!$Iniciado) {
            LogMsg "AVISO: Servico Firebird nao ficou em execucao: $($Servico.Name)"
        }
    }
}

function ReinstalarFirebird {
    RemoverFirebirdExistente

    $Argumentos = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$DestinoFirebird`""
    InstalarExe -Caminho $FirebirdExe -Argumentos $Argumentos -Nome "Firebird 2.5.9" | Out-Null

    if (Test-Path $DestinoFirebird) {
        LogMsg "Pasta Firebird encontrada: $DestinoFirebird"
    }
    else {
        LogMsg "AVISO: Pasta Firebird nao encontrada apos instalacao: $DestinoFirebird"
    }

    ConfigurarRecuperacaoFirebird
    IniciarServicosFirebird
}

Clear-Host

LogMsg "====================================="
LogMsg "SUPORTE TEKSOFTWARE"
LogMsg "====================================="
LogMsg "Acoes recebidas: $Acoes"
LogMsg "HostServidor recebido: $HostServidor"
LogMsg "Base: $Base"

$script:ExecutandoComoAdmin = Test-Admin
LogMsg "Executando como administrador: $script:ExecutandoComoAdmin"

$ListaAcoes = @($Acoes.Split(",") | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })

foreach ($Acao in $ListaAcoes) {
    switch ($Acao) {
        "rede" {
            ExecutarPassoAdmin "Configurar rede avancada" {
                ConfigurarRedeAvancada
            }
        }
        "credencial" {
            ExecutarPasso "Criar credencial SERVIDOR" {
                CriarCredencialServidor
            }
        }
        "certificados" {
            ExecutarPassoAdmin "Instalar cadeia de certificado" {
                InstalarCadeiaCertificado
            }
        }
        "mapear" {
            ExecutarPasso "Mapear TekSoftware" {
                MapearTekSoftware -HostInformado $HostServidor
            }
        }
        "firewall" {
            ExecutarPassoAdmin "Adicionar excecao no firewall" {
                AdicionarExcecoesFirewall
            }
        }
        "farmaciapopular" {
            ExecutarPasso "Instalar Farmacia Popular GBAS" {
                InstalarFarmaciaPopularGbas
            }
        }
        "firebird" {
            ExecutarPassoAdmin "Reinstalar Firebird" {
                ReinstalarFirebird
            }
        }
        "net35" {
            ExecutarPassoAdmin "Instalar .NET Framework 3.5" {
                InstalarNet35
            }
        }
        "portacom" {
            ExecutarPassoAdmin "Resetar portas COM" {
                ResetarPortasCom
            }
        }
        "removersenhacompartilhamento" {
            ExecutarPassoAdmin "Remover senha de compartilhamento" {
                RemoverSenhaCompartilhamento
            }
        }
        "windowsupdatefix" {
            ExecutarPassoAdmin "Corrigir Windows Update" {
                CorrigirWindowsUpdate
            }
        }
        "cacheicone" {
            ExecutarPassoAdmin "Aumentar cache de icones" {
                AumentarCacheIcone
            }
        }
        "firewalloff" {
            ExecutarPassoAdmin "Desativar Firewall do Windows" {
                DesativarFirewallWindows
            }
        }
        "resetimpressora" {
            ExecutarPassoAdmin "Resetar impressora" {
                ResetarImpressora
            }
        }
        "gpedit" {
            ExecutarPassoAdmin "Instalar GPEDIT.MSC" {
                InstalarGpeditMsc
            }
        }
        default {
            LogMsg "AVISO: Acao desconhecida ignorada: $Acao"
        }
    }
}

LogMsg "====================================="
LogMsg "SUPORTE TEKSOFTWARE FINALIZADO"
LogMsg "Log geral: $Log"
LogMsg "====================================="

exit 0

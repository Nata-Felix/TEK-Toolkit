param(
    [string]$Acoes = "",
    [string]$HostServidor = "SERVIDOR",
    [string]$ImpressoraMarca = "",
    [string]$ImpressoraModelo = "",
    [string]$ImpressoraArquivo = "",
    [string]$ImpressoraInstalador = "",
    [string]$RemoverImpressoras = "",
    [string]$RemoverDriversImpressora = "",
    [string]$TrocaPerfil = "",
    [string]$TrocaHostAntigo = "SERVIDOR",
    [string]$TrocaTipoVersao = "normal",
    [string]$TrocaCopiarPrincipal = "true",
    [string]$TrocaCopiarFinal = "false",
    [string]$TrocaInstalarFull = "true",
    [string]$TrocaInstalarFirebird = "true",
    [string]$TrocaConfigurarRede = "true",
    [string]$TrocaRenomearReiniciar = "false",
    [string]$TrocaExcluirPastas = "ArqPrn;Atualizacao;CFe;Documentos;DocumentosFiscais;NFCe;NFe;Sngpc;versao;XML;Xml;xml;SAT;CTe;MDFe",
    [string]$SefazTimeZoneId = "E. South America Standard Time"
)

$ErrorActionPreference = "Stop"

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path

$Log = Join-Path $Base "suporte_teksoftware_log.txt"
$FirebirdExe = Join-Path $Base "Firebird-2.5.9.exe"
$Net48 = Join-Path $Base "dotnet48.exe"
$CertificadoZip = Join-Path $Base "CADEIA_CERTIFICADO.zip"
$CertificadoZipUrl = "https://github.com/Nata-Felix/Instalacao_crystal_adv/releases/download/v1.0/CADEIA_CERTIFICADO.zip"
$GbasZip = Join-Path $Base "GBAS_FP_NOVO.zip"
$GbasZipUrl = "https://github.com/Nata-Felix/Instalacao_crystal_adv/releases/download/v1.0/GBAS_FP_NOVO.zip"
$Net48Url = "https://github.com/Nata-Felix/Instalacao_crystal_adv/releases/download/v1.0/dotnet48.exe"
$RadminVpnExe = Join-Path $Base "Radmin_VPN_2.0.4899.9.exe"
$RadminVpnUrl = "https://download.radmin-vpn.com/download/files/Radmin_VPN_2.0.4899.9.exe"
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

function Add-DwordFlag {
    param(
        [string]$Path,
        [string]$Name,
        [int]$Flag
    )

    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }

    $Atual = (Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue).$Name

    if ($null -eq $Atual) {
        $Atual = 0
    }

    New-ItemProperty -Path $Path -Name $Name -Value ([int]$Atual -bor $Flag) -PropertyType DWord -Force | Out-Null
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

function ObterHostCredencial {
    param([string]$Valor)

    if ([string]::IsNullOrWhiteSpace($Valor)) {
        return ""
    }

    $Limpo = $Valor.Trim()

    if ($Limpo -match "^\\\\([^\\]+)") {
        return $Matches[1].Trim()
    }

    $Limpo = $Limpo.Trim("\")

    if ($Limpo.Contains("\")) {
        return (($Limpo -split "\\") | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -First 1).Trim()
    }

    return $Limpo.Trim()
}

function CriarCredencialConvidadoParaHost {
    param([string]$HostCredencial)

    $HostCredencial = ObterHostCredencial -Valor $HostCredencial

    if ([string]::IsNullOrWhiteSpace($HostCredencial)) {
        return
    }

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

function CriarCredencialConvidadoParaHostUsuario {
    param([string]$HostCredencial)

    $HostCredencial = ObterHostCredencial -Valor $HostCredencial

    if ([string]::IsNullOrWhiteSpace($HostCredencial)) {
        return
    }

    try {
        LogMsg "Configurando credencial convidado para ${HostCredencial} no shell do usuario."
        $Resultado = ExecutarCmdShellUsuarioComTimeout -Comandos @(
            "cmdkey.exe /delete:$HostCredencial >nul 2>nul",
            "echo.|cmdkey.exe /add:$HostCredencial /user:convidado",
            "exit /b 0"
        ) -TimeoutSegundos 8

        foreach ($Linha in @($Resultado.Output)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "CMDKEY: $Linha"
            }
        }

        if ($Resultado.TimedOut) {
            LogMsg "AVISO: Cmdkey para ${HostCredencial} excedeu o tempo limite no shell do usuario."
        }
        elseif ($Resultado.ExitCode -eq 0) {
            LogMsg "Credencial do usuario configurada para ${HostCredencial}: usuario=convidado senha=vazia"
        }
        else {
            LogMsg "AVISO: Cmdkey para ${HostCredencial} retornou codigo $($Resultado.ExitCode) no shell do usuario."
        }
    }
    catch {
        LogMsg "AVISO: Falha ao configurar credencial do usuario para ${HostCredencial}: $($_.Exception.Message)"
    }
}

function CriarCredenciaisConvidadoParaHosts {
    param([string[]]$Hosts)

    $Vistos = @{}

    foreach ($HostItem in @($Hosts)) {
        $HostCredencial = ObterHostCredencial -Valor $HostItem

        if ([string]::IsNullOrWhiteSpace($HostCredencial)) {
            continue
        }

        $Chave = $HostCredencial.ToUpperInvariant()

        if ($Vistos.ContainsKey($Chave)) {
            continue
        }

        $Vistos[$Chave] = $true
        CriarCredencialConvidadoParaHost -HostCredencial $HostCredencial
        CriarCredencialConvidadoParaHostUsuario -HostCredencial $HostCredencial
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

function ResolverTimeZoneSefaz {
    param([string]$Valor)

    $ValorNormalizado = ""

    if ($null -ne $Valor) {
        $ValorNormalizado = $Valor.Trim()
    }

    switch -Regex ($ValorNormalizado) {
        "^(UTC)?-?2$|^UTC-02$" { return "UTC-02" }
        "^(UTC)?-?3$|^UTC-03$" { return "E. South America Standard Time" }
        "^(UTC)?-?4$|^UTC-04$" { return "SA Western Standard Time" }
        "^(UTC)?-?5$|^UTC-05$" { return "SA Pacific Standard Time" }
    }

    if (![string]::IsNullOrWhiteSpace($ValorNormalizado)) {
        $Encontrado = Get-TimeZone -ListAvailable | Where-Object { $_.Id -ieq $ValorNormalizado } | Select-Object -First 1

        if ($Encontrado) {
            return $Encontrado.Id
        }
    }

    return "E. South America Standard Time"
}

function ConfigurarTimeZoneSefaz {
    $IdDesejado = ResolverTimeZoneSefaz -Valor $SefazTimeZoneId
    $Antes = Get-TimeZone

    LogMsg "Timezone atual: $($Antes.Id) - $($Antes.DisplayName)"
    LogMsg "Timezone selecionado para SEFAZ: $IdDesejado"

    if ($Antes.Id -ieq $IdDesejado) {
        LogMsg "Timezone ja esta correto. Nenhuma alteracao necessaria."
        return
    }

    try {
        Set-TimeZone -Id $IdDesejado -ErrorAction Stop
        $Depois = Get-TimeZone
        LogMsg "Timezone alterado para: $($Depois.Id) - $($Depois.DisplayName)"
    }
    catch {
        LogMsg "AVISO: Falha ao alterar timezone para ${IdDesejado}: $($_.Exception.Message)"
        throw
    }
}

function ConfigurarTls12Sefaz {
    LogMsg "Configurando TLS 1.2 para SChannel, .NET e WinHTTP."

    foreach ($Role in @("Client", "Server")) {
        $Path = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\$Role"
        Set-Dword -Path $Path -Name "Enabled" -Value 1
        Set-Dword -Path $Path -Name "DisabledByDefault" -Value 0
        LogMsg "SChannel TLS 1.2 $Role habilitado."
    }

    foreach ($Path in @(
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v2.0.50727",
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\.NETFramework\v2.0.50727",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\.NETFramework\v4.0.30319"
    )) {
        Set-Dword -Path $Path -Name "SchUseStrongCrypto" -Value 1
        Set-Dword -Path $Path -Name "SystemDefaultTlsVersions" -Value 1
        LogMsg ".NET strong crypto aplicado: $Path"
    }

    foreach ($Path in @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp"
    )) {
        Add-DwordFlag -Path $Path -Name "DefaultSecureProtocols" -Flag 0x800
        LogMsg "WinHTTP DefaultSecureProtocols inclui TLS 1.2: $Path"
    }

    foreach ($Path in @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings"
    )) {
        Add-DwordFlag -Path $Path -Name "SecureProtocols" -Flag 0x800
        LogMsg "Internet Settings SecureProtocols inclui TLS 1.2: $Path"
    }
}

function SincronizarHoraSefaz {
    try {
        Set-Service -Name W32Time -StartupType Automatic -ErrorAction SilentlyContinue
        Start-Service -Name W32Time -ErrorAction SilentlyContinue
        LogMsg "Servico W32Time iniciado/configurado como automatico."
    }
    catch {
        LogMsg "AVISO: Falha ao iniciar W32Time: $($_.Exception.Message)"
    }

    $Peers = "a.st1.ntp.br,0x8 b.st1.ntp.br,0x8 c.st1.ntp.br,0x8 time.windows.com,0x8"

    try {
        $SaidaConfig = & w32tm.exe /config /manualpeerlist:$Peers /syncfromflags:manual /reliable:no /update 2>&1
        foreach ($Linha in @($SaidaConfig)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "W32TM config: $Linha"
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao configurar NTP: $($_.Exception.Message)"
    }

    try {
        $SaidaResync = & w32tm.exe /resync /force 2>&1
        foreach ($Linha in @($SaidaResync)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "W32TM resync: $Linha"
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao sincronizar hora: $($_.Exception.Message)"
    }

    try {
        $SaidaStatus = & w32tm.exe /query /status 2>&1
        foreach ($Linha in @($SaidaStatus)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "W32TM status: $Linha"
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao consultar status W32Time: $($_.Exception.Message)"
    }

    LogMsg "Data/hora local apos sincronizacao: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
}

function VerificarCertificadosClienteSefaz {
    $Agora = Get-Date
    $Certificados = @()

    foreach ($StorePath in @("Cert:\CurrentUser\My", "Cert:\LocalMachine\My")) {
        try {
            $Certificados += @(Get-ChildItem -Path $StorePath -ErrorAction SilentlyContinue | Where-Object {
                $_.HasPrivateKey -and
                $_.NotAfter -gt $Agora -and
                (
                    $_.EnhancedKeyUsageList.Count -eq 0 -or
                    ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq "1.3.6.1.5.5.7.3.2" -or $_.FriendlyName -match "Client|Cliente" })
                )
            })
        }
        catch {
            LogMsg "AVISO: Falha ao verificar certificados em ${StorePath}: $($_.Exception.Message)"
        }
    }

    if ($Certificados.Count -eq 0) {
        LogMsg "AVISO: Nenhum certificado cliente valido com chave privada foi localizado."
        return
    }

    LogMsg "Certificados cliente validos encontrados: $($Certificados.Count)"

    foreach ($Cert in @($Certificados | Sort-Object NotAfter -Descending | Select-Object -First 5)) {
        LogMsg "Certificado: Subject='$($Cert.Subject)' Vence=$($Cert.NotAfter.ToString('yyyy-MM-dd')) Thumbprint=$($Cert.Thumbprint)"
    }
}

function TestarConexaoSefazTls12 {
    $UrlTeste = "https://www.nfe.fazenda.gov.br/portal/"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $Resposta = Invoke-WebRequest -UseBasicParsing -Uri $UrlTeste -TimeoutSec 30 -ErrorAction Stop
        LogMsg "Teste HTTPS SEFAZ OK: $UrlTeste StatusCode=$($Resposta.StatusCode)"
    }
    catch {
        LogMsg "AVISO: Teste HTTPS SEFAZ falhou: $($_.Exception.Message)"
    }
}

function ConfigurarSslTlsSefaz {
    ConfigurarTimeZoneSefaz
    SincronizarHoraSefaz
    ConfigurarTls12Sefaz
    InstalarCadeiaCertificado
    VerificarCertificadosClienteSefaz
    TestarConexaoSefazTls12
    LogMsg "Procedimento SSL/TLS 1.2 SEFAZ finalizado."
}

function BaixarRadminVpnSeNecessario {
    if (Test-Path $RadminVpnExe) {
        return
    }

    LogMsg "Arquivo Radmin VPN nao encontrado na pasta temporaria. Baixando do site oficial..."
    Invoke-WebRequest -UseBasicParsing -Uri $RadminVpnUrl -OutFile $RadminVpnExe
    LogMsg "Radmin VPN baixado."
}

function ObterExecutavelRadminVpn {
    $Candidatos = @()

    if (![string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $Candidatos += (Join-Path ${env:ProgramFiles(x86)} "Radmin VPN\RvRvpnGui.exe")
    }

    if (![string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $Candidatos += (Join-Path $env:ProgramFiles "Radmin VPN\RvRvpnGui.exe")
    }

    foreach ($Candidato in $Candidatos) {
        if (Test-Path $Candidato) {
            return $Candidato
        }
    }

    $Encontrado = Get-ChildItem -Path @(${env:ProgramFiles(x86)}, $env:ProgramFiles) -Filter "RvRvpnGui.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\Radmin VPN\\" } |
        Select-Object -First 1

    if ($Encontrado) {
        return $Encontrado.FullName
    }

    return ""
}

function AbrirRadminVpn {
    $Executavel = ObterExecutavelRadminVpn

    if ([string]::IsNullOrWhiteSpace($Executavel)) {
        LogMsg "AVISO: RvRvpnGui.exe nao localizado para abrir a interface do Radmin VPN."
        return
    }

    LogMsg "Abrindo interface do Radmin VPN: $Executavel"
    Start-Process -FilePath $Executavel -ErrorAction Stop
}

function PararProcessosRadminVpn {
    $Servicos = @("RvControlSvc", "RadminVPN")

    foreach ($Servico in $Servicos) {
        try {
            $Svc = Get-Service -Name $Servico -ErrorAction SilentlyContinue
            if ($Svc -and $Svc.Status -ne "Stopped") {
                LogMsg "Parando servico Radmin VPN: $Servico"
                Stop-Service -Name $Servico -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
        }
    }

    foreach ($Nome in @("RvRvpnGui", "RvControlSvc", "RadminVPN", "Radmin_VPN_2.0.4899.9")) {
        try {
            Get-Process -Name $Nome -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        catch {
        }
    }
}

function ObterInstalacoesRadminVpn {
    $Paths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($Path in $Paths) {
        Get-ItemProperty -Path $Path -ErrorAction SilentlyContinue | Where-Object {
            $_.DisplayName -match "(?i)Radmin\s*VPN"
        }
    }
}

function ExecutarComandoDesinstalacaoRadmin {
    param(
        [string]$Comando,
        [string]$DisplayName
    )

    if ([string]::IsNullOrWhiteSpace($Comando)) {
        return $false
    }

    LogMsg "Executando desinstalador Radmin VPN ($DisplayName): $Comando"

    $Exe = ""
    $Args = ""

    if ($Comando -match '^\s*"([^"]+)"\s*(.*)$') {
        $Exe = $Matches[1]
        $Args = $Matches[2]
    }
    elseif ($Comando.Trim() -match '^\s*(.+?\.exe)\s*(.*)$') {
        $Exe = $Matches[1]
        $Args = $Matches[2]
    }
    else {
        $Partes = $Comando.Trim().Split(" ", 2)
        $Exe = $Partes[0]
        if ($Partes.Count -gt 1) {
            $Args = $Partes[1]
        }
    }

    $Exe = [Environment]::ExpandEnvironmentVariables($Exe.Trim())
    $Args = $Args.Trim()

    if ($Exe -match "(?i)msiexec(\.exe)?$") {
        if ($Args -match "(?i)(^|\s)/I\s*\{") {
            $Args = [regex]::Replace($Args, "(?i)(^|\s)/I(?=\s*\{)", '$1/X', 1)
            LogMsg "Comando MSI ajustado para remocao: $Exe $Args"
        }

        if ($Args -notmatch "(?i)(/q|/quiet|/qn)") {
            $Args += " /qn"
        }

        if ($Args -notmatch "(?i)norestart") {
            $Args += " /norestart"
        }
    }
    else {
        if ($Args -notmatch "(?i)/(VERYSILENT|SILENT|quiet|qn)") {
            $Args += " /VERYSILENT"
        }

        if ($Args -notmatch "(?i)NORESTART") {
            $Args += " /NORESTART"
        }
    }

    try {
        $Processo = Start-Process -FilePath $Exe -ArgumentList $Args -Wait -PassThru -ErrorAction Stop
        LogMsg "Desinstalador Radmin VPN finalizado. ExitCode: $($Processo.ExitCode)"
        return ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 1605 -or $Processo.ExitCode -eq 3010)
    }
    catch {
        LogMsg "AVISO: Falha ao executar desinstalador Radmin VPN: $($_.Exception.Message)"
        return $false
    }
}

function RemoverPastasRadminVpn {
    $Pastas = @()

    if (![string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $Pastas += (Join-Path ${env:ProgramFiles(x86)} "Radmin VPN")
    }

    if (![string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $Pastas += (Join-Path $env:ProgramFiles "Radmin VPN")
    }

    foreach ($Pasta in @($Pastas | Select-Object -Unique)) {
        if (Test-Path $Pasta) {
            try {
                LogMsg "Removendo pasta antiga do Radmin VPN: $Pasta"
                Remove-Item -LiteralPath $Pasta -Recurse -Force -ErrorAction Stop
            }
            catch {
                LogMsg "AVISO: Nao foi possivel remover pasta ${Pasta}: $($_.Exception.Message)"
            }
        }
    }
}

function RemoverRadminVpnExistente {
    LogMsg "Verificando instalacao existente do Radmin VPN."
    PararProcessosRadminVpn

    $Instalacoes = @(ObterInstalacoesRadminVpn)

    if ($Instalacoes.Count -eq 0) {
        LogMsg "Nenhuma instalacao anterior do Radmin VPN encontrada no registro."
    }

    foreach ($App in $Instalacoes) {
        $Nome = if ([string]::IsNullOrWhiteSpace($App.DisplayName)) { "Radmin VPN" } else { $App.DisplayName }
        $Comando = $App.QuietUninstallString

        if ([string]::IsNullOrWhiteSpace($Comando)) {
            $Comando = $App.UninstallString
        }

        if (![string]::IsNullOrWhiteSpace($Comando)) {
            [void](ExecutarComandoDesinstalacaoRadmin -Comando $Comando -DisplayName $Nome)
        }
        else {
            LogMsg "AVISO: Instalacao encontrada sem comando de desinstalacao: $Nome"
        }
    }

    Start-Sleep -Seconds 2
    PararProcessosRadminVpn
    RemoverPastasRadminVpn
}

function InstalarRadminVpn {
    BaixarRadminVpnSeNecessario

    if (!(Test-Path $RadminVpnExe)) {
        throw "Instalador do Radmin VPN nao encontrado: $RadminVpnExe"
    }

    Unblock-File -LiteralPath $RadminVpnExe -ErrorAction SilentlyContinue
    RemoverRadminVpnExistente

    $InstallLog = Join-Path $Base "RadminVPN-Install.log"
    $Argumentos = "/VERYSILENT /NORESTART /LOG=`"$InstallLog`""

    LogMsg "Instalando Radmin VPN em modo silencioso."
    LogMsg "Arquivo: $RadminVpnExe"
    LogMsg "Argumentos: $Argumentos"

    $Processo = Start-Process -FilePath $RadminVpnExe -ArgumentList $Argumentos -Wait -PassThru -ErrorAction Stop
    LogMsg "Instalador Radmin VPN finalizado. ExitCode: $($Processo.ExitCode). Log: $InstallLog"

    if ($Processo.ExitCode -ne 0 -and $Processo.ExitCode -ne 3010) {
        throw "Instalacao do Radmin VPN retornou codigo $($Processo.ExitCode)."
    }

    Start-Sleep -Seconds 2
    AbrirRadminVpn
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

function Test-DotNet48 {
    $Release = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Release -ErrorAction SilentlyContinue).Release

    if ($null -eq $Release) {
        return $false
    }

    return ([int]$Release -ge 528040)
}

function BaixarNet48SeNecessario {
    if (Test-Path $Net48) {
        return
    }

    LogMsg "Arquivo dotnet48.exe nao encontrado na pasta temporaria. Baixando do release..."
    Invoke-WebRequest -UseBasicParsing -Uri $Net48Url -OutFile $Net48
    LogMsg "dotnet48.exe baixado."
}

function InstalarNet48 {
    if (Test-DotNet48) {
        LogMsg ".NET Framework 4.8 ja esta instalado."
        return
    }

    BaixarNet48SeNecessario

    $Instalado = InstalarExe $Net48 "/q /norestart" ".NET Framework 4.8 Offline"

    if (Test-DotNet48) {
        LogMsg ".NET Framework 4.8 instalado e detectado."
        return
    }

    if ($Instalado) {
        LogMsg "AVISO: Instalador do .NET Framework 4.8 finalizou, mas pode ser necessario reiniciar para detectar."
        return
    }

    throw "Falha ao instalar .NET Framework 4.8."
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
    LogMsg "Parando o servico de spooler de impressao..."
    ExecutarComandoManutencao -Nome "Parar spooler de impressao" -Arquivo "net.exe" -Argumentos @("stop", "spooler") -IgnorarErro | Out-Null

    $Fila = Join-Path $env:SystemRoot "System32\Spool\Printers"

    if (Test-Path $Fila) {
        LogMsg "Limpando a fila de impressao..."
        $PadraoFila = Join-Path $Fila "*"
        ExecutarComandoManutencao -Nome "Limpar fila de impressao" -Arquivo "cmd.exe" -Argumentos @("/c", "del", "/Q", "/F", "/S", "`"$PadraoFila`"") -IgnorarErro | Out-Null
    }
    else {
        LogMsg "Pasta da fila de impressao nao encontrada: $Fila"
    }

    LogMsg "Iniciando o servico de spooler de impressao..."
    ExecutarComandoManutencao -Nome "Iniciar spooler de impressao" -Arquivo "net.exe" -Argumentos @("start", "spooler")
    LogMsg "Spooler de impressao resetado com sucesso."
}

function ObterNomeArquivoSeguro {
    param([string]$Nome)

    if ([string]::IsNullOrWhiteSpace($Nome)) {
        return ""
    }

    return [System.IO.Path]::GetFileName($Nome)
}

function ObterInstaladoresImpressora {
    param([string]$Raiz)

    $Extensoes = @(".exe", ".msi", ".bat", ".cmd", ".inf")
    $Arquivos = @(Get-ChildItem -LiteralPath $Raiz -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $Extensoes -contains $_.Extension.ToLowerInvariant()
    })

    $Pontuados = foreach ($Arquivo in $Arquivos) {
        $Relativo = $Arquivo.FullName.Substring($Raiz.TrimEnd("\").Length).TrimStart("\")
        $Profundidade = @($Relativo -split "\\").Count
        $Pontuacao = 1000

        switch ($Arquivo.Extension.ToLowerInvariant()) {
            ".exe" { $Pontuacao -= 400 }
            ".msi" { $Pontuacao -= 350 }
            ".bat" { $Pontuacao -= 250 }
            ".cmd" { $Pontuacao -= 250 }
            ".inf" { $Pontuacao -= 100 }
        }

        if ($Arquivo.Name -match "(?i)(setup|install|instal|driver|spooler|printer|zsu|apd|tanca|daruma|bematech|elgin|epson|perto|sweda|zebra|kp|feasso|gains)") {
            $Pontuacao -= 120
        }

        $Pontuacao += ($Profundidade * 10)

        [pscustomobject]@{
            Caminho = $Arquivo.FullName
            Relativo = $Relativo
            Pontuacao = $Pontuacao
        }
    }

    @($Pontuados | Sort-Object Pontuacao, Relativo)
}

function IniciarInstaladorImpressora {
    param([string]$Caminho)

    if (!(Test-Path $Caminho)) {
        throw "Instalador nao encontrado: $Caminho"
    }

    Unblock-File -LiteralPath $Caminho -ErrorAction SilentlyContinue

    $Extensao = [System.IO.Path]::GetExtension($Caminho).ToLowerInvariant()
    $Pasta = Split-Path -Parent $Caminho

    LogMsg "Abrindo instalador de impressora: $Caminho"

    switch ($Extensao) {
        ".msi" {
            Start-Process -FilePath "msiexec.exe" -ArgumentList @("/i", "`"$Caminho`"") -WorkingDirectory $Pasta | Out-Null
        }
        ".bat" {
            Start-Process -FilePath "cmd.exe" -ArgumentList @("/c", "`"$Caminho`"") -WorkingDirectory $Pasta | Out-Null
        }
        ".cmd" {
            Start-Process -FilePath "cmd.exe" -ArgumentList @("/c", "`"$Caminho`"") -WorkingDirectory $Pasta | Out-Null
        }
        ".inf" {
            ExecutarComandoManutencao -Nome "Instalar driver INF de impressora" -Arquivo "pnputil.exe" -Argumentos @("/add-driver", $Caminho, "/install")
        }
        default {
            Start-Process -FilePath $Caminho -WorkingDirectory $Pasta | Out-Null
        }
    }
}

function ConverterListaArgumentos {
    param([string]$Texto)

    if ([string]::IsNullOrWhiteSpace($Texto)) {
        return @()
    }

    @($Texto -split [regex]::Escape("|||") | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function RemoverImpressorasEDriversSelecionados {
    $Impressoras = @(ConverterListaArgumentos -Texto $RemoverImpressoras)
    $Drivers = @(ConverterListaArgumentos -Texto $RemoverDriversImpressora)

    if ($Impressoras.Count -eq 0 -and $Drivers.Count -eq 0) {
        LogMsg "Nenhuma impressora ou driver atual selecionado para remocao."
        return
    }

    LogMsg "Remocao previa selecionada: $($Impressoras.Count) impressora(s), $($Drivers.Count) driver(s)."

    foreach ($NomeImpressora in $Impressoras) {
        try {
            $Impressora = Get-Printer -Name $NomeImpressora -ErrorAction SilentlyContinue

            if ($null -eq $Impressora) {
                LogMsg "AVISO: Impressora nao encontrada para remover: $NomeImpressora"
                continue
            }

            LogMsg "Removendo impressora: $NomeImpressora"
            Remove-Printer -Name $NomeImpressora -ErrorAction Stop
            LogMsg "Impressora removida: $NomeImpressora"
        }
        catch {
            LogMsg "AVISO: Falha ao remover impressora ${NomeImpressora}: $($_.Exception.Message)"
        }
    }

    if ($Impressoras.Count -gt 0) {
        ResetarImpressora
    }

    foreach ($NomeDriver in $Drivers) {
        try {
            $Driver = Get-PrinterDriver -Name $NomeDriver -ErrorAction SilentlyContinue

            if ($null -eq $Driver) {
                LogMsg "AVISO: Driver de impressora nao encontrado para remover: $NomeDriver"
                continue
            }

            LogMsg "Removendo driver de impressora: $NomeDriver"
            Remove-PrinterDriver -Name $NomeDriver -ErrorAction Stop
            LogMsg "Driver de impressora removido: $NomeDriver"
        }
        catch {
            LogMsg "AVISO: Falha ao remover driver ${NomeDriver}: $($_.Exception.Message)"
            LogMsg "Tentando resetar spooler e remover novamente: $NomeDriver"

            try {
                ResetarImpressora
                Remove-PrinterDriver -Name $NomeDriver -ErrorAction Stop
                LogMsg "Driver de impressora removido apos reset do spooler: $NomeDriver"
            }
            catch {
                LogMsg "AVISO: Nao foi possivel remover driver ${NomeDriver}: $($_.Exception.Message)"
            }
        }
    }
}

function InstalarDriverImpressora {
    if ([string]::IsNullOrWhiteSpace($ImpressoraArquivo)) {
        throw "Nenhum driver de impressora foi selecionado na GUI."
    }

    $ArquivoSeguro = ObterNomeArquivoSeguro -Nome $ImpressoraArquivo
    $Zip = Join-Path $Base $ArquivoSeguro

    if (!(Test-Path $Zip)) {
        throw "Pacote de driver nao encontrado: $Zip"
    }

    RemoverImpressorasEDriversSelecionados

    $NomeBase = [System.IO.Path]::GetFileNameWithoutExtension($ArquivoSeguro)
    $DestinoTemp = Join-Path $Base ("driver_impressora_" + $NomeBase + "_" + (Get-Date -Format "yyyyMMddHHmmss"))

    LogMsg "Marca selecionada: $ImpressoraMarca"
    LogMsg "Modelo selecionado: $ImpressoraModelo"
    LogMsg "Pacote selecionado: $ArquivoSeguro"
    LogMsg "Extraindo driver de impressora para: $DestinoTemp"

    New-Item -ItemType Directory -Path $DestinoTemp -Force | Out-Null
    Expand-Archive -LiteralPath $Zip -DestinationPath $DestinoTemp -Force

    $Instalador = ""

    if (![string]::IsNullOrWhiteSpace($ImpressoraInstalador)) {
        $Candidato = Join-Path $DestinoTemp $ImpressoraInstalador

        if (Test-CaminhoDentroOuIgual -BasePath $DestinoTemp -Path $Candidato -and (Test-Path $Candidato)) {
            $Instalador = $Candidato
        }
        else {
            LogMsg "AVISO: Instalador informado no indice nao foi encontrado: $ImpressoraInstalador"
        }
    }

    if ([string]::IsNullOrWhiteSpace($Instalador)) {
        $Candidatos = @(ObterInstaladoresImpressora -Raiz $DestinoTemp)

        if ($Candidatos.Count -eq 0) {
            throw "Nenhum instalador foi encontrado dentro do pacote $ArquivoSeguro."
        }

        $Instalador = $Candidatos[0].Caminho
        LogMsg "Instalador detectado automaticamente: $($Candidatos[0].Relativo)"
    }

    IniciarInstaladorImpressora -Caminho $Instalador
    LogMsg "Instalador iniciado. Arquivos extraidos mantidos em: $DestinoTemp"
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

function GarantirTipoNativeProcess {
    if (([System.Management.Automation.PSTypeName]"TekSoftware.NativeProcess").Type) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TekSoftware
{
    public static class NativeProcess
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, bool bInheritHandle, UInt32 dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            UInt32 dwDesiredAccess,
            IntPtr lpTokenAttributes,
            Int32 ImpersonationLevel,
            Int32 TokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            UInt32 dwLogonFlags,
            string lpApplicationName,
            string lpCommandLine,
            UInt32 dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public UInt32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public UInt32 dwX;
            public UInt32 dwY;
            public UInt32 dwXSize;
            public UInt32 dwYSize;
            public UInt32 dwXCountChars;
            public UInt32 dwYCountChars;
            public UInt32 dwFillAttribute;
            public UInt32 dwFlags;
            public UInt16 wShowWindow;
            public UInt16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public UInt32 dwProcessId;
            public UInt32 dwThreadId;
        }
    }
}
'@
}

function ObterExplorerUsuarioAtual {
    $UsuarioAtual = [Security.Principal.WindowsIdentity]::GetCurrent().Name

    try {
        $Processos = @(Get-CimInstance Win32_Process -Filter "name='explorer.exe'" -ErrorAction SilentlyContinue | ForEach-Object {
            $Processo = $_
            $Owner = Invoke-CimMethod -InputObject $Processo -MethodName GetOwner -ErrorAction SilentlyContinue

            if ($Owner -and "$($Owner.Domain)\$($Owner.User)" -ieq $UsuarioAtual) {
                $Processo
            }
        })

        return @($Processos | Sort-Object ProcessId -Descending | Select-Object -First 1)
    }
    catch {
        LogMsg "AVISO: Falha ao localizar Explorer do usuario: $($_.Exception.Message)"
        return @()
    }
}

function IniciarCmdComTokenExplorer {
    param([string]$CmdPath)

    $Explorer = @(ObterExplorerUsuarioAtual | Select-Object -First 1)

    if ($Explorer.Count -eq 0 -or $null -eq $Explorer[0]) {
        return $false
    }

    GarantirTipoNativeProcess

    $ProcessHandle = [IntPtr]::Zero
    $TokenHandle = [IntPtr]::Zero
    $DuplicatedToken = [IntPtr]::Zero
    $ProcessInfo = New-Object TekSoftware.NativeProcess+PROCESS_INFORMATION

    try {
        $ProcessId = [uint32]$Explorer[0].ProcessId
        $ProcessHandle = [TekSoftware.NativeProcess]::OpenProcess(0x1000, $false, $ProcessId)

        if ($ProcessHandle -eq [IntPtr]::Zero) {
            $ProcessHandle = [TekSoftware.NativeProcess]::OpenProcess(0x0400, $false, $ProcessId)
        }

        if ($ProcessHandle -eq [IntPtr]::Zero) {
            LogMsg "AVISO: Nao foi possivel abrir o token do Explorer. Erro Win32: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
            return $false
        }

        $TokenAccess = [uint32]0x0000018F

        if (-not [TekSoftware.NativeProcess]::OpenProcessToken($ProcessHandle, $TokenAccess, [ref]$TokenHandle)) {
            LogMsg "AVISO: Nao foi possivel abrir o token do Explorer. Erro Win32: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
            return $false
        }

        if (-not [TekSoftware.NativeProcess]::DuplicateTokenEx($TokenHandle, $TokenAccess, [IntPtr]::Zero, 2, 1, [ref]$DuplicatedToken)) {
            LogMsg "AVISO: Nao foi possivel duplicar o token do Explorer. Erro Win32: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
            return $false
        }

        $StartupInfo = New-Object TekSoftware.NativeProcess+STARTUPINFO
        $StartupInfo.cb = [uint32][Runtime.InteropServices.Marshal]::SizeOf($StartupInfo)
        $StartupInfo.lpDesktop = "winsta0\default"
        $StartupInfo.dwFlags = 1
        $StartupInfo.wShowWindow = 0

        $CmdExe = Join-Path $env:WINDIR "System32\cmd.exe"
        $CommandLine = "`"$CmdExe`" /c `"$CmdPath`""
        $WorkingDirectory = Split-Path -Parent $CmdPath

        $Criado = [TekSoftware.NativeProcess]::CreateProcessWithTokenW(
            $DuplicatedToken,
            1,
            $CmdExe,
            $CommandLine,
            0x08000000,
            [IntPtr]::Zero,
            $WorkingDirectory,
            [ref]$StartupInfo,
            [ref]$ProcessInfo
        )

        if (-not $Criado) {
            LogMsg "AVISO: Nao foi possivel executar cmd com token do Explorer. Erro Win32: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
            return $false
        }

        return $true
    }
    finally {
        if ($ProcessInfo.hThread -ne [IntPtr]::Zero) {
            [void][TekSoftware.NativeProcess]::CloseHandle($ProcessInfo.hThread)
        }

        if ($ProcessInfo.hProcess -ne [IntPtr]::Zero) {
            [void][TekSoftware.NativeProcess]::CloseHandle($ProcessInfo.hProcess)
        }

        if ($DuplicatedToken -ne [IntPtr]::Zero) {
            [void][TekSoftware.NativeProcess]::CloseHandle($DuplicatedToken)
        }

        if ($TokenHandle -ne [IntPtr]::Zero) {
            [void][TekSoftware.NativeProcess]::CloseHandle($TokenHandle)
        }

        if ($ProcessHandle -ne [IntPtr]::Zero) {
            [void][TekSoftware.NativeProcess]::CloseHandle($ProcessHandle)
        }
    }
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
        "echo %ERRORLEVEL% > `"$ExitPath`"",
        "exit /b %ERRORLEVEL%",
        ":main"
    ) + $Comandos + @(
        "exit /b %ERRORLEVEL%"
    )

    Set-Content -LiteralPath $CmdPath -Value $Conteudo -Encoding ASCII -Force

    try {
        $Iniciado = IniciarCmdComTokenExplorer -CmdPath $CmdPath

        if (-not $Iniciado) {
            $Shell = New-Object -ComObject Shell.Application
            $Shell.ShellExecute("cmd.exe", "/c `"$CmdPath`"", "", "open", 0)
        }
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

function RemoverMapeamentoUsuario {
    param([string]$Drive)

    try {
        $Resultado = ExecutarCmdShellUsuarioComTimeout -Comandos @("net use $Drive /delete /y") -TimeoutSegundos 8

        foreach ($Linha in @($Resultado.Output)) {
            if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
                LogMsg "$Linha"
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao remover mapeamento $Drive do usuario: $($_.Exception.Message)"
    }
}

function MapearDriveNoShellUsuario {
    param(
        [string]$Drive,
        [string]$CaminhoRede,
        [string]$HostRede
    )

    $Comandos = @()
    $HostRede = ObterHostCredencial -Valor $HostRede

    if (![string]::IsNullOrWhiteSpace($HostRede)) {
        $Comandos += "cmdkey.exe /delete:$HostRede >nul 2>nul"
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

function Test-TekAplicacaoDisponivel {
    param(
        [string]$Drive,
        [string]$CaminhoRede
    )

    $AlvoDrive = EncontrarTekAplicacaoEmDrive -Drive $Drive
    if (![string]::IsNullOrWhiteSpace($AlvoDrive)) {
        LogMsg "TekAplicacao encontrado no mapeamento: $AlvoDrive"
        return $true
    }

    $AlvoRede = EncontrarTekAplicacaoEmRede -CaminhoRede $CaminhoRede
    if (![string]::IsNullOrWhiteSpace($AlvoRede)) {
        LogMsg "TekAplicacao encontrado no caminho de rede: $AlvoRede"
        return $true
    }

    return $false
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
            if (!(Test-TekAplicacaoDisponivel -Drive $Drive -CaminhoRede $CaminhoRede)) {
                LogMsg "AVISO: $CaminhoRede foi mapeado em $Drive, mas TekAplicacao.exe nao foi encontrado em TekFarma. Removendo e tentando proximo compartilhamento..."
                RemoverMapeamentoUsuario -Drive $Drive
                return ""
            }

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

function AdicionarCandidatoRede {
    param(
        [System.Collections.ArrayList]$Lista,
        [string]$CaminhoRede
    )

    if ([string]::IsNullOrWhiteSpace($CaminhoRede)) {
        return
    }

    $Normalizado = $CaminhoRede.TrimEnd("\")

    foreach ($Item in $Lista) {
        if ($Item.Equals($Normalizado, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    [void]$Lista.Add($Normalizado)
}

function ObterCompartilhamentosServidor {
    param([string]$Servidor)

    $Compartilhamentos = @()

    if ([string]::IsNullOrWhiteSpace($Servidor)) {
        return $Compartilhamentos
    }

    try {
        $Resultado = ExecutarProcessoMapeamentoComTimeout -Arquivo "cmd.exe" -Argumentos "/c net view \\$Servidor" -TimeoutSegundos 10

        foreach ($Linha in @($Resultado.Output)) {
            if ($Linha -match "^\s*(.+?)\s{2,}(Disco|Disk)\b") {
                $Nome = $Matches[1].Trim()

                if (![string]::IsNullOrWhiteSpace($Nome) -and !$Nome.EndsWith('$') -and $Compartilhamentos -notcontains $Nome) {
                    $Compartilhamentos += $Nome
                }
            }
        }
    }
    catch {
        LogMsg "AVISO: Falha ao listar compartilhamentos de ${Servidor}: $($_.Exception.Message)"
    }

    return $Compartilhamentos
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

    $Candidatos = New-Object System.Collections.ArrayList

    if ($Valor.StartsWith("\\")) {
        AdicionarCandidatoRede -Lista $Candidatos -CaminhoRede $Valor
        return @($Candidatos)
    }

    if ($Valor.Contains("\")) {
        AdicionarCandidatoRede -Lista $Candidatos -CaminhoRede ("\\" + $Valor.Trim("\"))
        return @($Candidatos)
    }

    $HostLimpo = $Valor.Trim("\")

    AdicionarCandidatoRede -Lista $Candidatos -CaminhoRede "\\$HostLimpo\TekSoftware"
    AdicionarCandidatoRede -Lista $Candidatos -CaminhoRede "\\$HostLimpo\Tek"

    $Compartilhamentos = @(ObterCompartilhamentosServidor -Servidor $HostLimpo)
    $Ordenados = @($Compartilhamentos | Sort-Object @{ Expression = {
        if ($_ -ieq "TekSoftware") { 0 }
        elseif ($_ -ieq "Tek") { 1 }
        elseif ($_ -match "(?i)tek") { 2 }
        else { 3 }
    } }, @{ Expression = { $_ } })

    foreach ($Compartilhamento in $Ordenados) {
        AdicionarCandidatoRede -Lista $Candidatos -CaminhoRede "\\$HostLimpo\$Compartilhamento"
    }

    $TextoCandidatos = @($Candidatos) -join ", "
    LogMsg "Compartilhamentos candidatos para localizar TekAplicacao.exe: $TextoCandidatos"

    return @($Candidatos)
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

    $HostInicial = ObterHostCredencial -Valor $HostInformado

    if ([string]::IsNullOrWhiteSpace($HostInicial)) {
        $HostInicial = "SERVIDOR"
    }

    HabilitarMapeamentoElevadoNoExplorer
    CriarCredenciaisConvidadoParaHosts -Hosts @($HostInicial)
    RemoverMapeamentosTekSoftware

    foreach ($CaminhoRede in @(ObterCandidatosRedeTekSoftware -HostInformado $HostInformado)) {
        $HostRede = ObterHostDeCaminhoRede -CaminhoRede $CaminhoRede
        CriarCredenciaisConvidadoParaHosts -Hosts @($HostInicial, $HostRede)

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

function ConverterTextoBooleano {
    param([string]$Valor)

    return $Valor -match "(?i)^(1|true|sim|yes|y|s)$"
}

function ObterOrigemTekSoftwareTroca {
    $Valor = $TrocaHostAntigo

    if ([string]::IsNullOrWhiteSpace($Valor)) {
        $Valor = "SERVIDOR"
    }

    $Valor = $Valor.Trim().TrimEnd("\")

    if ($Valor.StartsWith("\\")) {
        if ($Valor -match "^\\\\[^\\]+\\[^\\]+") {
            return $Valor
        }

        return "$Valor\TekSoftware"
    }

    if ($Valor.Contains("\")) {
        return "\\" + $Valor.Trim("\")
    }

    return "\\$Valor\TekSoftware"
}

function ObterPastasExclusaoTroca {
    if ([string]::IsNullOrWhiteSpace($TrocaExcluirPastas)) {
        return @()
    }

    $Pastas = @($TrocaExcluirPastas -split "[,;]" | ForEach-Object { $_.Trim() } | Where-Object { $_ })

    foreach ($Pasta in @($Pastas)) {
        if ($Pasta -ieq "Atualizacao") {
            $Pastas += ("Atualiza" + [char]0x00E7 + [char]0x00E3 + "o")
        }
    }

    @($Pastas | Select-Object -Unique)
}

function ExecutarRobocopyTroca {
    param(
        [string]$Nome,
        [string]$Origem,
        [string]$Destino,
        [string[]]$PastasExcluir = @()
    )

    if (!(Test-Path $Origem)) {
        LogMsg "AVISO: Origem nao acessivel antes do robocopy: $Origem"
    }

    New-Item -ItemType Directory -Path $Destino -Force | Out-Null

    $RoboLog = Join-Path $Base ("troca_servidor_robocopy_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")
    $Argumentos = @($Origem, $Destino, "/E", "/Z", "/R:3", "/W:5", "/MT:16", "/NP", "/IS", "/IT", "/TEE", "/LOG+:$RoboLog")

    if ($PastasExcluir -and $PastasExcluir.Count -gt 0) {
        $Argumentos += "/XD"
        $Argumentos += $PastasExcluir
    }

    LogMsg "Executando ${Nome}: robocopy $($Argumentos -join ' ')"

    & robocopy.exe @Argumentos 2>&1 | ForEach-Object {
        if ($null -ne $_ -and "$_".Trim().Length -gt 0) {
            LogMsg "$_"
        }
    }

    $Codigo = $LASTEXITCODE

    LogMsg "Robocopy finalizado com codigo $Codigo. Log: $RoboLog"

    if ($Codigo -gt 7) {
        throw "Robocopy retornou erro $Codigo."
    }
}

function GarantirCompartilhamentoTekSoftwareSuporte {
    New-Item -ItemType Directory -Path $RaizTekSoftware -Force | Out-Null

    if (Get-Command New-SmbShare -ErrorAction SilentlyContinue) {
        try {
            $Share = Get-SmbShare -Name "TekSoftware" -ErrorAction SilentlyContinue

            if ($Share -and $Share.Path -ne $RaizTekSoftware) {
                LogMsg "Removendo compartilhamento TekSoftware apontando para: $($Share.Path)"
                Remove-SmbShare -Name "TekSoftware" -Force -ErrorAction SilentlyContinue
                $Share = $null
            }

            if (!$Share) {
                LogMsg "Criando compartilhamento TekSoftware -> $RaizTekSoftware"
                New-SmbShare -Name "TekSoftware" -Path $RaizTekSoftware -Description "TekSoftware" | Out-Null
            }

            foreach ($Conta in @("Todos", "Everyone", "Rede", "Network", "Convidado", "Guest")) {
                try {
                    Grant-SmbShareAccess -Name "TekSoftware" -AccountName $Conta -AccessRight Full -Force -ErrorAction Stop | Out-Null
                    LogMsg "Permissao de compartilhamento aplicada para: $Conta"
                }
                catch {
                }
            }
        }
        catch {
            LogMsg "AVISO: Falha ao configurar compartilhamento SMB: $($_.Exception.Message)"
        }
    }
    else {
        LogMsg "AVISO: New-SmbShare nao disponivel neste Windows."
    }

    foreach ($Sid in @("S-1-1-0", "S-1-5-2", "S-1-5-32-546")) {
        try {
            & icacls.exe $RaizTekSoftware /grant "*$Sid`:(OI)(CI)F" /T /C | Out-Null
            LogMsg "Permissao NTFS aplicada para SID $Sid"
        }
        catch {
            LogMsg "AVISO: Falha ao aplicar permissao NTFS para SID ${Sid}: $($_.Exception.Message)"
        }
    }
}

function ExecutarInstalacaoFullTroca {
    $ScriptFull = Join-Path $Base "instalar.ps1"

    if (!(Test-Path $ScriptFull)) {
        throw "Script FULL nao encontrado: $ScriptFull"
    }

    $Tipo = $TrocaTipoVersao
    if ($Tipo -notin @("normal", "i")) {
        $Tipo = "normal"
    }

    LogMsg "Executando FULL no novo servidor: versao=$Tipo"
    $Argumentos = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptFull, "-Modo", "3", "-TipoVersao", $Tipo)
    $Saida = & powershell.exe @Argumentos 2>&1
    $Codigo = $LASTEXITCODE

    foreach ($Linha in @($Saida)) {
        if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
            LogMsg "$Linha"
        }
    }

    if ($Codigo -ne 0) {
        throw "Instalacao FULL retornou codigo $Codigo."
    }
}

function RenomearComputadorTroca {
    param([string]$NovoNome)

    if ($env:COMPUTERNAME -ieq $NovoNome) {
        LogMsg "Computador ja se chama $NovoNome. Reinicio nao sera agendado por renomeacao."
        return $false
    }

    LogMsg "Renomeando computador para $NovoNome"
    Rename-Computer -NewName $NovoNome -Force -ErrorAction Stop
    LogMsg "Renomeacao solicitada. Agendando reinicio em 60 segundos."
    shutdown.exe /r /t 60 /c "Troca de servidor TekSoftware: reinicio necessario para aplicar nome $NovoNome." | Out-Null
    return $true
}

function EscaparTextoScriptTroca {
    param([string]$Texto)

    if ($null -eq $Texto) {
        return ""
    }

    return (($Texto -replace '`', '``') -replace '"', '`"')
}

function ObterHostUncTroca {
    param([string]$Caminho)

    if ($Caminho -match "^\\\\([^\\]+)\\") {
        return $Matches[1]
    }

    return ""
}

function PrepararConexaoOrigemTroca {
    param([string]$Origem)

    $HostOrigem = ObterHostUncTroca -Caminho $Origem

    if ([string]::IsNullOrWhiteSpace($HostOrigem)) {
        return
    }

    LogMsg "Configurando credencial convidado para $HostOrigem."

    try {
        cmdkey.exe /delete:$HostOrigem 2>&1 | Out-Null
    }
    catch {
    }

    try {
        cmdkey.exe /add:$HostOrigem /user:convidado /pass:"" 2>&1 | Out-Null
    }
    catch {
        LogMsg "AVISO: falha ao configurar cmdkey para ${HostOrigem}: $($_.Exception.Message)"
    }

    try {
        net.exe use $Origem /delete /y 2>&1 | Out-Null
    }
    catch {
    }

    try {
        net.exe use $Origem /user:convidado "" /persistent:no 2>&1 | Out-Null
        LogMsg "Conexao de rede preparada para $Origem."
    }
    catch {
        LogMsg "AVISO: falha ao preparar net use para ${Origem}: $($_.Exception.Message)"
    }
}

function ObterParPastasFinaisTroca {
    param(
        [string]$OrigemFinal,
        [string]$DestinoFinal,
        [string]$Pasta
    )

    $OrigemPastaTekFarma = Join-Path (Join-Path $OrigemFinal "TekFarma") $Pasta
    $DestinoPastaTekFarma = Join-Path (Join-Path $DestinoFinal "TekFarma") $Pasta
    $OrigemPasta = Join-Path $OrigemFinal $Pasta
    $DestinoPasta = Join-Path $DestinoFinal $Pasta

    if (Test-Path $OrigemPastaTekFarma) {
        return @{
            Origem = $OrigemPastaTekFarma
            Destino = $DestinoPastaTekFarma
        }
    }

    return @{
        Origem = $OrigemPasta
        Destino = $DestinoPasta
    }
}

function ExecutarCopiaFinalTrocaServidor {
    param(
        [string]$OrigemFinal,
        [string[]]$Pastas
    )

    $PastasUnicas = @($Pastas | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

    if ($PastasUnicas.Count -eq 0) {
        $PastasUnicas = @("NFe", "NFCe")
    }

    LogMsg "Iniciando copia final de troca de servidor."
    LogMsg "Origem final: $OrigemFinal"
    LogMsg "Destino final: $RaizTekSoftware"
    LogMsg "Pastas finais: $($PastasUnicas -join ', ')"

    PrepararConexaoOrigemTroca -Origem $OrigemFinal

    if (!(Test-Path $OrigemFinal)) {
        throw "Origem final nao acessivel: $OrigemFinal"
    }

    foreach ($Pasta in $PastasUnicas) {
        $Par = ObterParPastasFinaisTroca -OrigemFinal $OrigemFinal -DestinoFinal $RaizTekSoftware -Pasta $Pasta

        if (!(Test-Path $Par.Origem)) {
            LogMsg "AVISO: pasta final nao encontrada na origem: $($Par.Origem)"
            continue
        }

        ExecutarRobocopyTroca -Nome "Copia final $Pasta" -Origem $Par.Origem -Destino $Par.Destino
    }

    LogMsg "Copia final de troca de servidor concluida."
}

function CriarScriptCopiaFinalTrocaServidor {
    param(
        [string]$OrigemFinal,
        [string[]]$Pastas
    )

    $DiretorioTroca = Join-Path $RaizTekSoftware "_troca_servidor"
    New-Item -ItemType Directory -Path $DiretorioTroca -Force | Out-Null

    $ScriptFinal = Join-Path $DiretorioTroca "copia_final_troca_servidor.ps1"
    $PastasUnicas = @($Pastas | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

    if ($PastasUnicas.Count -eq 0) {
        $PastasUnicas = @("NFe", "NFCe")
    }

    $PastasJson = $PastasUnicas | ConvertTo-Json -Compress
    $TaskNames = @(
        "TekSoftware Troca Servidor Copia Final Startup",
        "TekSoftware Troca Servidor Copia Final Logon"
    )
    $TaskNamesJson = $TaskNames | ConvertTo-Json -Compress

    $Template = @'
$ErrorActionPreference = "Continue"

$Origem = "__ORIGEM_FINAL__"
$Destino = "__DESTINO_FINAL__"
$BaseDir = "__BASE_DIR__"
$HostOrigem = ""

if ($Origem -match '^\\\\([^\\]+)\\') {
    $HostOrigem = $Matches[1]
}

$Pastas = @(ConvertFrom-Json @"
__PASTAS_JSON__
"@)
$TaskNames = @(ConvertFrom-Json @"
__TASKS_JSON__
"@)

New-Item -ItemType Directory -Path $BaseDir -Force | Out-Null

$Log = Join-Path $BaseDir "copia_final_troca_servidor.log"
$Done = Join-Path $BaseDir "copia_final_concluida.ok"
$LockPath = Join-Path $BaseDir "copia_final.lock"

function LogFinal {
    param([string]$Texto)

    $Linha = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Texto"
    Add-Content -Path $Log -Value $Linha
}

function RemoverTarefasAgendadas {
    foreach ($TaskName in @($TaskNames)) {
        try {
            schtasks.exe /Delete /TN $TaskName /F 2>&1 | Out-Null
        }
        catch {
        }
    }
}

function ConfigurarCredencialOrigem {
    if ([string]::IsNullOrWhiteSpace($HostOrigem)) {
        return
    }

    try {
        LogFinal "Configurando credencial convidado para $HostOrigem."
        cmdkey.exe /delete:$HostOrigem 2>&1 | Out-Null
        cmdkey.exe /add:$HostOrigem /user:convidado /pass:"" 2>&1 | Out-Null
    }
    catch {
        LogFinal "AVISO: falha ao configurar cmdkey para ${HostOrigem}: $($_.Exception.Message)"
    }

    try {
        net.exe use $Origem /delete /y 2>&1 | Out-Null
    }
    catch {
    }

    try {
        net.exe use $Origem /user:convidado "" /persistent:no 2>&1 | Out-Null
        LogFinal "Conexao de rede preparada para $Origem."
    }
    catch {
        LogFinal "AVISO: falha ao preparar net use para ${Origem}: $($_.Exception.Message)"
    }
}

if (Test-Path $Done) {
    LogFinal "Copia final ja estava marcada como concluida. Removendo tarefas agendadas."
    RemoverTarefasAgendadas
    exit 0
}

$Lock = $null

try {
    try {
        $Lock = [System.IO.File]::Open($LockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    }
    catch {
        LogFinal "Outra execucao da copia final ja esta em andamento."
        exit 0
    }

    LogFinal "Iniciando copia final de troca de servidor."
    LogFinal "Origem: $Origem"
    LogFinal "Destino: $Destino"
    LogFinal "Pastas finais: $($Pastas -join ', ')"

    ConfigurarCredencialOrigem

    $OrigemDisponivel = $false

    for ($i = 1; $i -le 60; $i++) {
        if (Test-Path $Origem) {
            $OrigemDisponivel = $true
            break
        }

        LogFinal "Aguardando origem ficar disponivel ($i/60): $Origem"
        Start-Sleep -Seconds 10
    }

    if (!$OrigemDisponivel) {
        LogFinal "ERRO: origem nao ficou disponivel. A tarefa tentara novamente no proximo inicio/logon."
        exit 1
    }

    New-Item -ItemType Directory -Path $Destino -Force | Out-Null

    $Falhas = 0

    foreach ($Pasta in @($Pastas | Select-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($Pasta)) {
            continue
        }

        $OrigemPastaTekFarma = Join-Path (Join-Path $Origem "TekFarma") $Pasta
        $DestinoPastaTekFarma = Join-Path (Join-Path $Destino "TekFarma") $Pasta
        $OrigemPasta = Join-Path $Origem $Pasta
        $DestinoPasta = Join-Path $Destino $Pasta

        if (Test-Path $OrigemPastaTekFarma) {
            $OrigemPasta = $OrigemPastaTekFarma
            $DestinoPasta = $DestinoPastaTekFarma
        }

        if (!(Test-Path $OrigemPasta)) {
            LogFinal "AVISO: pasta final nao encontrada na origem: $OrigemPasta"
            continue
        }

        New-Item -ItemType Directory -Path $DestinoPasta -Force | Out-Null
        $NomeLogSeguro = $Pasta -replace '[\\/:*?"<>|]', '_'
        $RoboLog = Join-Path $BaseDir "robocopy_final_$NomeLogSeguro.log"

        LogFinal "Copiando pasta final: $Pasta"
        robocopy.exe $OrigemPasta $DestinoPasta /E /Z /R:3 /W:5 /MT:16 /NP /IS /IT /TEE /LOG+:$RoboLog | Out-Null
        $Codigo = $LASTEXITCODE
        LogFinal "Robocopy $Pasta finalizado com codigo $Codigo. Log: $RoboLog"

        if ($Codigo -gt 7) {
            $Falhas++
        }
    }

    if ($Falhas -gt 0) {
        LogFinal "Copia final terminou com $Falhas falha(s). A tarefa tentara novamente no proximo inicio/logon."
        exit 1
    }

    New-Item -ItemType File -Path $Done -Force | Out-Null
    LogFinal "Copia final concluida com sucesso."
    RemoverTarefasAgendadas
    exit 0
}
catch {
    LogFinal "ERRO inesperado na copia final: $($_.Exception.Message)"
    exit 1
}
finally {
    if ($Lock) {
        $Lock.Close()
    }

    Remove-Item -LiteralPath $LockPath -Force -ErrorAction SilentlyContinue
}
'@

    $Conteudo = $Template.
        Replace("__ORIGEM_FINAL__", (EscaparTextoScriptTroca $OrigemFinal)).
        Replace("__DESTINO_FINAL__", (EscaparTextoScriptTroca $RaizTekSoftware)).
        Replace("__BASE_DIR__", (EscaparTextoScriptTroca $DiretorioTroca)).
        Replace("__PASTAS_JSON__", $PastasJson).
        Replace("__TASKS_JSON__", $TaskNamesJson)

    Set-Content -LiteralPath $ScriptFinal -Value $Conteudo -Encoding UTF8 -Force
    return $ScriptFinal
}

function AgendarCopiaFinalTrocaServidor {
    param(
        [string]$OrigemFinal,
        [string[]]$Pastas
    )

    $ScriptFinal = CriarScriptCopiaFinalTrocaServidor -OrigemFinal $OrigemFinal -Pastas $Pastas
    $TaskStartup = "TekSoftware Troca Servidor Copia Final Startup"
    $TaskLogon = "TekSoftware Troca Servidor Copia Final Logon"
    $TaskCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$ScriptFinal`""

    foreach ($TaskName in @($TaskStartup, $TaskLogon)) {
        schtasks.exe /Delete /TN $TaskName /F 2>&1 | Out-Null
    }

    LogMsg "Script persistente da copia final criado: $ScriptFinal"
    LogMsg "Origem final agendada: $OrigemFinal"
    LogMsg "Pastas finais agendadas: $($Pastas -join ', ')"

    $SaidaStartup = & schtasks.exe /Create /TN $TaskStartup /SC ONSTART /RU SYSTEM /RL HIGHEST /TR $TaskCommand /F 2>&1
    $CodigoStartup = $LASTEXITCODE

    foreach ($Linha in @($SaidaStartup)) {
        if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
            LogMsg "SCHTASKS startup: $Linha"
        }
    }

    $SaidaLogon = & schtasks.exe /Create /TN $TaskLogon /SC ONLOGON /RL HIGHEST /TR $TaskCommand /F 2>&1
    $CodigoLogon = $LASTEXITCODE

    foreach ($Linha in @($SaidaLogon)) {
        if ($null -ne $Linha -and "$Linha".Trim().Length -gt 0) {
            LogMsg "SCHTASKS logon: $Linha"
        }
    }

    if ($CodigoStartup -ne 0 -and $CodigoLogon -ne 0) {
        throw "Nao foi possivel criar tarefa agendada para copia final."
    }

    LogMsg "Copia final agendada. Ela tentara copiar as pastas finais no proximo inicio do Windows/logon."
}

function ExecutarTrocaServidor {
    $Perfil = $TrocaPerfil.Trim().ToLowerInvariant()

    if ($Perfil -eq "novo") {
        $Origem = ObterOrigemTekSoftwareTroca
        LogMsg "Modo troca: novo servidor"
        LogMsg "Origem TekSoftware: $Origem"
        LogMsg "Destino TekSoftware: $RaizTekSoftware"

        if (ConverterTextoBooleano $TrocaConfigurarRede) {
            ExecutarPasso "Configurar rede avancada" {
                ConfigurarRedeAvancada
            }

            ExecutarPasso "Compartilhar C:\TekSoftware" {
                GarantirCompartilhamentoTekSoftwareSuporte
            }
        }

        if (ConverterTextoBooleano $TrocaInstalarFirebird) {
            ExecutarPasso "Instalar/reinstalar Firebird no novo servidor" {
                ReinstalarFirebird
            }
        }

        if (ConverterTextoBooleano $TrocaCopiarPrincipal) {
            ExecutarPasso "Pre-copiar TekSoftware sem pastas pesadas" {
                ExecutarRobocopyTroca -Nome "Pre-copia TekSoftware" -Origem $Origem -Destino $RaizTekSoftware -PastasExcluir @(ObterPastasExclusaoTroca)
            }
        }

        if (ConverterTextoBooleano $TrocaInstalarFull) {
            ExecutarPasso "Executar FULL versao + Crystal" {
                ExecutarInstalacaoFullTroca
            }
        }

        $DeveCopiarFinal = ConverterTextoBooleano $TrocaCopiarFinal
        $RenomeacaoMarcada = ConverterTextoBooleano $TrocaRenomearReiniciar
        $ReinicioAgendado = $false

        if (ConverterTextoBooleano $TrocaRenomearReiniciar) {
            LogMsg "Renomeacao/reinicio selecionados. Verificando se este computador ja esta com o nome SERVIDOR."
            $ReinicioAgendado = [bool](RenomearComputadorTroca -NovoNome "SERVIDOR")

            if ($ReinicioAgendado) {
                if ($DeveCopiarFinal) {
                    ExecutarPasso "Agendar copia final apos reinicio" {
                        AgendarCopiaFinalTrocaServidor -OrigemFinal "\\ANTIGO\TekSoftware" -Pastas @(ObterPastasExclusaoTroca)
                    }
                }

                LogMsg "Reinicio agendado. A copia final ficara para o proximo inicio/logon."
                return
            }

            LogMsg "Nome do computador ja esta correto. A troca vai continuar sem reiniciar."
        }

        if ($DeveCopiarFinal) {
            $OrigemFinal = $Origem

            if ($RenomeacaoMarcada) {
                $OrigemFinal = "\\ANTIGO\TekSoftware"
            }

            LogMsg "====================================="
            LogMsg "INICIANDO: Copiar pastas finais"

            try {
                ExecutarCopiaFinalTrocaServidor -OrigemFinal $OrigemFinal -Pastas @(ObterPastasExclusaoTroca)
                LogMsg "FINALIZADO: Copiar pastas finais"
            }
            catch {
                LogMsg "ERRO no passo 'Copiar pastas finais': $($_.Exception.Message)"
                throw
            }
        }

        LogMsg "Troca do novo servidor processada. Fluxo humano: confirme os terminais e acompanhe a copia final se ela foi marcada."
        return
    }

    if ($Perfil -eq "antigo") {
        LogMsg "Modo troca: servidor antigo"

        ExecutarPasso "Remover Firebird do servidor antigo" {
            RemoverFirebirdExistente
        }

        if (ConverterTextoBooleano $TrocaRenomearReiniciar) {
            $null = RenomearComputadorTroca -NovoNome "ANTIGO"
        }
        else {
            LogMsg "Renomeacao para ANTIGO nao selecionada."
        }

        return
    }

    throw "Perfil de troca invalido: $TrocaPerfil"
}

Clear-Host

LogMsg "====================================="
LogMsg "SUPORTE TEKSOFTWARE"
LogMsg "====================================="
LogMsg "Acoes recebidas: $Acoes"
LogMsg "HostServidor recebido: $HostServidor"
if (![string]::IsNullOrWhiteSpace($ImpressoraArquivo)) {
    LogMsg "Impressora recebida: $ImpressoraMarca / $ImpressoraModelo / $ImpressoraArquivo"
    LogMsg "Remocao previa recebida: impressoras=$(@(ConverterListaArgumentos -Texto $RemoverImpressoras).Count), drivers=$(@(ConverterListaArgumentos -Texto $RemoverDriversImpressora).Count)"
}
if (![string]::IsNullOrWhiteSpace($TrocaPerfil)) {
    LogMsg "Troca de servidor recebida: perfil=$TrocaPerfil hostAntigo=$TrocaHostAntigo versao=$TrocaTipoVersao"
}
if ($Acoes -match "(?i)ssltlssefaz") {
    LogMsg "UTC/Timezone SEFAZ recebido: $SefazTimeZoneId"
}
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
        "ssltlssefaz" {
            ExecutarPassoAdmin "SSL/TLS 1.2 SEFAZ" {
                ConfigurarSslTlsSefaz
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
        "radminvpn" {
            ExecutarPassoAdmin "Instalar Radmin VPN" {
                InstalarRadminVpn
            }
        }
        "firebird" {
            ExecutarPassoAdmin "Reinstalar Firebird" {
                ReinstalarFirebird
            }
        }
        "trocaservidor" {
            ExecutarPassoAdmin "Troca de servidor" {
                ExecutarTrocaServidor
            }
        }
        "net35" {
            ExecutarPassoAdmin "Instalar .NET Framework 3.5" {
                InstalarNet35
            }
        }
        "net48" {
            ExecutarPassoAdmin "Instalar .NET Framework 4.8" {
                InstalarNet48
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
        "impressora" {
            ExecutarPassoAdmin "Instalar driver de impressora" {
                InstalarDriverImpressora
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

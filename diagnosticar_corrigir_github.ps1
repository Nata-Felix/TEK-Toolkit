param(
    [switch]$Elevado,
    [switch]$SomenteDiagnostico
)

$ErrorActionPreference = "Continue"
$ScriptPath = $MyInvocation.MyCommand.Path
$Desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)

if ([string]::IsNullOrEmpty($Desktop)) {
    $Desktop = [System.IO.Path]::GetTempPath()
}

$LogFile = Join-Path $Desktop ("Diagnostico_GitHub_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$RawUrl = "https://raw.githubusercontent.com/Nata-Felix/TEK-Toolkit/refs/heads/main/instalar.ps1"
$ApiUrl = "https://api.github.com/repos/Nata-Felix/TEK-Toolkit/contents/instalar.ps1?ref=main"
$ReleaseUrl = "https://github.com/Nata-Felix/TEK-Toolkit/releases/download/v1.0/TekFarmaInstaller.exe"
$Alteracoes = 0

function Write-Log {
    param(
        [string]$Mensagem,
        [string]$Nivel = "INFO"
    )

    $Linha = "{0} [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Nivel, $Mensagem
    $Cor = "Gray"

    if ($Nivel -eq "OK") { $Cor = "Green" }
    elseif ($Nivel -eq "AVISO") { $Cor = "Yellow" }
    elseif ($Nivel -eq "ERRO") { $Cor = "Red" }

    Write-Host $Linha -ForegroundColor $Cor

    try {
        [System.IO.File]::AppendAllText($LogFile, $Linha + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
    }
    catch {
    }
}

function Test-Administrador {
    try {
        $Identidade = [Security.Principal.WindowsIdentity]::GetCurrent()
        $Principal = New-Object Security.Principal.WindowsPrincipal($Identidade)
        return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    catch {
        return $false
    }
}

function Set-Dword {
    param(
        [string]$Caminho,
        [string]$Nome,
        [int]$Valor
    )

    try {
        if (!(Test-Path -LiteralPath $Caminho)) {
            New-Item -Path $Caminho -Force -ErrorAction Stop | Out-Null
        }

        $Atual = $null
        try {
            $Atual = (Get-ItemProperty -LiteralPath $Caminho -Name $Nome -ErrorAction Stop).$Nome
        }
        catch {
        }

        if ($null -eq $Atual -or [int]$Atual -ne $Valor) {
            New-ItemProperty -LiteralPath $Caminho -Name $Nome -Value $Valor -PropertyType DWord -Force -ErrorAction Stop | Out-Null
            $script:Alteracoes++
            Write-Log "$Caminho\$Nome configurado para $Valor." "OK"
        }
        else {
            Write-Log "$Caminho\$Nome ja estava correto." "OK"
        }
    }
    catch {
        Write-Log "Nao foi possivel configurar $Caminho\$Nome. $($_.Exception.Message)" "ERRO"
    }
}

function Enable-DwordBits {
    param(
        [string]$Caminho,
        [string]$Nome,
        [int]$Bits
    )

    try {
        if (!(Test-Path -LiteralPath $Caminho)) {
            New-Item -Path $Caminho -Force -ErrorAction Stop | Out-Null
        }

        $Atual = 0
        try {
            $Atual = [int](Get-ItemProperty -LiteralPath $Caminho -Name $Nome -ErrorAction Stop).$Nome
        }
        catch {
        }

        $Novo = $Atual -bor $Bits

        if ($Novo -ne $Atual) {
            New-ItemProperty -LiteralPath $Caminho -Name $Nome -Value $Novo -PropertyType DWord -Force -ErrorAction Stop | Out-Null
            $script:Alteracoes++
            Write-Log "$Caminho\$Nome recebeu suporte TLS 1.2 (valor $Novo)." "OK"
        }
        else {
            Write-Log "$Caminho\$Nome ja possui TLS 1.2 habilitado." "OK"
        }
    }
    catch {
        Write-Log "Nao foi possivel configurar $Caminho\$Nome. $($_.Exception.Message)" "ERRO"
    }
}

function Test-DnsHost {
    param([string]$HostName)

    try {
        $Enderecos = [Net.Dns]::GetHostAddresses($HostName)
        $Lista = @($Enderecos | ForEach-Object { $_.IPAddressToString }) -join ", "
        Write-Log "DNS $HostName -> $Lista" "OK"
        return $true
    }
    catch {
        Write-Log "DNS nao resolveu $HostName. $($_.Exception.Message)" "ERRO"
        return $false
    }
}

function Test-Porta443 {
    param([string]$HostName)

    $Cliente = New-Object Net.Sockets.TcpClient

    try {
        $Async = $Cliente.BeginConnect($HostName, 443, $null, $null)

        if (!$Async.AsyncWaitHandle.WaitOne(8000, $false)) {
            throw "Tempo limite ao conectar na porta 443."
        }

        $Cliente.EndConnect($Async)
        Write-Log "Conexao TCP com $HostName`:443 realizada." "OK"
        return $true
    }
    catch {
        Write-Log "Nao foi possivel conectar a $HostName`:443. $($_.Exception.Message)" "ERRO"
        return $false
    }
    finally {
        $Cliente.Close()
    }
}

function Test-WebUrl {
    param(
        [string]$Nome,
        [string]$Url
    )

    try {
        $Request = [Net.HttpWebRequest]::Create($Url)
        $Request.Method = "HEAD"
        $Request.AllowAutoRedirect = $true
        $Request.Timeout = 20000
        $Request.ReadWriteTimeout = 20000
        $Request.UserAgent = "TEK-Toolkit-Diagnostico/1.0"

        if ($null -ne $Request.Proxy) {
            $Request.Proxy.Credentials = [Net.CredentialCache]::DefaultNetworkCredentials
        }

        $Response = $Request.GetResponse()
        try {
            $Codigo = [int]$Response.StatusCode
            Write-Log "$Nome respondeu HTTP $Codigo. Destino: $($Response.ResponseUri.Host)" "OK"
            return $true
        }
        finally {
            $Response.Close()
        }
    }
    catch {
        $Status = ""
        if ($_.Exception -is [Net.WebException]) {
            $Status = " Status: $($_.Exception.Status)."
        }

        Write-Log "$Nome falhou.$Status $($_.Exception.Message)" "ERRO"
        return $false
    }
}

function Show-ProxyInfo {
    try {
        $Internet = Get-ItemProperty -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -ErrorAction Stop
        $Ativo = [int]$Internet.ProxyEnable
        $Servidor = [string]$Internet.ProxyServer

        if ($Ativo -eq 1) {
            Write-Log "Proxy do usuario esta habilitado. Servidor configurado: $Servidor" "AVISO"
        }
        else {
            Write-Log "Proxy manual do usuario esta desabilitado." "OK"
        }
    }
    catch {
        Write-Log "Nao foi possivel ler a configuracao de proxy do usuario." "AVISO"
    }

    try {
        $SaidaWinHttp = (& netsh winhttp show proxy 2>&1 | Out-String).Trim()
        Write-Log ("Proxy WinHTTP: " + ($SaidaWinHttp -replace "[\r\n]+", " | "))
    }
    catch {
        Write-Log "Nao foi possivel consultar o proxy WinHTTP." "AVISO"
    }
}

function Test-HostsFile {
    $HostsPath = Join-Path $env:WINDIR "System32\drivers\etc\hosts"

    try {
        $Bloqueios = @(Get-Content -LiteralPath $HostsPath -ErrorAction Stop | Where-Object {
            $_ -notmatch "^\s*#" -and $_ -match "github\.com|githubusercontent\.com"
        })

        if ($Bloqueios.Count -gt 0) {
            Write-Log "O arquivo hosts possui entrada para GitHub: $($Bloqueios -join ' | ')" "AVISO"
        }
        else {
            Write-Log "Nenhum bloqueio do GitHub foi encontrado no arquivo hosts." "OK"
        }
    }
    catch {
        Write-Log "Nao foi possivel verificar o arquivo hosts." "AVISO"
    }
}

function Test-AtualizacoesWin7 {
    $Os = Get-WmiObject Win32_OperatingSystem -ErrorAction SilentlyContinue

    if ($null -eq $Os -or $Os.Version -notmatch "^6\.1\.") {
        return
    }

    Write-Log "Windows 7 detectado. Service Pack: $($Os.ServicePackMajorVersion)."

    $Instaladas = @()
    try {
        $Instaladas = @(Get-HotFix -ErrorAction Stop | ForEach-Object { $_.HotFixID.ToUpperInvariant() })
    }
    catch {
        Write-Log "Nao foi possivel consultar todas as atualizacoes instaladas." "AVISO"
    }

    foreach ($Kb in @("KB3140245", "KB4474419", "KB4490628")) {
        if ($Instaladas -contains $Kb) {
            Write-Log "$Kb instalada." "OK"
        }
        else {
            Write-Log "$Kb nao foi localizada. Ela pode ser necessaria para TLS/SHA-2 no Windows 7." "AVISO"
        }
    }
}

function Enable-TlsSettings {
    Write-Log "Aplicando configuracoes de TLS 1.2 e criptografia forte..."

    foreach ($BaseDotNet in @(
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319",
        "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319",
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v2.0.50727",
        "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v2.0.50727"
    )) {
        Set-Dword $BaseDotNet "SchUseStrongCrypto" 1
        Set-Dword $BaseDotNet "SystemDefaultTlsVersions" 1
    }

    foreach ($BaseWinHttp in @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp",
        "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp"
    )) {
        Set-Dword $BaseWinHttp "DefaultSecureProtocols" 2560
    }

    $TlsClient = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client"
    Set-Dword $TlsClient "Enabled" 1
    Set-Dword $TlsClient "DisabledByDefault" 0

    Enable-DwordBits "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" "SecureProtocols" 2048

    try {
        $ProtocolosAtuais = [int][Net.ServicePointManager]::SecurityProtocol
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]($ProtocolosAtuais -bor 3072)
        Write-Log "TLS 1.2 habilitado no processo atual do PowerShell." "OK"
    }
    catch {
        Write-Log "Este .NET nao reconheceu TLS 1.2. Verifique as atualizacoes do Windows e o .NET Framework." "ERRO"
    }

    try {
        & ipconfig /flushdns | Out-Null
        Write-Log "Cache DNS limpo." "OK"
    }
    catch {
        Write-Log "Nao foi possivel limpar o cache DNS." "AVISO"
    }
}

try {
    [System.IO.File]::WriteAllText($LogFile, "", [System.Text.Encoding]::UTF8)
}
catch {
    Write-Host "Nao foi possivel criar o arquivo de log: $LogFile" -ForegroundColor Red
}

Write-Log "=============================================="
Write-Log "DIAGNOSTICO DE CONEXAO - GITHUB / TEK TOOLKIT"
Write-Log "=============================================="
Write-Log "Relatorio: $LogFile"
Write-Log "Windows: $([Environment]::OSVersion.VersionString)"
Write-Log "PowerShell: $($PSVersionTable.PSVersion)"
$Processo64 = ($env:PROCESSOR_ARCHITECTURE -eq "AMD64")
$Sistema64 = ($env:PROCESSOR_ARCHITECTURE -eq "AMD64" -or $env:PROCESSOR_ARCHITEW6432 -eq "AMD64")
Write-Log "Processo 64 bits: $Processo64"
Write-Log "Sistema 64 bits: $Sistema64"
Write-Log "Data local: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"

if (!(Test-Administrador) -and !$SomenteDiagnostico) {
    if (!$Elevado -and ![string]::IsNullOrEmpty($ScriptPath)) {
        Write-Log "Solicitando permissao de administrador para aplicar as correcoes..." "AVISO"

        try {
            $Argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Elevado"
            Start-Process -FilePath "$PSHOME\powershell.exe" -Verb RunAs -ArgumentList $Argumentos -ErrorAction Stop | Out-Null
            exit 0
        }
        catch {
            Write-Log "A elevacao foi cancelada ou falhou. $($_.Exception.Message)" "ERRO"
        }
    }

    Write-Log "Execute este arquivo como administrador para permitir as correcoes." "ERRO"
}

Write-Log "--- Diagnostico inicial ---"
Show-ProxyInfo
Test-HostsFile
Test-AtualizacoesWin7

$DnsRawAntes = Test-DnsHost "raw.githubusercontent.com"
$DnsGitHubAntes = Test-DnsHost "github.com"
$TcpRawAntes = Test-Porta443 "raw.githubusercontent.com"
$TcpGitHubAntes = Test-Porta443 "github.com"

try {
    $ProtocolosAtuais = [int][Net.ServicePointManager]::SecurityProtocol
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]($ProtocolosAtuais -bor 3072)
}
catch {
}

$RawAntes = Test-WebUrl "GitHub RAW antes da correcao" $RawUrl
$ApiAntes = Test-WebUrl "GitHub API antes da correcao" $ApiUrl
$ReleaseAntes = Test-WebUrl "GitHub Release antes da correcao" $ReleaseUrl

if ((Test-Administrador) -and !$SomenteDiagnostico) {
    Write-Log "--- Correcao automatica ---"
    Enable-TlsSettings
}

Write-Log "--- Teste depois da correcao ---"
$DnsRawDepois = Test-DnsHost "raw.githubusercontent.com"
$TcpRawDepois = Test-Porta443 "raw.githubusercontent.com"
$RawDepois = Test-WebUrl "GitHub RAW depois da correcao" $RawUrl
$ApiDepois = Test-WebUrl "GitHub API depois da correcao" $ApiUrl
$ReleaseDepois = Test-WebUrl "GitHub Release depois da correcao" $ReleaseUrl

Write-Log "--- Resultado ---"

if ($RawDepois) {
    if ($RawAntes) {
        Write-Log "CONEXAO OK: raw.githubusercontent.com esta acessivel." "OK"
    }
    else {
        Write-Log "CONEXAO CORRIGIDA: raw.githubusercontent.com voltou a responder." "OK"
    }
    Write-Log "Feche o instalador, abra novamente e repita a operacao." "OK"
}
elseif (!$DnsRawDepois) {
    Write-Log "CAUSA PROVAVEL: falha de DNS para raw.githubusercontent.com." "ERRO"
    Write-Log "Verifique o DNS da rede, filtro de conteudo ou bloqueio no roteador." "AVISO"
}
elseif (!$TcpRawDepois) {
    Write-Log "CAUSA PROVAVEL: firewall, antivirus ou proxy bloqueando raw.githubusercontent.com na porta 443." "ERRO"
}
elseif ($ApiDepois -or $ReleaseDepois) {
    Write-Log "CAUSA IDENTIFICADA: somente o dominio raw.githubusercontent.com esta bloqueado." "ERRO"
    Write-Log "A rede, proxy, antivirus ou filtro de conteudo precisa liberar *.githubusercontent.com." "AVISO"
}
else {
    Write-Log "CAUSA PROVAVEL: TLS/certificados, proxy ou bloqueio geral dos dominios do GitHub." "ERRO"
    Write-Log "Confirme as atualizacoes avisadas acima e reinicie o Windows se configuracoes foram alteradas." "AVISO"
}

if ($Alteracoes -gt 0) {
    Write-Log "$Alteracoes configuracao(oes) foram alteradas. Reinicie o computador antes de um novo teste se a conexao ainda falhar." "AVISO"
}
else {
    Write-Log "Nenhuma configuracao do Windows precisou ser alterada."
}

Write-Log "Envie o arquivo de relatorio ao suporte: $LogFile"
Write-Host ""
Write-Host "Pressione ENTER para fechar." -ForegroundColor Cyan
[void](Read-Host)

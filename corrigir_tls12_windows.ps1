param(
    [switch]$SomenteVerificar,
    [switch]$Elevado
)

$ErrorActionPreference = "Continue"
$ScriptPath = $MyInvocation.MyCommand.Path
$Desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)

if ([string]::IsNullOrEmpty($Desktop)) {
    $Desktop = [IO.Path]::GetTempPath()
}

$DataExecucao = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $Desktop ("Correcao_TLS12_" + $DataExecucao + ".log")
$BackupDir = Join-Path $Desktop ("Backup_TLS12_" + $DataExecucao)
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
        [IO.File]::AppendAllText($LogFile, $Linha + [Environment]::NewLine, [Text.Encoding]::UTF8)
    }
    catch {
    }
}

function Test-Admin {
    try {
        $Identidade = [Security.Principal.WindowsIdentity]::GetCurrent()
        $Principal = New-Object Security.Principal.WindowsPrincipal($Identidade)
        return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    catch {
        return $false
    }
}

function Set-DwordSeguro {
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
            Write-Log "$Caminho\$Nome = $Valor" "OK"
        }
        else {
            Write-Log "$Caminho\$Nome ja estava correto." "OK"
        }
    }
    catch {
        Write-Log "Falha ao configurar $Caminho\$Nome. $($_.Exception.Message)" "ERRO"
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
            Write-Log "$Caminho\$Nome recebeu o bit TLS 1.2. Valor: $Novo" "OK"
        }
        else {
            Write-Log "$Caminho\$Nome ja possui TLS 1.2." "OK"
        }
    }
    catch {
        Write-Log "Falha ao configurar $Caminho\$Nome. $($_.Exception.Message)" "ERRO"
    }
}

function Export-RegistryBackup {
    if (!(Test-Path -LiteralPath $BackupDir)) {
        New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    }

    $Chaves = @(
        @{ Nome = "schannel"; Caminho = "HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols" },
        @{ Nome = "dotnet"; Caminho = "HKLM\SOFTWARE\Microsoft\.NETFramework" },
        @{ Nome = "winhttp"; Caminho = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp" },
        @{ Nome = "internet_usuario"; Caminho = "HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings" }
    )

    foreach ($Chave in $Chaves) {
        try {
            $Destino = Join-Path $BackupDir ($Chave.Nome + ".reg")
            & reg.exe export $Chave.Caminho $Destino /y | Out-Null

            if ($LASTEXITCODE -eq 0) {
                Write-Log "Backup do Registro criado: $Destino" "OK"
            }
            else {
                Write-Log "A chave $($Chave.Caminho) nao existia ou nao foi exportada." "AVISO"
            }
        }
        catch {
            Write-Log "Falha ao exportar $($Chave.Caminho)." "AVISO"
        }
    }
}

function Test-Windows7Prerequisites {
    try {
        $Os = Get-WmiObject Win32_OperatingSystem -ErrorAction Stop
    }
    catch {
        return
    }

    Write-Log "Windows detectado: $($Os.Caption) - versao $($Os.Version) SP $($Os.ServicePackMajorVersion)."

    if ($Os.Version -notmatch "^6[.]1[.]") {
        return
    }

    if ([int]$Os.ServicePackMajorVersion -lt 1) {
        Write-Log "Windows 7 sem Service Pack 1. Instale o SP1 antes de continuar." "ERRO"
    }

    $HotFixes = @()
    try {
        $HotFixes = @(Get-HotFix -ErrorAction Stop | ForEach-Object { $_.HotFixID.ToUpperInvariant() })
    }
    catch {
        Write-Log "Nao foi possivel consultar todas as atualizacoes do Windows." "AVISO"
    }

    foreach ($Kb in @("KB3140245", "KB4474419", "KB4490628")) {
        if ($HotFixes -contains $Kb) {
            Write-Log "$Kb instalada." "OK"
        }
        else {
            Write-Log "$Kb nao foi localizada. Ela pode ser necessaria no Windows 7 para TLS 1.2/SHA-2." "AVISO"
        }
    }
}

function Test-Dns {
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

function Test-Https {
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
        $Request.UserAgent = "TEK-Toolkit-TLS12/1.0"

        if ($null -ne $Request.Proxy) {
            $Request.Proxy.Credentials = [Net.CredentialCache]::DefaultNetworkCredentials
        }

        $Response = $Request.GetResponse()
        try {
            Write-Log "$Nome respondeu HTTP $([int]$Response.StatusCode). Destino: $($Response.ResponseUri.Host)" "OK"
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

function Enable-Tls12 {
    $Sistema64 = ($env:PROCESSOR_ARCHITECTURE -eq "AMD64" -or $env:PROCESSOR_ARCHITEW6432 -eq "AMD64")

    $DotNetBases = @(
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v2.0.50727",
        "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319"
    )

    if ($Sistema64) {
        $DotNetBases += "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v2.0.50727"
        $DotNetBases += "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319"
    }

    foreach ($BaseDotNet in $DotNetBases) {
        Set-DwordSeguro $BaseDotNet "SchUseStrongCrypto" 1
        Set-DwordSeguro $BaseDotNet "SystemDefaultTlsVersions" 1
    }

    $WinHttpBases = @("HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp")
    if ($Sistema64) {
        $WinHttpBases += "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp"
    }

    foreach ($BaseWinHttp in $WinHttpBases) {
        Set-DwordSeguro $BaseWinHttp "DefaultSecureProtocols" 2560
    }

    $Tls12Client = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client"
    Set-DwordSeguro $Tls12Client "Enabled" 1
    Set-DwordSeguro $Tls12Client "DisabledByDefault" 0

    Enable-DwordBits "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" "SecureProtocols" 2048

    try {
        $Protocolos = [int][Net.ServicePointManager]::SecurityProtocol
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]($Protocolos -bor 3072)
        Write-Log "TLS 1.2 habilitado no processo atual do PowerShell." "OK"
    }
    catch {
        Write-Log "O .NET atual nao reconheceu TLS 1.2. Atualize o .NET Framework e o Windows." "ERRO"
    }

    try {
        & ipconfig.exe /flushdns | Out-Null
        Write-Log "Cache DNS limpo." "OK"
    }
    catch {
        Write-Log "Nao foi possivel limpar o cache DNS." "AVISO"
    }
}

try {
    [IO.File]::WriteAllText($LogFile, "", [Text.Encoding]::UTF8)
}
catch {
}

Write-Log "====================================="
Write-Log "CORRECAO TLS 1.2 - TEK TOOLKIT"
Write-Log "====================================="
Write-Log "Relatorio: $LogFile"
Write-Log "PowerShell: $($PSVersionTable.PSVersion)"
Write-Log "Data do computador: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
Test-Windows7Prerequisites

if (!$SomenteVerificar -and !(Test-Admin)) {
    if (!$Elevado -and ![string]::IsNullOrEmpty($ScriptPath)) {
        Write-Log "Solicitando permissao de administrador..." "AVISO"
        try {
            $Argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Elevado"
            Start-Process -FilePath "$PSHOME\powershell.exe" -Verb RunAs -ArgumentList $Argumentos -ErrorAction Stop | Out-Null
            exit 0
        }
        catch {
            Write-Log "A elevacao foi cancelada ou falhou. $($_.Exception.Message)" "ERRO"
            exit 1
        }
    }

    Write-Log "Execute este script como administrador." "ERRO"
    exit 1
}

if (!$SomenteVerificar) {
    Export-RegistryBackup
    Enable-Tls12
}
else {
    try {
        $Protocolos = [int][Net.ServicePointManager]::SecurityProtocol
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]($Protocolos -bor 3072)
    }
    catch {
    }
}

Write-Log "--- Teste de conexao ---"
$DnsRaw = Test-Dns "raw.githubusercontent.com"
$RawOk = Test-Https "GitHub RAW" "https://raw.githubusercontent.com/Nata-Felix/TEK-Toolkit/refs/heads/main/install.ps1"
$ReleaseOk = Test-Https "GitHub Release" "https://github.com/Nata-Felix/TEK-Toolkit/releases/download/v1.0/TekFarmaInstaller.exe"

if ($RawOk -and $ReleaseOk) {
    Write-Log "TLS 1.2 e conexao com GitHub estao funcionando." "OK"
    $CodigoSaida = 0
}
elseif (!$DnsRaw) {
    Write-Log "A falha restante e de DNS, nao de TLS. Verifique DNS, roteador ou filtro de rede." "ERRO"
    $CodigoSaida = 1
}
else {
    Write-Log "TLS 1.2 foi configurado, mas a conexao continua bloqueada por certificado, proxy, antivirus ou firewall." "ERRO"
    $CodigoSaida = 1
}

if ($Alteracoes -gt 0) {
    Write-Log "$Alteracoes configuracao(oes) foram alteradas." "OK"
    Write-Log "Se a conexao ainda falhar, reinicie o computador e execute novamente." "AVISO"
    Write-Log "Backup das configuracoes anteriores: $BackupDir" "INFO"
}
else {
    Write-Log "Nenhuma configuracao precisou ser alterada."
}

Write-Log "Relatorio final: $LogFile"

if (!$SomenteVerificar) {
    Write-Host ""
    Write-Host "Pressione ENTER para fechar." -ForegroundColor Cyan
    [void](Read-Host)
}

exit $CodigoSaida

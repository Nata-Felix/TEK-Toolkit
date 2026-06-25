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
}

function ObterLetraLivre {
    foreach ($Codigo in ([int][char]'Z')..([int][char]'D')) {
        $Letra = [char]$Codigo
        if (!(Get-PSDrive -Name $Letra -ErrorAction SilentlyContinue)) {
            return "$Letra`:"
        }
    }

    return ""
}

function CriarAtalhoTekFarmaMapeado {
    param([string]$Drive)

    $Alvo = Join-Path "$Drive\" "TekFarma\TekAplicacao.exe"
    $Desktop = [Environment]::GetFolderPath("Desktop")
    $Atalho = Join-Path $Desktop "TekFarma.lnk"

    if (!(Test-Path $Alvo)) {
        LogMsg "AVISO: Executavel TekFarma nao encontrado para atalho: $Alvo"
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

function MapearTekSoftware {
    param([string]$HostInformado)

    $HostNormalizado = $HostInformado.Trim()
    if ([string]::IsNullOrWhiteSpace($HostNormalizado)) {
        $HostNormalizado = "SERVIDOR"
    }

    RemoverMapeamentosTekSoftware

    $LetraLivre = ObterLetraLivre
    if ([string]::IsNullOrWhiteSpace($LetraLivre)) {
        LogMsg "AVISO: Nenhuma letra livre encontrada entre Z: e D:."
        return
    }

    $CaminhoRede = "\\$HostNormalizado\TekSoftware"
    LogMsg "Mapeando $CaminhoRede em $LetraLivre"
    & net.exe use $LetraLivre $CaminhoRede /persistent:yes | Out-Null
    LogMsg "Mapeamento criado: $LetraLivre -> $CaminhoRede"

    CriarAtalhoTekFarmaMapeado -Drive $LetraLivre
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

if (!(Test-Admin)) {
    LogMsg "ERRO: Este script precisa rodar como Administrador."
    exit 1
}

$ListaAcoes = @($Acoes.Split(",") | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })

foreach ($Acao in $ListaAcoes) {
    switch ($Acao) {
        "rede" {
            ExecutarPasso "Configurar rede avancada" {
                ConfigurarRedeAvancada
            }
        }
        "credencial" {
            ExecutarPasso "Criar credencial SERVIDOR" {
                CriarCredencialServidor
            }
        }
        "certificados" {
            ExecutarPasso "Instalar cadeia de certificado" {
                InstalarCadeiaCertificado
            }
        }
        "mapear" {
            ExecutarPasso "Mapear TekSoftware" {
                MapearTekSoftware -HostInformado $HostServidor
            }
        }
        "firewall" {
            ExecutarPasso "Adicionar excecao no firewall" {
                AdicionarExcecoesFirewall
            }
        }
        "firebird" {
            ExecutarPasso "Reinstalar Firebird" {
                ReinstalarFirebird
            }
        }
        "net35" {
            ExecutarPasso "Instalar .NET Framework 3.5" {
                InstalarNet35
            }
        }
        "portacom" {
            ExecutarPasso "Resetar portas COM" {
                ResetarPortasCom
            }
        }
        "removersenhacompartilhamento" {
            ExecutarPasso "Remover senha de compartilhamento" {
                RemoverSenhaCompartilhamento
            }
        }
        "windowsupdatefix" {
            ExecutarPasso "Corrigir Windows Update" {
                CorrigirWindowsUpdate
            }
        }
        "cacheicone" {
            ExecutarPasso "Aumentar cache de icones" {
                AumentarCacheIcone
            }
        }
        "firewalloff" {
            ExecutarPasso "Desativar Firewall do Windows" {
                DesativarFirewallWindows
            }
        }
        "resetimpressora" {
            ExecutarPasso "Resetar impressora" {
                ResetarImpressora
            }
        }
        "gpedit" {
            ExecutarPasso "Instalar GPEDIT.MSC" {
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

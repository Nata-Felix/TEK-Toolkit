param(
    [string]$TipoVersao = "",
    [string]$PerfilTek = ""
)

$ErrorActionPreference = "Stop"

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path

$Log = Join-Path $Base "tekfarma_instalacao_log.txt"
$CrystalLog = Join-Path $Base "crystal_install.log"
$CrystalUninstallLog = Join-Path $Base "crystal_uninstall.log"

$Net48 = Join-Path $Base "dotnet48.exe"
$VCx86 = Join-Path $Base "VC_redist.x86.exe"
$VCx64 = Join-Path $Base "VC_redist.x64.exe"
$CrystalMsi = Join-Path $Base "CRRuntime_32bit_13_0_39.msi"
$FixZip = Join-Path $Base "crdb_adoplus.zip"

$FirebirdExe = Join-Path $Base "Firebird-2.5.9.exe"
$TekFarmaPastaZip = Join-Path $Base "TekFarmaPasta.zip"
$PastasTekFarmaZip = Join-Path $Base "pastastekfarma.zip"
$DllsZip = Join-Path $Base "DLLS.zip"
$BancoTekFarmaZip = Join-Path $Base "TEKFARMA(NOV-2020).zip"

$RaizTekSoftware = "C:\TekSoftware"
$DestinoSistema = Join-Path $RaizTekSoftware "TekFarma"
$DestinoCrystal = "C:\Program Files (x86)\SAP BusinessObjects\Crystal Reports for .NET Framework 4.0\Common\SAP BusinessObjects Enterprise XI 4.0\win32_x86"
$DestinoFirebird = "C:\Program Files\Firebird\Firebird_2_5"
$TempFix = "C:\Windows\Temp\crdb_adoplus_fix"
$ServidorShare = "\\SERVIDOR\TekSoftware"

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

function ObterContaPorSid {
    param([string]$Sid)

    try {
        return (New-Object Security.Principal.SecurityIdentifier($Sid)).Translate([Security.Principal.NTAccount]).Value
    }
    catch {
        return $Sid
    }
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

        if ([string]::IsNullOrWhiteSpace($Argumentos)) {
            $Processo = Start-Process -FilePath $Caminho -Wait -PassThru
        }
        else {
            $Processo = Start-Process -FilePath $Caminho -ArgumentList $Argumentos -Wait -PassThru
        }

        LogMsg "$Nome finalizado. ExitCode: $($Processo.ExitCode)"
        return ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 3010)
    }
    catch {
        LogMsg "ERRO ao executar ${Nome}: $($_.Exception.Message)"
        return $false
    }
}

function ObterProcessosTek {
    $ProcessosProtegidos = @("TekFarmaInstaller")

    @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -like "Tek*" -and
        $ProcessosProtegidos -notcontains $_.ProcessName
    })
}

function FinalizarProcessosTek {
    $ProcessosTek = @(ObterProcessosTek)

    if ($ProcessosTek.Count -eq 0) {
        LogMsg "Nenhum processo Tek encontrado."
        return $true
    }

    foreach ($Proc in $ProcessosTek) {
        try {
            LogMsg "Finalizando: $($Proc.ProcessName).exe PID $($Proc.Id)"
            Stop-Process -Id $Proc.Id -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel finalizar $($Proc.ProcessName). Motivo: $($_.Exception.Message)"
        }
    }

    Start-Sleep -Seconds 1

    foreach ($Proc in @(ObterProcessosTek)) {
        try {
            LogMsg "Forcando encerramento via taskkill: $($Proc.ProcessName).exe PID $($Proc.Id)"
            Start-Process -FilePath "taskkill.exe" -ArgumentList @("/PID", $Proc.Id, "/F", "/T") -Wait -NoNewWindow | Out-Null
        }
        catch {
            LogMsg "AVISO: taskkill falhou para $($Proc.ProcessName). Motivo: $($_.Exception.Message)"
        }
    }

    $Limite = (Get-Date).AddSeconds(10)

    do {
        $Restantes = @(ObterProcessosTek)

        if ($Restantes.Count -eq 0) {
            LogMsg "Todos os processos Tek* foram finalizados."
            return $true
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $Limite)

    foreach ($Proc in $Restantes) {
        LogMsg "AVISO: Processo Tek* ainda ativo: $($Proc.ProcessName).exe PID $($Proc.Id)"
    }

    return $false
}

function FecharArquivosSmbTekSoftware {
    if (!(Get-Command Get-SmbOpenFile -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: Get-SmbOpenFile nao disponivel."
        return
    }

    $ArquivosAbertos = @(Get-SmbOpenFile -ErrorAction SilentlyContinue | Where-Object {
        Test-CaminhoDentroOuIgual -BasePath $RaizTekSoftware -Path $_.Path
    })

    if ($ArquivosAbertos.Count -eq 0) {
        LogMsg "Nenhum arquivo SMB aberto em $RaizTekSoftware."
        return
    }

    foreach ($Arquivo in $ArquivosAbertos) {
        try {
            LogMsg "Fechando arquivo SMB aberto: $($Arquivo.Path) FileId $($Arquivo.FileId)"
            Close-SmbOpenFile -FileId $Arquivo.FileId -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel fechar SMB $($Arquivo.Path). Motivo: $($_.Exception.Message)"
        }
    }
}

function SuspenderCompartilhamentosTekSoftware {
    if (!(Get-Command Get-SmbShare -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: Modulo SMB nao disponivel."
        return @()
    }

    $Compartilhamentos = @(Get-SmbShare -ErrorAction SilentlyContinue | Where-Object {
        -not $_.Special -and (Test-CaminhoDentroOuIgual -BasePath $RaizTekSoftware -Path $_.Path)
    })

    if ($Compartilhamentos.Count -eq 0) {
        LogMsg "Nenhum compartilhamento encontrado em $RaizTekSoftware."
        return @()
    }

    FecharArquivosSmbTekSoftware

    $Snapshots = @()

    foreach ($Share in $Compartilhamentos) {
        $Acessos = @()

        try {
            $Acessos = @(Get-SmbShareAccess -Name $Share.Name -ErrorAction Stop | Select-Object AccountName, AccessControlType, AccessRight)
        }
        catch {
            LogMsg "AVISO: Nao foi possivel ler permissoes do compartilhamento $($Share.Name)."
        }

        $Snapshot = [pscustomobject]@{
            Name = $Share.Name
            Path = $Share.Path
            Description = $Share.Description
            CachingMode = $Share.CachingMode
            FolderEnumerationMode = $Share.FolderEnumerationMode
            Access = $Acessos
        }

        try {
            LogMsg "Removendo compartilhamento temporariamente: $($Share.Name) -> $($Share.Path)"
            Remove-SmbShare -Name $Share.Name -Force -ErrorAction Stop
            $Snapshots += $Snapshot
        }
        catch {
            LogMsg "AVISO: Nao foi possivel remover compartilhamento $($Share.Name). Motivo: $($_.Exception.Message)"
        }
    }

    return $Snapshots
}

function RestaurarPermissaoCompartilhamento {
    param(
        [string]$NomeCompartilhamento,
        [string]$Conta,
        [string]$TipoControle,
        [string]$Direito
    )

    try {
        if ($TipoControle -eq "Deny") {
            Block-SmbShareAccess -Name $NomeCompartilhamento -AccountName $Conta -Force -ErrorAction Stop | Out-Null
        }
        else {
            Grant-SmbShareAccess -Name $NomeCompartilhamento -AccountName $Conta -AccessRight $Direito -Force -ErrorAction Stop | Out-Null
        }
    }
    catch {
        LogMsg "AVISO: Nao foi possivel aplicar permissao $Direito para $Conta em $NomeCompartilhamento."
    }
}

function GarantirPermissoesPadraoCompartilhamento {
    param([string]$NomeCompartilhamento)

    $ContasPadrao = @(
        ObterContaPorSid "S-1-1-0",
        ObterContaPorSid "S-1-5-2",
        ObterContaPorSid "S-1-5-32-546"
    )

    foreach ($Conta in $ContasPadrao) {
        RestaurarPermissaoCompartilhamento -NomeCompartilhamento $NomeCompartilhamento -Conta $Conta -TipoControle "Allow" -Direito "Full"
    }
}

function RestaurarCompartilhamentosTekSoftware {
    param([array]$Compartilhamentos)

    if (!$Compartilhamentos -or $Compartilhamentos.Count -eq 0) {
        return
    }

    if (!(Get-Command New-SmbShare -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: New-SmbShare nao disponivel. Compartilhamentos nao foram restaurados automaticamente."
        return
    }

    foreach ($Share in $Compartilhamentos) {
        try {
            if (!(Test-Path $Share.Path)) {
                New-Item -ItemType Directory -Path $Share.Path -Force | Out-Null
            }

            $Parametros = @{
                Name = $Share.Name
                Path = $Share.Path
            }

            if (![string]::IsNullOrWhiteSpace($Share.Description)) {
                $Parametros.Description = $Share.Description
            }

            if ($Share.CachingMode) {
                $Parametros.CachingMode = $Share.CachingMode
            }

            if ($Share.FolderEnumerationMode) {
                $Parametros.FolderEnumerationMode = $Share.FolderEnumerationMode
            }

            LogMsg "Restaurando compartilhamento: $($Share.Name) -> $($Share.Path)"
            New-SmbShare @Parametros | Out-Null

            foreach ($AcessoAtual in @(Get-SmbShareAccess -Name $Share.Name -ErrorAction SilentlyContinue)) {
                try {
                    Revoke-SmbShareAccess -Name $Share.Name -AccountName $AcessoAtual.AccountName -Force -ErrorAction Stop | Out-Null
                }
                catch {
                    LogMsg "AVISO: Nao foi possivel remover permissao temporaria de $($AcessoAtual.AccountName)."
                }
            }

            foreach ($Acesso in $Share.Access) {
                RestaurarPermissaoCompartilhamento -NomeCompartilhamento $Share.Name -Conta $Acesso.AccountName -TipoControle $Acesso.AccessControlType -Direito $Acesso.AccessRight
            }

            GarantirPermissoesPadraoCompartilhamento -NomeCompartilhamento $Share.Name
        }
        catch {
            LogMsg "AVISO: Falha ao restaurar compartilhamento $($Share.Name): $($_.Exception.Message)"
        }
    }
}

function ExtrairZip {
    param(
        [string]$Zip,
        [string]$Destino,
        [string]$Nome
    )

    if (!(Test-Path $Zip)) {
        LogMsg "AVISO: ZIP nao encontrado: $Zip"
        return $false
    }

    if (!(Test-Path $Destino)) {
        New-Item -ItemType Directory -Path $Destino -Force | Out-Null
    }

    LogMsg "Extraindo $Nome para $Destino"
    Unblock-File $Zip -ErrorAction SilentlyContinue
    Expand-Archive -LiteralPath $Zip -DestinationPath $Destino -Force
    return $true
}

function AtualizarVersaoTekFarmaServidor {
    if ($TipoVersao -eq "normal") {
        $Pacote = Join-Path $Base "TekFarma50.exe"
    }
    elseif ($TipoVersao -eq "i") {
        $Pacote = Join-Path $Base "TekFarma50i.exe"
    }
    else {
        LogMsg "AVISO: Tipo de versao invalido: $TipoVersao"
        return $false
    }

    if (!(Test-Path $Pacote)) {
        LogMsg "AVISO: Pacote da versao nao encontrado: $Pacote"
        return $false
    }

    if (!(Test-Path $DestinoSistema)) {
        New-Item -ItemType Directory -Path $DestinoSistema -Force | Out-Null
    }

    Unblock-File $Pacote -ErrorAction SilentlyContinue

    LogMsg "Extraindo versao TekFarma com tar para $DestinoSistema"

    $ProcTar = Start-Process -FilePath "tar.exe" -ArgumentList @("-xf", $Pacote, "-C", $DestinoSistema) -Wait -PassThru
    LogMsg "Extracao da versao finalizada. ExitCode: $($ProcTar.ExitCode)"

    return ($ProcTar.ExitCode -eq 0)
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
            LogMsg "Removendo servico Firebird restante: $($Servico.Name)"
            & sc.exe delete $Servico.Name | Out-Null
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

function InstalarFirebird {
    $Argumentos = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$DestinoFirebird`""
    InstalarExe -Caminho $FirebirdExe -Argumentos $Argumentos -Nome "Firebird 2.5.9" | Out-Null

    if (Test-Path $DestinoFirebird) {
        LogMsg "Pasta Firebird encontrada: $DestinoFirebird"
    }
    else {
        LogMsg "AVISO: Pasta Firebird nao encontrada apos instalacao: $DestinoFirebird"
    }

    $ServicosFirebird = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Firebird*" -or $_.DisplayName -like "*Firebird*" })

    foreach ($Servico in $ServicosFirebird) {
        try {
            if ($Servico.Status -ne "Running") {
                Start-Service -Name $Servico.Name -ErrorAction Stop
            }

            LogMsg "Servico Firebird OK: $($Servico.Name) - $((Get-Service -Name $Servico.Name).Status)"
        }
        catch {
            LogMsg "AVISO: Nao foi possivel iniciar servico Firebird $($Servico.Name): $($_.Exception.Message)"
        }
    }
}

function RemoverCrystalAntigo {
    $CrystalInstalados = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.DisplayName -like "*SAP Crystal Reports runtime engine for .NET Framework*") -or
            ($_.DisplayName -like "*Crystal Reports runtime engine*") -or
            ($_.DisplayName -like "*SAP Crystal*") -or
            ($_.DisplayName -like "*Crystal*Reports*")
        }

    if (!$CrystalInstalados) {
        LogMsg "Nenhuma instalacao do Crystal encontrada."
    }

    foreach ($Crystal in $CrystalInstalados) {
        LogMsg "Crystal encontrado: $($Crystal.DisplayName) - $($Crystal.DisplayVersion)"

        if ($Crystal.PSChildName -match "^\{.*\}$") {
            $Guid = $Crystal.PSChildName
            $ArgsUninstall = "/x $Guid /qn /norestart /L*v `"$CrystalUninstallLog`""

            try {
                $ProcUninstall = Start-Process -FilePath "msiexec.exe" -ArgumentList $ArgsUninstall -Wait -PassThru
                LogMsg "Desinstalacao Crystal ExitCode: $($ProcUninstall.ExitCode)"
            }
            catch {
                LogMsg "AVISO: Falha ao desinstalar Crystal: $($_.Exception.Message)"
            }
        }
    }

    $CrystalRestantes = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.DisplayName -like "*SAP Crystal Reports runtime engine for .NET Framework*") -or
            ($_.DisplayName -like "*Crystal Reports runtime engine*") -or
            ($_.DisplayName -like "*SAP Crystal*") -or
            ($_.DisplayName -like "*Crystal*Reports*")
        }

    foreach ($Item in $CrystalRestantes) {
        try {
            LogMsg "Removendo chave orfa do Crystal: $($Item.DisplayName)"
            Remove-Item -LiteralPath $Item.PSPath -Recurse -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel remover chave orfa: $($_.Exception.Message)"
        }
    }

    foreach ($Pasta in @("C:\Program Files (x86)\SAP BusinessObjects", "C:\Program Files\SAP BusinessObjects")) {
        if (Test-Path $Pasta) {
            try {
                LogMsg "Apagando pasta Crystal antiga: $Pasta"
                Remove-Item -LiteralPath $Pasta -Recurse -Force -ErrorAction Stop
            }
            catch {
                LogMsg "AVISO: Nao foi possivel apagar ${Pasta}: $($_.Exception.Message)"
            }
        }
    }
}

function InstalarCrystalNovo {
    if (!(Test-Path $CrystalMsi)) {
        LogMsg "AVISO: MSI Crystal nao encontrado: $CrystalMsi"
        return $false
    }

    Unblock-File $CrystalMsi -ErrorAction SilentlyContinue
    $ArgsCrystal = '/i "' + $CrystalMsi + '" /qn /norestart /L*v "' + $CrystalLog + '"'

    try {
        $Processo = Start-Process -FilePath "msiexec.exe" -ArgumentList $ArgsCrystal -Wait -PassThru
        LogMsg "Crystal Reports Runtime finalizado. ExitCode: $($Processo.ExitCode)"
        return ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 3010)
    }
    catch {
        LogMsg "AVISO: Falha ao instalar Crystal: $($_.Exception.Message)"
        return $false
    }
}

function AplicarFixCrystal {
    if (!(Test-Path $FixZip)) {
        LogMsg "AVISO: ZIP do fix Crystal nao encontrado: $FixZip"
        return $false
    }

    if (!(Test-Path $DestinoCrystal)) {
        LogMsg "AVISO: Pasta destino do Crystal nao encontrada: $DestinoCrystal"
        return $false
    }

    if (Test-Path $TempFix) {
        Remove-Item -LiteralPath $TempFix -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $TempFix -Force | Out-Null

    try {
        Unblock-File $FixZip -ErrorAction SilentlyContinue
        Expand-Archive -LiteralPath $FixZip -DestinationPath $TempFix -Force
        Copy-Item -Path (Join-Path $TempFix "*") -Destination $DestinoCrystal -Recurse -Force -ErrorAction Stop
        LogMsg "Fix crdb_adoplus aplicado com sucesso."
        return $true
    }
    catch {
        LogMsg "AVISO: Falha ao aplicar fix Crystal: $($_.Exception.Message)"
        return $false
    }
    finally {
        if (Test-Path $TempFix) {
            Remove-Item -LiteralPath $TempFix -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function InstalarDependenciasFull {
    ExecutarPasso ".NET Framework 4.8" {
        InstalarExe $Net48 "/q /norestart" ".NET Framework 4.8 Offline" | Out-Null
    }

    ExecutarPasso "Visual C++ Redistributable x86" {
        InstalarExe $VCx86 "/install /quiet /norestart" "Visual C++ Redistributable x86" | Out-Null
    }

    ExecutarPasso "Visual C++ Redistributable x64" {
        InstalarExe $VCx64 "/install /quiet /norestart" "Visual C++ Redistributable x64" | Out-Null
    }

    ExecutarPasso "Remover Crystal antigo" {
        RemoverCrystalAntigo
    }

    ExecutarPasso "Instalar Crystal novo" {
        InstalarCrystalNovo | Out-Null
    }

    ExecutarPasso "Aplicar fix Crystal" {
        AplicarFixCrystal | Out-Null
    }
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

function GarantirCompartilhamentoTekSoftware {
    if (!(Test-Path $RaizTekSoftware)) {
        New-Item -ItemType Directory -Path $RaizTekSoftware -Force | Out-Null
    }

    if (!(Get-Command Get-SmbShare -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: Modulo SMB nao disponivel. Compartilhamento TekSoftware nao criado."
        return
    }

    $Share = Get-SmbShare -Name "TekSoftware" -ErrorAction SilentlyContinue

    if ($null -eq $Share) {
        New-SmbShare -Name "TekSoftware" -Path $RaizTekSoftware -Description "TekSoftware" | Out-Null
        LogMsg "Compartilhamento TekSoftware criado."
    }
    else {
        LogMsg "Compartilhamento TekSoftware ja existe."
    }

    GarantirPermissoesPadraoCompartilhamento -NomeCompartilhamento "TekSoftware"

    foreach ($Sid in @("S-1-1-0", "S-1-5-2", "S-1-5-32-546")) {
        & icacls.exe $RaizTekSoftware /grant "*$Sid`:(OI)(CI)F" /T /C | Out-Null
    }

    LogMsg "Permissoes do compartilhamento TekSoftware reforcadas."
}

function EncontrarBat {
    param([string]$Nome)

    $Raizes = @($Base, $RaizTekSoftware, $DestinoSistema) | Where-Object { Test-Path $_ } | Select-Object -Unique
    $Encontrados = @()

    foreach ($Raiz in $Raizes) {
        try {
            $Encontrados += @(Get-ChildItem -LiteralPath $Raiz -Filter $Nome -File -Recurse -ErrorAction SilentlyContinue)
        }
        catch {
            LogMsg "AVISO: Falha ao procurar $Nome em $Raiz."
        }
    }

    $Encontrados | Select-Object -ExpandProperty FullName -Unique
}

function ExecutarBatsTekFarma {
    $Executados = 0

    foreach ($NomeBat in @("00.Permitir Aplicativo.bat", "00.TekOnline.bat")) {
        $Bats = @(EncontrarBat -Nome $NomeBat)

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
                $Executados++
            }
            catch {
                LogMsg "AVISO: Falha ao executar ${Bat}: $($_.Exception.Message)"
            }
        }
    }

    return $Executados
}

function MapearServidorTerminal {
    LogMsg "Testando acesso a $ServidorShare"

    if (!(Test-Path $ServidorShare)) {
        LogMsg "AVISO: Share $ServidorShare nao acessivel neste momento."
    }

    try {
        & net.exe use $ServidorShare /persistent:yes | Out-Null
        LogMsg "Conexao persistente criada para $ServidorShare."
    }
    catch {
        LogMsg "AVISO: net use falhou para ${ServidorShare}: $($_.Exception.Message)"
    }

    if (!(Test-Path $RaizTekSoftware)) {
        try {
            $Comando = "mklink /D `"$RaizTekSoftware`" `"$ServidorShare`""
            cmd.exe /c $Comando | Out-Null
            LogMsg "Link criado: $RaizTekSoftware -> $ServidorShare"
        }
        catch {
            LogMsg "AVISO: Nao foi possivel criar link $RaizTekSoftware -> ${ServidorShare}: $($_.Exception.Message)"
        }
    }
    else {
        $Item = Get-Item -LiteralPath $RaizTekSoftware -ErrorAction SilentlyContinue

        if ($Item -and (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            LogMsg "$RaizTekSoftware ja existe como link/reparse point."
        }
        else {
            LogMsg "AVISO: $RaizTekSoftware ja existe. Nao foi substituido pelo link de rede."
        }
    }
}

function GarantirCredencialServidorTerminal {
    $HostCredencial = "SERVIDOR"
    $UsuarioCredencial = "convidado"

    try {
        LogMsg "Configurando credencial do Windows para ${HostCredencial} com usuario ${UsuarioCredencial}."
        & cmdkey.exe /delete:$HostCredencial 2>&1 | Out-Null
        & cmd.exe /c "echo.|cmdkey.exe /add:$HostCredencial /user:$UsuarioCredencial" 2>&1 | Out-Null
        LogMsg "Credencial do Windows configurada: host=$HostCredencial usuario=$UsuarioCredencial senha=vazia"
    }
    catch {
        LogMsg "AVISO: Nao foi possivel configurar credencial do Windows para ${HostCredencial}: $($_.Exception.Message)"
    }
}

function CriarAtalhoTekFarma {
    $Alvo = Join-Path $DestinoSistema "TekAplicacao.exe"
    $Desktop = [Environment]::GetFolderPath("Desktop")
    $Atalho = Join-Path $Desktop "TekFarma.lnk"

    if (!(Test-Path $Alvo)) {
        LogMsg "AVISO: Executavel TekFarma nao encontrado para atalho: $Alvo"
        return
    }

    $Shell = New-Object -ComObject WScript.Shell
    $Shortcut = $Shell.CreateShortcut($Atalho)
    $Shortcut.TargetPath = $Alvo
    $Shortcut.WorkingDirectory = $DestinoSistema
    $Shortcut.IconLocation = "$Alvo,0"
    $Shortcut.Save()

    LogMsg "Atalho criado na area de trabalho: $Atalho"
}

function RenomearServidor {
    if ($env:COMPUTERNAME -ieq "SERVIDOR") {
        LogMsg "Computador ja se chama SERVIDOR."
        $script:ReinicioNecessario = $false
        return
    }

    Rename-Computer -NewName "SERVIDOR" -Force -ErrorAction Stop
    $script:ReinicioNecessario = $true
    LogMsg "Renomeacao para SERVIDOR solicitada. Sera aplicada apos reiniciar."
}

function AgendarReinicio {
    LogMsg "Agendando reinicio em 30 segundos..."
    shutdown.exe /r /t 30 /c "Instalacao TekFarma servidor finalizada. Reinicio necessario." | Out-Null
}

function FluxoServidor {
    $script:ReinicioNecessario = $false

    ExecutarPasso "Remover Firebird existente" {
        RemoverFirebirdExistente
    }

    ExecutarPasso "Instalar Firebird 2.5.9" {
        InstalarFirebird
    }

    ExecutarPasso "Finalizar processos Tek" {
        FinalizarProcessosTek | Out-Null
    }

    $CompartilhamentosSuspensos = @()

    ExecutarPasso "Suspender compartilhamentos TekSoftware" {
        $script:CompartilhamentosSuspensos = @(SuspenderCompartilhamentosTekSoftware)
    }

    try {
        ExecutarPasso "Criar pasta C:\TekSoftware" {
            New-Item -ItemType Directory -Path $RaizTekSoftware -Force | Out-Null
            New-Item -ItemType Directory -Path $DestinoSistema -Force | Out-Null
        }

        ExecutarPasso "Extrair TekFarmaPasta.zip" {
            ExtrairZip -Zip $TekFarmaPastaZip -Destino $RaizTekSoftware -Nome "TekFarmaPasta.zip" | Out-Null
        }

        ExecutarPasso "Extrair pastastekfarma.zip" {
            ExtrairZip -Zip $PastasTekFarmaZip -Destino $DestinoSistema -Nome "pastastekfarma.zip" | Out-Null
        }

        ExecutarPasso "Extrair DLLS.zip" {
            ExtrairZip -Zip $DllsZip -Destino $DestinoSistema -Nome "DLLS.zip" | Out-Null
        }

        ExecutarPasso "Atualizar versao TekFarma" {
            AtualizarVersaoTekFarmaServidor | Out-Null
        }
    }
    finally {
        ExecutarPasso "Restaurar compartilhamentos TekSoftware" {
            RestaurarCompartilhamentosTekSoftware -Compartilhamentos $script:CompartilhamentosSuspensos
        }
    }

    InstalarDependenciasFull

    ExecutarPasso "Executar scripts auxiliares TekFarma" {
        ExecutarBatsTekFarma | Out-Null
    }

    ExecutarPasso "Configurar rede avancada" {
        ConfigurarRedeAvancada
    }

    ExecutarPasso "Compartilhar C:\TekSoftware" {
        GarantirCompartilhamentoTekSoftware
    }

    ExecutarPasso "Extrair banco TEKFARMA(NOV-2020).zip" {
        ExtrairZip -Zip $BancoTekFarmaZip -Destino $DestinoSistema -Nome "TEKFARMA(NOV-2020).zip" | Out-Null
    }

    ExecutarPasso "Renomear computador para SERVIDOR" {
        RenomearServidor
    }

    ExecutarPasso "Reiniciar computador" {
        if ($script:ReinicioNecessario) {
            AgendarReinicio
        }
        else {
            LogMsg "Reinicio automatico ignorado porque o computador ja se chama SERVIDOR ou nao foi renomeado."
        }
    }
}

function FluxoTerminal {
    InstalarDependenciasFull

    $script:BatsExecutados = 0

    ExecutarPasso "Executar scripts auxiliares TekFarma" {
        $script:BatsExecutados = ExecutarBatsTekFarma
    }

    ExecutarPasso "Configurar rede avancada" {
        ConfigurarRedeAvancada
    }

    ExecutarPasso "Configurar credencial do Windows para SERVIDOR" {
        GarantirCredencialServidorTerminal
    }

    ExecutarPasso "Mapear servidor TekSoftware" {
        MapearServidorTerminal
    }

    if ($script:BatsExecutados -eq 0) {
        ExecutarPasso "Executar scripts auxiliares TekFarma apos mapeamento" {
            ExecutarBatsTekFarma | Out-Null
        }
    }

    ExecutarPasso "Criar atalho TekFarma" {
        CriarAtalhoTekFarma
    }
}

Clear-Host

LogMsg "====================================="
LogMsg "INSTALADOR TEKFARMA SERVIDOR / TERMINAL"
LogMsg "====================================="
LogMsg "PerfilTek recebido: $PerfilTek"
LogMsg "TipoVersao recebido: $TipoVersao"
LogMsg "Base: $Base"

if (!(Test-Admin)) {
    LogMsg "ERRO: Este script precisa rodar como Administrador."
    exit 1
}

if ($TipoVersao -notin @("normal", "i")) {
    LogMsg "ERRO: TipoVersao invalido: $TipoVersao"
    exit 1
}

if ($PerfilTek -notin @("servidor", "terminal")) {
    LogMsg "ERRO: PerfilTek invalido: $PerfilTek"
    exit 1
}

if ($PerfilTek -eq "servidor") {
    FluxoServidor
}
else {
    FluxoTerminal
}

LogMsg "====================================="
LogMsg "PROCESSO TEKFARMA FINALIZADO"
LogMsg "Log geral: $Log"
LogMsg "Log Crystal install: $CrystalLog"
LogMsg "Log Crystal uninstall: $CrystalUninstallLog"
LogMsg "====================================="

exit 0

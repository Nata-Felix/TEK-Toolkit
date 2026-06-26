param(
    [string]$Modo = "3",
    [string]$TipoVersao = ""
)

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path

$Log = Join-Path $Base "install_final_log.txt"
$CrystalLog = Join-Path $Base "crystal_install.log"
$CrystalUninstallLog = Join-Path $Base "crystal_uninstall.log"

$Net48 = Join-Path $Base "dotnet48.exe"
$VCx86 = Join-Path $Base "VC_redist.x86.exe"
$VCx64 = Join-Path $Base "VC_redist.x64.exe"
$CrystalMsi = Join-Path $Base "CRRuntime_32bit_13_0_39.msi"
$FixZip = Join-Path $Base "crdb_adoplus.zip"

$RaizTekSoftware = "C:\TekSoftware"
$DestinoSistema = Join-Path $RaizTekSoftware "TekFarma"
$DestinoCrystal = "C:\Program Files (x86)\SAP BusinessObjects\Crystal Reports for .NET Framework 4.0\Common\SAP BusinessObjects Enterprise XI 4.0\win32_x86"
$TempFix = "C:\Windows\Temp\crdb_adoplus_fix"

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

function InstalarExe {
    param(
        [string]$Caminho,
        [string]$Argumentos,
        [string]$Nome
    )

    LogMsg "====================================="
    LogMsg "Iniciando: $Nome"
    LogMsg "Arquivo: $Caminho"
    LogMsg "Argumentos: $Argumentos"

    if (!(Test-Path $Caminho)) {
        LogMsg "ERRO: Arquivo nao encontrado: $Caminho"
        return $false
    }

    Unblock-File $Caminho -ErrorAction SilentlyContinue

    if ([string]::IsNullOrWhiteSpace($Argumentos)) {
        $Processo = Start-Process -FilePath $Caminho -Wait -PassThru
    }
    else {
        $Processo = Start-Process -FilePath $Caminho -ArgumentList $Argumentos -Wait -PassThru
    }

    LogMsg "$Nome finalizado. ExitCode: $($Processo.ExitCode)"

    if ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 3010) {
        return $true
    }

    return $false
}

function ObterProcessosTek {
    $ProcessosProtegidos = @(
        "TekFarmaInstaller",
        "TekSoftwareSuporte"
    )

    @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -like "Tek*" -and
        $ProcessosProtegidos -notcontains $_.ProcessName
    })
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

function ObterContaPorSid {
    param([string]$Sid)

    try {
        return (New-Object Security.Principal.SecurityIdentifier($Sid)).Translate([Security.Principal.NTAccount]).Value
    }
    catch {
        return $Sid
    }
}

function ObterContasCompartilhamentoPorSid {
    param([string]$Sid)

    $Contas = @()
    $Traduzida = ObterContaPorSid $Sid

    if (![string]::IsNullOrWhiteSpace($Traduzida)) {
        $Contas += $Traduzida
    }

    switch ($Sid) {
        "S-1-1-0" {
            $Contas += @("Todos", "Everyone")
        }
        "S-1-5-2" {
            $Contas += @("Rede", "Network", "NT AUTHORITY\NETWORK")
        }
        "S-1-5-32-546" {
            $Contas += @("Convidados", "Guests", "BUILTIN\Guests")
        }
        default {
            $Contas += $Sid
        }
    }

    @($Contas | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function FecharArquivosSmbTekSoftware {
    if (!(Get-Command Get-SmbOpenFile -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: Get-SmbOpenFile nao disponivel. Nao foi possivel fechar arquivos abertos via SMB automaticamente."
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
            LogMsg "AVISO: Nao foi possivel fechar arquivo SMB $($Arquivo.Path). Motivo: $($_.Exception.Message)"
        }
    }
}

function SuspenderCompartilhamentosTekSoftware {
    LogMsg "====================================="
    LogMsg "Verificando compartilhamentos de $RaizTekSoftware..."

    if (!(Get-Command Get-SmbShare -ErrorAction SilentlyContinue)) {
        LogMsg "AVISO: Modulo SMB nao disponivel. Nao foi possivel suspender compartilhamentos automaticamente."
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
            LogMsg "AVISO: Nao foi possivel ler permissoes do compartilhamento $($Share.Name). Motivo: $($_.Exception.Message)"
        }

        $Snapshots += [pscustomobject]@{
            Name = $Share.Name
            Path = $Share.Path
            Description = $Share.Description
            CachingMode = $Share.CachingMode
            FolderEnumerationMode = $Share.FolderEnumerationMode
            ConcurrentUserLimit = $Share.ConcurrentUserLimit
            Access = $Acessos
        }

        try {
            LogMsg "Removendo compartilhamento temporariamente: $($Share.Name) -> $($Share.Path)"
            Remove-SmbShare -Name $Share.Name -Force -ErrorAction Stop
        }
        catch {
            LogMsg "ERRO: Nao foi possivel remover o compartilhamento $($Share.Name). A extracao da versao foi cancelada."
            LogMsg "Motivo: $($_.Exception.Message)"
            exit 1
        }
    }

    return $Snapshots
}

function RestaurarPermissaoCompartilhamento {
    param(
        [string]$NomeCompartilhamento,
        [string[]]$Conta,
        [string]$TipoControle,
        [string]$Direito
    )

    $UltimoErro = ""

    foreach ($ContaAtual in @($Conta | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
        try {
            if ($TipoControle -eq "Deny") {
                Block-SmbShareAccess -Name $NomeCompartilhamento -AccountName $ContaAtual -Force -ErrorAction Stop | Out-Null
            }
            else {
                Grant-SmbShareAccess -Name $NomeCompartilhamento -AccountName $ContaAtual -AccessRight $Direito -Force -ErrorAction Stop | Out-Null
            }

            LogMsg "Permissao $Direito aplicada para $ContaAtual no compartilhamento $NomeCompartilhamento."
            return $true
        }
        catch {
            $UltimoErro = $_.Exception.Message
        }
    }

    LogMsg "AVISO: Nao foi possivel aplicar permissao $Direito para $($Conta -join ', ') no compartilhamento $NomeCompartilhamento. Motivo: $UltimoErro"
    return $false
}

function GarantirPermissoesPadraoCompartilhamento {
    param([string]$NomeCompartilhamento)

    foreach ($Sid in @("S-1-1-0", "S-1-5-2", "S-1-5-32-546")) {
        $Contas = @(ObterContasCompartilhamentoPorSid $Sid)
        RestaurarPermissaoCompartilhamento -NomeCompartilhamento $NomeCompartilhamento -Conta $Contas -TipoControle "Allow" -Direito "Full" | Out-Null
    }
}

function RestaurarCompartilhamentosTekSoftware {
    param([array]$Compartilhamentos)

    if (!$Compartilhamentos -or $Compartilhamentos.Count -eq 0) {
        return
    }

    if (!(Get-Command New-SmbShare -ErrorAction SilentlyContinue)) {
        LogMsg "ERRO: New-SmbShare nao disponivel. Nao foi possivel restaurar compartilhamentos automaticamente."
        exit 1
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

            $AcessosAtuais = @(Get-SmbShareAccess -Name $Share.Name -ErrorAction SilentlyContinue)

            foreach ($AcessoAtual in $AcessosAtuais) {
                try {
                    Revoke-SmbShareAccess -Name $Share.Name -AccountName $AcessoAtual.AccountName -Force -ErrorAction Stop | Out-Null
                }
                catch {
                    LogMsg "AVISO: Nao foi possivel remover permissao temporaria de $($AcessoAtual.AccountName) no compartilhamento $($Share.Name)."
                }
            }

            foreach ($Acesso in $Share.Access) {
                RestaurarPermissaoCompartilhamento -NomeCompartilhamento $Share.Name -Conta $Acesso.AccountName -TipoControle $Acesso.AccessControlType -Direito $Acesso.AccessRight
            }

            GarantirPermissoesPadraoCompartilhamento -NomeCompartilhamento $Share.Name
        }
        catch {
            LogMsg "ERRO: Falha ao restaurar compartilhamento $($Share.Name)."
            LogMsg "Motivo: $($_.Exception.Message)"
            exit 1
        }
    }
}

function FinalizarProcessosTek {
    LogMsg "====================================="
    LogMsg "Finalizando processos iniciados com Tek..."

    $ProcessosTek = @(ObterProcessosTek)

    if ($ProcessosTek.Count -eq 0) {
        LogMsg "Nenhum processo Tek encontrado."
        return
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

    $ProcessosRestantes = @(ObterProcessosTek)

    foreach ($Proc in $ProcessosRestantes) {
        try {
            LogMsg "Forcando encerramento via taskkill: $($Proc.ProcessName).exe PID $($Proc.Id)"
            Start-Process -FilePath "taskkill.exe" -ArgumentList @("/PID", $Proc.Id, "/F", "/T") -Wait -NoNewWindow | Out-Null
        }
        catch {
            LogMsg "AVISO: Nao foi possivel executar taskkill para $($Proc.ProcessName). Motivo: $($_.Exception.Message)"
        }
    }

    $Limite = (Get-Date).AddSeconds(10)

    do {
        $ProcessosRestantes = @(ObterProcessosTek)

        if ($ProcessosRestantes.Count -eq 0) {
            LogMsg "Todos os processos Tek* foram finalizados."
            return
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $Limite)

    LogMsg "ERRO: Ainda existem processos Tek* em execucao. A extracao da versao foi cancelada."

    foreach ($Proc in $ProcessosRestantes) {
        LogMsg "Processo ainda ativo: $($Proc.ProcessName).exe PID $($Proc.Id)"
    }

    exit 1
}

function ExtrairArquivoVersaoZipDotNet {
    param(
        [string]$Pacote,
        [string]$Destino
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop

    $DestinoBase = [System.IO.Path]::GetFullPath($Destino).TrimEnd("\") + "\"
    $ArquivoZip = [System.IO.Compression.ZipFile]::OpenRead($Pacote)

    try {
        foreach ($Entrada in $ArquivoZip.Entries) {
            $Relativo = $Entrada.FullName -replace "/", "\"

            if ([string]::IsNullOrWhiteSpace($Relativo)) {
                continue
            }

            $DestinoEntrada = [System.IO.Path]::GetFullPath((Join-Path $Destino $Relativo))

            if (!$DestinoEntrada.StartsWith($DestinoBase, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Entrada fora do destino permitida: $($Entrada.FullName)"
            }

            if ([string]::IsNullOrWhiteSpace($Entrada.Name)) {
                New-Item -ItemType Directory -Path $DestinoEntrada -Force | Out-Null
                continue
            }

            $PastaDestino = Split-Path -Parent $DestinoEntrada
            if (!(Test-Path $PastaDestino)) {
                New-Item -ItemType Directory -Path $PastaDestino -Force | Out-Null
            }

            if (Test-Path $DestinoEntrada) {
                Remove-Item -LiteralPath $DestinoEntrada -Force -ErrorAction Stop
            }

            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($Entrada, $DestinoEntrada)
        }
    }
    finally {
        $ArquivoZip.Dispose()
    }
}

function ExtrairArquivoVersaoShell {
    param(
        [string]$Pacote,
        [string]$Destino
    )

    $Shell = New-Object -ComObject Shell.Application
    $OrigemShell = $Shell.NameSpace($Pacote)
    $DestinoShell = $Shell.NameSpace($Destino)

    if ($null -eq $OrigemShell -or $null -eq $DestinoShell) {
        throw "Shell.Application nao conseguiu abrir o pacote ou destino."
    }

    $DestinoShell.CopyHere($OrigemShell.Items(), 20)
    Start-Sleep -Seconds 5
}

function ExtrairArquivoVersaoCompat {
    param(
        [string]$Pacote,
        [string]$Destino
    )

    if (!(Test-Path $Destino)) {
        New-Item -ItemType Directory -Path $Destino -Force | Out-Null
    }

    $Tar = Get-Command "tar.exe" -ErrorAction SilentlyContinue

    if ($Tar) {
        LogMsg "Extraindo versao com tar: $($Tar.Source)"
        LogMsg "Comando equivalente: tar -xf `"$Pacote`" -C `"$Destino`""

        $ProcTar = Start-Process -FilePath $Tar.Source -ArgumentList @("-xf", $Pacote, "-C", $Destino) -Wait -PassThru
        LogMsg "Extracao com tar finalizada. ExitCode: $($ProcTar.ExitCode)"

        if ($ProcTar.ExitCode -eq 0) {
            return 0
        }

        LogMsg "AVISO: tar.exe retornou codigo $($ProcTar.ExitCode). Tentando extracao alternativa."
    }
    else {
        LogMsg "AVISO: tar.exe nao encontrado neste Windows. Tentando extracao alternativa via .NET."
    }

    try {
        LogMsg "Extraindo versao via .NET ZipFile."
        ExtrairArquivoVersaoZipDotNet -Pacote $Pacote -Destino $Destino
        LogMsg "Extracao via .NET ZipFile finalizada com sucesso."
        return 0
    }
    catch {
        LogMsg "AVISO: Extracao via .NET ZipFile falhou: $($_.Exception.Message)"
    }

    try {
        LogMsg "Tentando extracao via Shell.Application."
        ExtrairArquivoVersaoShell -Pacote $Pacote -Destino $Destino
        LogMsg "Extracao via Shell.Application solicitada."
        return 0
    }
    catch {
        LogMsg "ERRO: Extracao via Shell.Application falhou: $($_.Exception.Message)"
        return 1
    }
}

function AtualizarVersaoTekFarma {
    param(
        [string]$TipoVersao
    )

    LogMsg "====================================="
    LogMsg "INICIANDO ATUALIZACAO DO TEKFARMA"
    LogMsg "Tipo de versao: $TipoVersao"
    LogMsg "Destino: $DestinoSistema"

    if (!(Test-Path $DestinoSistema)) {
        LogMsg "Pasta destino nao existe. Criando: $DestinoSistema"
        New-Item -ItemType Directory -Path $DestinoSistema -Force | Out-Null
    }

    if ($TipoVersao -eq "normal") {
        $Pacote = Join-Path $Base "TekFarma50.exe"
    }
    elseif ($TipoVersao -eq "i") {
        $Pacote = Join-Path $Base "TekFarma50i.exe"
    }
    else {
        LogMsg "ERRO: Tipo de versao invalido ou vazio: $TipoVersao"
        exit 1
    }

    if (!(Test-Path $Pacote)) {
        LogMsg "ERRO: Pacote da versao nao encontrado: $Pacote"
        exit 1
    }

    Unblock-File $Pacote -ErrorAction SilentlyContinue

    LogMsg "Pacote encontrado: $Pacote"
    LogMsg "Garantindo que nao exista processo Tek* ativo antes da extracao..."
    FinalizarProcessosTek

    $CompartilhamentosSuspensos = @(SuspenderCompartilhamentosTekSoftware)

    try {
        $CodigoExtracao = ExtrairArquivoVersaoCompat -Pacote $Pacote -Destino $DestinoSistema
    }
    finally {
        RestaurarCompartilhamentosTekSoftware -Compartilhamentos $CompartilhamentosSuspensos
    }

    LogMsg "Extracao da versao finalizada. ExitCode: $CodigoExtracao"

    if ($CodigoExtracao -ne 0) {
        LogMsg "ERRO: Falha ao extrair a versao."
        exit 1
    }

    LogMsg "Atualizacao da versao aplicada com sucesso."
}

function RemoverCrystalAntigo {
    LogMsg "====================================="
    LogMsg "Procurando instalacoes antigas do Crystal Reports..."

    $CrystalInstalados = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.DisplayName -like "*SAP Crystal Reports runtime engine for .NET Framework*") -or
            ($_.DisplayName -like "*Crystal Reports runtime engine*") -or
            ($_.DisplayName -like "*SAP Crystal*") -or
            ($_.DisplayName -like "*Crystal*Reports*")
        }

    if ($CrystalInstalados) {
        foreach ($Crystal in $CrystalInstalados) {
            LogMsg "Encontrado: $($Crystal.DisplayName) - $($Crystal.DisplayVersion)"
            LogMsg "PSChildName: $($Crystal.PSChildName)"
            LogMsg "UninstallString: $($Crystal.UninstallString)"

            if ($Crystal.PSChildName -match "^\{.*\}$") {
                $Guid = $Crystal.PSChildName
                LogMsg "Desinstalando Crystal via MSI: $Guid"

                $ArgsUninstall = "/x $Guid /qn /norestart /L*v `"$CrystalUninstallLog`""

                $ProcUninstall = Start-Process -FilePath "msiexec.exe" -ArgumentList $ArgsUninstall -Wait -PassThru

                LogMsg "Desinstalacao Crystal ExitCode: $($ProcUninstall.ExitCode)"

                if ($ProcUninstall.ExitCode -ne 0 -and $ProcUninstall.ExitCode -ne 3010 -and $ProcUninstall.ExitCode -ne 1605) {
                    LogMsg "AVISO: Falha ao desinstalar pelo MSI. O registro pode continuar aparecendo no appwiz.cpl."
                }
            }
            else {
                LogMsg "AVISO: ProductCode/GUID nao encontrado. Nao foi possivel desinstalar automaticamente este item."
            }
        }
    }
    else {
        LogMsg "Nenhuma instalacao do Crystal encontrada no registro."
    }

    LogMsg "Limpando registros orfaos do Crystal no appwiz.cpl..."

    $CrystalRestantes = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.DisplayName -like "*SAP Crystal Reports runtime engine for .NET Framework*") -or
            ($_.DisplayName -like "*Crystal Reports runtime engine*") -or
            ($_.DisplayName -like "*SAP Crystal*") -or
            ($_.DisplayName -like "*Crystal*Reports*")
        }

    foreach ($Item in $CrystalRestantes) {
        try {
            LogMsg "Removendo chave orfa do registro: $($Item.DisplayName)"
            Remove-Item -LiteralPath $Item.PSPath -Recurse -Force -ErrorAction Stop
        }
        catch {
            LogMsg "AVISO: Nao foi possivel remover chave orfa: $($_.Exception.Message)"
        }
    }

    LogMsg "====================================="
    LogMsg "Limpando pastas antigas SAP BusinessObjects..."

    $PastasCrystal = @(
        "C:\Program Files (x86)\SAP BusinessObjects",
        "C:\Program Files\SAP BusinessObjects"
    )

    foreach ($Pasta in $PastasCrystal) {
        if (Test-Path $Pasta) {
            LogMsg "Apagando pasta: $Pasta"

            try {
                Remove-Item -LiteralPath $Pasta -Recurse -Force -ErrorAction Stop
                LogMsg "Pasta apagada com sucesso: $Pasta"
            }
            catch {
                LogMsg "AVISO: Nao foi possivel apagar totalmente a pasta: $Pasta"
                LogMsg "Motivo: $($_.Exception.Message)"
            }
        }
        else {
            LogMsg "Pasta nao existe: $Pasta"
        }
    }
}

function InstalarCrystalNovo {
    LogMsg "====================================="
    LogMsg "Instalando Crystal Reports Runtime novo..."

    if (!(Test-Path $CrystalMsi)) {
        LogMsg "ERRO: MSI nao encontrado: $CrystalMsi"
        exit 1
    }

    Unblock-File $CrystalMsi -ErrorAction SilentlyContinue

    $ArgsCrystal = '/i "' + $CrystalMsi + '" /qn /norestart /L*v "' + $CrystalLog + '"'

    $Processo = Start-Process -FilePath "msiexec.exe" -ArgumentList $ArgsCrystal -Wait -PassThru

    LogMsg "Crystal Reports Runtime finalizado. ExitCode: $($Processo.ExitCode)"

    if ($Processo.ExitCode -ne 0 -and $Processo.ExitCode -ne 3010) {
        LogMsg "ERRO: Crystal nao instalou corretamente. Veja o log: $CrystalLog"
        exit 1
    }
}

function AplicarFixCrystal {
    LogMsg "====================================="
    LogMsg "Aplicando crdb_adoplus.zip..."

    if (!(Test-Path $FixZip)) {
        LogMsg "ERRO: ZIP nao encontrado: $FixZip"
        exit 1
    }

    if (!(Test-Path $DestinoCrystal)) {
        LogMsg "ERRO: Pasta destino do Crystal nao encontrada: $DestinoCrystal"
        exit 1
    }

    if (Test-Path $TempFix) {
        Remove-Item -LiteralPath $TempFix -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $TempFix -Force | Out-Null

    try {
        Unblock-File $FixZip -ErrorAction SilentlyContinue

        Expand-Archive -LiteralPath $FixZip -DestinationPath $TempFix -Force

        LogMsg "Copiando arquivos do fix para: $DestinoCrystal"

        Copy-Item -Path (Join-Path $TempFix "*") -Destination $DestinoCrystal -Recurse -Force -ErrorAction Stop

        LogMsg "Fix crdb_adoplus aplicado com sucesso."
    }
    catch {
        LogMsg "ERRO ao aplicar fix: $($_.Exception.Message)"
        exit 1
    }
    finally {
        if (Test-Path $TempFix) {
            Remove-Item -LiteralPath $TempFix -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Clear-Host

LogMsg "====================================="
LogMsg "INSTALADOR FINAL - TEKFARMA / CRYSTAL"
LogMsg "====================================="
LogMsg "Modo recebido: $Modo"
LogMsg "TipoVersao recebido: $TipoVersao"
LogMsg "Base: $Base"

if (!(Test-Admin)) {
    LogMsg "ERRO: Este script NAO esta rodando como Administrador."
    LogMsg "Execute pelo install.ps1/start.ps1 para elevar automaticamente."
    exit 1
}

if ($Modo -notin @("1", "2", "3")) {
    LogMsg "ERRO: Modo invalido: $Modo"
    exit 1
}

if ($Modo -eq "1" -or $Modo -eq "3") {
    AtualizarVersaoTekFarma -TipoVersao $TipoVersao
}

if ($Modo -eq "2" -or $Modo -eq "3") {
    InstalarExe $Net48 "/q /norestart" ".NET Framework 4.8 Offline"
    InstalarExe $VCx86 "/install /quiet /norestart" "Visual C++ Redistributable x86"
    InstalarExe $VCx64 "/install /quiet /norestart" "Visual C++ Redistributable x64"
}

if ($Modo -eq "2" -or $Modo -eq "3") {
    FinalizarProcessosTek
    RemoverCrystalAntigo
    InstalarCrystalNovo
    AplicarFixCrystal
}

LogMsg "====================================="
LogMsg "PROCESSO FINALIZADO COM SUCESSO"
LogMsg "Log geral: $Log"
LogMsg "Log Crystal install: $CrystalLog"
LogMsg "Log Crystal uninstall: $CrystalUninstallLog"
LogMsg "====================================="

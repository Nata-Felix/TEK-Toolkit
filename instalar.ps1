param(
    [string]$Modo = "3",
    [string]$TipoVersao = "",
    [string]$ReparoSemCrystal = "false",
    [string]$CompatibilidadeWin7 = "false",
    [string]$VerificarAntesDeBaixar = "false"
)

$Base = Split-Path -Parent $MyInvocation.MyCommand.Path

$Log = Join-Path $Base "install_final_log.txt"
$CrystalLog = Join-Path $Base "crystal_install.log"
$CrystalUninstallLog = Join-Path $Base "crystal_uninstall.log"

$Net48 = Join-Path $Base "dotnet48.exe"
$VCx86 = Join-Path $Base "VC_redist.x86.exe"
$VCx86Win7 = Join-Path $Base "VC_redist.x86.Win7.exe"
$VCx64 = Join-Path $Base "VC_redist.x64.exe"
$UcrtWin7x86 = Join-Path $Base "Windows6.1-KB2999226-x86.msu"
$UcrtWin7x64 = Join-Path $Base "Windows6.1-KB2999226-x64.msu"
$CrystalMsi = Join-Path $Base "CRRuntime_32bit_13_0_39.msi"
$FixZip = Join-Path $Base "crdb_adoplus.zip"
$TekSyncZip = Join-Path $Base "TekSync 1.10.0.zip"

$RaizTekSoftware = "C:\TekSoftware"
$DestinoSistema = Join-Path $RaizTekSoftware "TekFarma"
$ProgramFilesX86 = ${env:ProgramFiles(x86)}
if ([string]::IsNullOrEmpty($ProgramFilesX86)) {
    $ProgramFilesX86 = $env:ProgramFiles
}
$DestinoCrystal = Join-Path $ProgramFilesX86 "SAP BusinessObjects\Crystal Reports for .NET Framework 4.0\Common\SAP BusinessObjects Enterprise XI 4.0\win32_x86"
$TempFix = "C:\Windows\Temp\crdb_adoplus_fix"
$VersaoWindows = [Environment]::OSVersion.Version
$EhWindows7 = ($VersaoWindows.Major -eq 6 -and $VersaoWindows.Minor -eq 1)
$Sistema64Bits = ($env:PROCESSOR_ARCHITEW6432 -eq "AMD64" -or $env:PROCESSOR_ARCHITECTURE -eq "AMD64")
$PastaSistemaX86 = Join-Path $env:WINDIR "System32"
if ($Sistema64Bits) {
    $PastaSistemaX86 = Join-Path $env:WINDIR "SysWOW64"
}

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

    RemoverBloqueioArquivo $Caminho

    if (Test-TextoVazio $Argumentos) {
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

function Test-DotNet48Instalado {
    $ChavesDotNet = @(
        "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full"
    )

    foreach ($ChaveDotNet in $ChavesDotNet) {
        try {
            $Release = (Get-ItemProperty -Path $ChaveDotNet -Name "Release" -ErrorAction Stop).Release
            if ($null -ne $Release -and [int]$Release -ge 528040) {
                return $true
            }
        }
        catch {
        }
    }

    return $false
}

function Test-VisualCppInstalado {
    param([bool]$X64)

    $Caminhos = if ($X64) {
        @("HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64")
    }
    else {
        @(
            "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86",
            "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"
        )
    }

    foreach ($Caminho in $Caminhos) {
        try {
            $Instalado = (Get-ItemProperty -Path $Caminho -Name "Installed" -ErrorAction Stop).Installed
            if ($null -ne $Instalado -and [int]$Instalado -eq 1) {
                return $true
            }
        }
        catch {
        }
    }

    return $false
}

function GarantirDotNet48 {
    if (Test-DotNet48Instalado) {
        LogMsg ".NET Framework 4.8 ja esta instalado. Reinstalacao ignorada."
        return $true
    }

    $Resultado = InstalarExe $Net48 "/q /norestart" ".NET Framework 4.8 Offline"

    if (Test-DotNet48Instalado) {
        LogMsg ".NET Framework 4.8 confirmado no registro."
        return $true
    }

    if (!$Resultado) {
        LogMsg "ERRO: Instalador do .NET 4.8 falhou e a versao 4.8 nao foi detectada."
    }
    else {
        LogMsg "ERRO: .NET 4.8 ainda nao foi detectado. Reinicie o Windows e execute novamente."
    }

    return $false
}

function RepararVisualCppWin7AposCrystal {
    LogMsg "====================================="
    LogMsg "Reaplicando Visual C++ x86 para Windows 7 apos o Crystal..."

    if ($VerificarAntesDeBaixar -eq "true" -and (Test-VisualCppInstalado $false) -and !(Test-Path $VCx86Win7)) {
        LogMsg "Visual C++ x86 para Windows 7 ja instalado e pacote nao foi baixado. Reparo ignorado."
        return $true
    }

    if (InstalarExe $VCx86Win7 "/repair /quiet /norestart" "Reparo do Visual C++ x86 para Windows 7") {
        return $true
    }

    LogMsg "AVISO: O modo reparo falhou. Tentando instalar novamente."
    return (InstalarExe $VCx86Win7 "/install /quiet /norestart" "Reinstalacao do Visual C++ x86 para Windows 7")
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

    if (Test-TextoVazio $Caminho) {
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

    if ((Test-TextoVazio $BaseNormalizada) -or (Test-TextoVazio $PathNormalizado)) {
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
    # O usuario Convidado local usa SID terminado em -501; nao confundir com o grupo Convidados (-546).
    param([string]$Sid)

    $Contas = @()
    $Traduzida = ObterContaPorSid $Sid

    if (!(Test-TextoVazio $Traduzida)) {
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

    @($Contas | Where-Object { !(Test-TextoVazio $_) } | Select-Object -Unique)
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

    foreach ($ContaAtual in @($Conta | Where-Object { !(Test-TextoVazio $_) } | Select-Object -Unique)) {
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

    foreach ($Sid in @("S-1-1-0", "S-1-5-2")) {
        $Contas = @(ObterContasCompartilhamentoPorSid $Sid)
        RestaurarPermissaoCompartilhamento -NomeCompartilhamento $NomeCompartilhamento -Conta $Contas -TipoControle "Allow" -Direito "Full" | Out-Null
    }
    $Guest = Get-WmiObject Win32_UserAccount -Filter "LocalAccount=True" -ErrorAction SilentlyContinue | Where-Object { $_.SID -match "-501$" } | Select-Object -First 1
    if ($Guest) { RestaurarPermissaoCompartilhamento -NomeCompartilhamento $NomeCompartilhamento -Conta @("$env:COMPUTERNAME\$($Guest.Name)") -TipoControle "Allow" -Direito "Full" | Out-Null }
}

function InstalarAtualizacaoUcrtWindows7 {
    if (!$EhWindows7) {
        return $true
    }

    $UcrtBase = Join-Path $PastaSistemaX86 "ucrtbase.dll"
    if (Test-Path $UcrtBase) {
        LogMsg "Universal CRT do Windows 7 ja esta instalado."
        return $true
    }

    if ($VersaoWindows.Build -lt 7601) {
        LogMsg "ERRO: Windows 7 SP1 e obrigatorio para instalar o Crystal Runtime."
        return $false
    }

    $Pacote = $UcrtWin7x86
    if ($Sistema64Bits) {
        $Pacote = $UcrtWin7x64
    }

    LogMsg "====================================="
    LogMsg "Instalando Universal CRT KB2999226 para Windows 7..."
    LogMsg "Arquivo: $Pacote"

    if (!(Test-Path $Pacote)) {
        LogMsg "ERRO: Atualizacao KB2999226 nao encontrada: $Pacote"
        return $false
    }

    RemoverBloqueioArquivo $Pacote

    try {
        $Argumentos = '"' + $Pacote + '" /quiet /norestart'
        $Processo = Start-Process -FilePath "wusa.exe" -ArgumentList $Argumentos -Wait -PassThru
        LogMsg "KB2999226 finalizada. ExitCode: $($Processo.ExitCode)"

        if ($Processo.ExitCode -eq 0 -or $Processo.ExitCode -eq 3010 -or $Processo.ExitCode -eq 2359302) {
            if ($Processo.ExitCode -eq 3010) {
                LogMsg "AVISO: O Windows solicitou reinicializacao para concluir a KB2999226."
            }
            return $true
        }

        LogMsg "ERRO: Nao foi possivel instalar a KB2999226. ExitCode: $($Processo.ExitCode)"
        return $false
    }
    catch {
        LogMsg "ERRO ao instalar a KB2999226: $($_.Exception.Message)"
        return $false
    }
}

function ValidarRuntimeCrystal {
    LogMsg "====================================="
    LogMsg "Validando Crystal Runtime 32 bits..."

    $Crpe32 = Join-Path $DestinoCrystal "crpe32.dll"

    $ApiSetUcrt = Join-Path $PastaSistemaX86 "api-ms-win-crt-runtime-l1-1-0.dll"
    if (!(Test-Path $ApiSetUcrt)) {
        $ApiSetUcrt = Join-Path $PastaSistemaX86 "downlevel\api-ms-win-crt-runtime-l1-1-0.dll"
    }

    $ArquivosObrigatorios = @(
        $Crpe32,
        (Join-Path $PastaSistemaX86 "ucrtbase.dll"),
        $ApiSetUcrt,
        (Join-Path $PastaSistemaX86 "vcruntime140.dll"),
        (Join-Path $PastaSistemaX86 "msvcp140.dll"),
        (Join-Path $PastaSistemaX86 "mfc140u.dll")
    )

    $ArquivosAusentes = @()
    foreach ($ArquivoObrigatorio in $ArquivosObrigatorios) {
        if (!(Test-Path $ArquivoObrigatorio)) {
            $ArquivosAusentes += $ArquivoObrigatorio
        }
    }

    if ($ArquivosAusentes.Count -gt 0) {
        foreach ($ArquivoAusente in $ArquivosAusentes) {
            LogMsg "ERRO: Dependencia ausente: $ArquivoAusente"
        }
        LogMsg "ERRO: O crpe32.dll nao podera ser carregado. Reinicie o Windows e execute o instalador novamente."
        return $false
    }

    $VersaoCrpe = (Get-Item -LiteralPath $Crpe32).VersionInfo.FileVersion
    if (!$VersaoCrpe.StartsWith("13.0.39.")) {
        LogMsg "ERRO: Versao inesperada do crpe32.dll: $VersaoCrpe. Esperada: 13.0.39.x"
        return $false
    }

    LogMsg "Crystal Runtime $VersaoCrpe e dependencias nativas encontrados."
    return $true
}

function Test-TextoVazio {
    param([object]$Valor)
    return ($null -eq $Valor -or [string]::IsNullOrEmpty(([string]$Valor).Trim()))
}

function RemoverBloqueioArquivo {
    param([string]$Caminho)
    if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
        Unblock-File -LiteralPath $Caminho -ErrorAction SilentlyContinue
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

            if (!(Test-TextoVazio $Share.Description)) {
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

            if (Test-TextoVazio $Relativo) {
                continue
            }

            $DestinoEntrada = [System.IO.Path]::GetFullPath((Join-Path $Destino $Relativo))

            if (!$DestinoEntrada.StartsWith($DestinoBase, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Entrada fora do destino permitida: $($Entrada.FullName)"
            }

            if (Test-TextoVazio $Entrada.Name) {
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
        $Pacote = Join-Path $Base "TekFarma50v109.7.zip"
    }
    elseif ($TipoVersao -eq "i") {
        $Pacote = Join-Path $Base "TekFarma50v109.7i.zip"
    }
    else {
        LogMsg "ERRO: Tipo de versao invalido ou vazio: $TipoVersao"
        exit 1
    }

    if (!(Test-Path $Pacote)) {
        LogMsg "ERRO: Pacote da versao nao encontrado: $Pacote"
        exit 1
    }

    RemoverBloqueioArquivo $Pacote

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

function ObterSha256Arquivo {
    param([string]$Caminho)

    $Stream = [System.IO.File]::OpenRead($Caminho)
    $Sha256 = [System.Security.Cryptography.SHA256]::Create()

    try {
        return ([System.BitConverter]::ToString($Sha256.ComputeHash($Stream))).Replace("-", "")
    }
    finally {
        $Sha256.Dispose()
        $Stream.Dispose()
    }
}

function Test-TekFarma1096 {
    $TekAplicacao = Join-Path $DestinoSistema "TekAplicacao.exe"

    if (!(Test-Path -LiteralPath $TekAplicacao -PathType Leaf)) {
        LogMsg "ERRO: TekAplicacao.exe nao encontrado em $DestinoSistema."
        return $false
    }

    $VersaoArquivo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($TekAplicacao).FileVersion

    if ($VersaoArquivo -match "^85([.]|$)") {
        LogMsg "TekFarma 1.09.6 confirmado (build do executavel: $VersaoArquivo)."
        return $true
    }

    LogMsg "ERRO: O TekFarma instalado nao corresponde a versao 1.09.6. Build encontrado: $VersaoArquivo"
    LogMsg "Atualize primeiro pela opcao 'Somente Versao' e depois execute 'Atualizar TekSync'."
    return $false
}

function ObterNovoAutenticador {
    param([string]$ArquivoAutenticador)

    $Valor = ""

    foreach ($Linha in [System.IO.File]::ReadAllLines($ArquivoAutenticador)) {
        if ($Linha -match "^\s*Autenticador\s*=(.*)$") {
            $Valor = $Matches[1].Trim()
            break
        }
    }

    if (Test-TextoVazio $Valor) {
        throw "O arquivo autenticador_sync.txt nao possui uma chave Autenticador valida."
    }

    return $Valor
}

function Test-SyncIniServidor {
    param([string]$CaminhoSyncIni)

    $Conteudo = (LerArquivoTextoPreservandoEncoding $CaminhoSyncIni).Texto
    $PadraoEndereco = "(?im)^[\t ]*Endereco[\t ]*=[\t ]*C:\\TekSoftware\\TekFarma\\TEKSYNC[.]FDB[\t ]*(?=\r\n|\n|\r|$)"
    $PadraoAutenticador = "(?im)^[\t ]*Autenticador[\t ]*="
    $Enderecos = [System.Text.RegularExpressions.Regex]::Matches($Conteudo, $PadraoEndereco)
    $Autenticadores = [System.Text.RegularExpressions.Regex]::Matches($Conteudo, $PadraoAutenticador)

    if ($Enderecos.Count -ne 1 -or $Autenticadores.Count -gt 1) {
        return $false
    }

    if ($Autenticadores.Count -eq 0) {
        return $true
    }

    $InicioEndereco = $Enderecos[0].Index
    $FimSecao = $Conteudo.Length
    $ProximaSecao = [System.Text.RegularExpressions.Regex]::Match($Conteudo.Substring($Enderecos[0].Index + $Enderecos[0].Length), "(?m)^[\t ]*\[")

    if ($ProximaSecao.Success) {
        $FimSecao = $Enderecos[0].Index + $Enderecos[0].Length + $ProximaSecao.Index
    }

    return ($Autenticadores[0].Index -gt $InicioEndereco -and $Autenticadores[0].Index -lt $FimSecao)
}

function LerArquivoTextoPreservandoEncoding {
    param([string]$Caminho)

    [byte[]]$Bytes = [System.IO.File]::ReadAllBytes($Caminho)
    [byte[]]$Preambulo = @()
    $Offset = 0
    $Encoding = $null

    if ($Bytes.Length -ge 4 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE -and $Bytes[2] -eq 0x00 -and $Bytes[3] -eq 0x00) {
        $Encoding = New-Object System.Text.UTF32Encoding($false, $false, $true)
        [byte[]]$Preambulo = @(0xFF, 0xFE, 0x00, 0x00)
        $Offset = 4
    }
    elseif ($Bytes.Length -ge 4 -and $Bytes[0] -eq 0x00 -and $Bytes[1] -eq 0x00 -and $Bytes[2] -eq 0xFE -and $Bytes[3] -eq 0xFF) {
        $Encoding = New-Object System.Text.UTF32Encoding($true, $false, $true)
        [byte[]]$Preambulo = @(0x00, 0x00, 0xFE, 0xFF)
        $Offset = 4
    }
    elseif ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
        $Encoding = New-Object System.Text.UTF8Encoding($false, $true)
        [byte[]]$Preambulo = @(0xEF, 0xBB, 0xBF)
        $Offset = 3
    }
    elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) {
        $Encoding = New-Object System.Text.UnicodeEncoding($false, $false, $true)
        [byte[]]$Preambulo = @(0xFF, 0xFE)
        $Offset = 2
    }
    elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) {
        $Encoding = New-Object System.Text.UnicodeEncoding($true, $false, $true)
        [byte[]]$Preambulo = @(0xFE, 0xFF)
        $Offset = 2
    }
    else {
        try {
            $Encoding = New-Object System.Text.UTF8Encoding($false, $true)
            [void]$Encoding.GetString($Bytes)
        }
        catch {
            $Encoding = [System.Text.Encoding]::Default
        }
    }

    $Texto = $Encoding.GetString($Bytes, $Offset, $Bytes.Length - $Offset)
    return New-Object PSObject -Property @{
        Texto = $Texto
        Encoding = $Encoding
        Preambulo = $Preambulo
    }
}

function GravarArquivoTextoAtomico {
    param(
        [string]$Caminho,
        [string]$Conteudo,
        [object]$DadosEncoding
    )

    [byte[]]$BytesConteudo = $DadosEncoding.Encoding.GetBytes($Conteudo)
    [byte[]]$Preambulo = $DadosEncoding.Preambulo
    [byte[]]$BytesFinais = New-Object byte[] ($Preambulo.Length + $BytesConteudo.Length)

    if ($Preambulo.Length -gt 0) {
        [System.Array]::Copy($Preambulo, 0, $BytesFinais, 0, $Preambulo.Length)
    }

    [System.Array]::Copy($BytesConteudo, 0, $BytesFinais, $Preambulo.Length, $BytesConteudo.Length)

    $Pasta = Split-Path -Parent $Caminho
    $Nome = Split-Path -Leaf $Caminho
    $Identificador = $PID.ToString() + "." + [Guid]::NewGuid().ToString("N")
    $ArquivoTemporario = Join-Path $Pasta ("." + $Nome + "." + $Identificador + ".tmp")
    $BackupTroca = Join-Path $Pasta ("." + $Nome + "." + $Identificador + ".bak")

    try {
        [System.IO.File]::WriteAllBytes($ArquivoTemporario, $BytesFinais)
        [System.IO.File]::Replace($ArquivoTemporario, $Caminho, $BackupTroca)
    }
    finally {
        if (Test-Path -LiteralPath $ArquivoTemporario) {
            Remove-Item -LiteralPath $ArquivoTemporario -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $BackupTroca) {
            Remove-Item -LiteralPath $BackupTroca -Force -ErrorAction SilentlyContinue
        }
    }
}

function CriarBackupSyncIni {
    param([string]$CaminhoSyncIni)

    $Pasta = Split-Path -Parent $CaminhoSyncIni
    $Backup = Join-Path $Pasta "bckp_sync.ini"

    if (Test-Path -LiteralPath $Backup) {
        $Backup = Join-Path $Pasta ("bckp_sync_" + (Get-Date -Format "yyyyMMdd_HHmmss_fff") + "_" + $PID + ".ini")
    }

    Copy-Item -LiteralPath $CaminhoSyncIni -Destination $Backup -Force -ErrorAction Stop

    if ((ObterSha256Arquivo $CaminhoSyncIni) -ne (ObterSha256Arquivo $Backup)) {
        throw "A copia de seguranca do sync.ini falhou na verificacao."
    }

    return $Backup
}

function AtualizarAutenticadorSyncIni {
    param(
        [string]$CaminhoSyncIni,
        [string]$NovoAutenticador
    )

    $DadosArquivo = LerArquivoTextoPreservandoEncoding $CaminhoSyncIni
    $ConteudoOriginal = $DadosArquivo.Texto
    $PadraoEndereco = "(?im)^[\t ]*Endereco[\t ]*=[\t ]*C:\\TekSoftware\\TekFarma\\TEKSYNC[.]FDB[\t ]*(?=\r\n|\n|\r|$)"
    $PadraoAutenticador = "(?im)^(?<prefixo>[\t ]*Autenticador[\t ]*=[\t ]*)(?<valor>[^\r\n]*)"
    $Enderecos = [System.Text.RegularExpressions.Regex]::Matches($ConteudoOriginal, $PadraoEndereco)
    $Autenticadores = [System.Text.RegularExpressions.Regex]::Matches($ConteudoOriginal, $PadraoAutenticador)

    if ($Enderecos.Count -ne 1) {
        throw "O sync.ini deve possuir um unico Endereco apontando para C:\TekSoftware\TekFarma\TEKSYNC.FDB."
    }

    if ($Autenticadores.Count -gt 1) {
        throw "O sync.ini possui mais de uma chave Autenticador. A edicao foi cancelada."
    }

    $NovoConteudo = ""
    $Acao = ""

    if ($Autenticadores.Count -eq 1) {
        $InicioEndereco = $Enderecos[0].Index
        $FimSecao = $ConteudoOriginal.Length
        $ProximaSecao = [System.Text.RegularExpressions.Regex]::Match($ConteudoOriginal.Substring($Enderecos[0].Index + $Enderecos[0].Length), "(?m)^[\t ]*\[")

        if ($ProximaSecao.Success) {
            $FimSecao = $Enderecos[0].Index + $Enderecos[0].Length + $ProximaSecao.Index
        }

        if ($Autenticadores[0].Index -le $InicioEndereco -or $Autenticadores[0].Index -ge $FimSecao) {
            throw "A chave Autenticador existente nao esta abaixo do Endereco na mesma secao. A edicao foi cancelada."
        }

        $GrupoValor = $Autenticadores[0].Groups["valor"]
        $NovoConteudo = $ConteudoOriginal.Substring(0, $GrupoValor.Index) + $NovoAutenticador + $ConteudoOriginal.Substring($GrupoValor.Index + $GrupoValor.Length)
        $Acao = "substituida"
    }
    else {
        $FimEndereco = $Enderecos[0].Index + $Enderecos[0].Length
        $QuebraLinha = [Environment]::NewLine
        $AposEndereco = $ConteudoOriginal.Substring($FimEndereco)

        if ($AposEndereco.StartsWith("`r`n")) {
            $QuebraLinha = "`r`n"
        }
        elseif ($AposEndereco.StartsWith("`n")) {
            $QuebraLinha = "`n"
        }
        elseif ($AposEndereco.StartsWith("`r")) {
            $QuebraLinha = "`r"
        }

        $NovoConteudo = $ConteudoOriginal.Substring(0, $FimEndereco) + $QuebraLinha + "Autenticador=" + $NovoAutenticador + $ConteudoOriginal.Substring($FimEndereco)
        $Acao = "adicionada"
    }

    GravarArquivoTextoAtomico -Caminho $CaminhoSyncIni -Conteudo $NovoConteudo -DadosEncoding $DadosArquivo
    return $Acao
}

function AtualizarTekSync {
    $HashZipEsperado = "D919EFD9264DFAE5D63E00602143DCF510CD849AFEE298F17893C94937725684"
    $HashExeEsperado = "D433188CCF48D6BC5739BBACD6D96643DA06F7A8CB8FE3C7B83C4797400BC160"
    $HashAutenticadorEsperado = "96E69F684C95C94BE8EEF4E3E6CC512B3519186E39A29C5AFFC6ECB7A9DFF4C2"
    $TekSyncExe = Join-Path $DestinoSistema "TekSync.exe"
    $SyncIni = Join-Path $DestinoSistema "sync.ini"
    $TempTekSync = Join-Path $Base "TekSync_Extraido"
    $BackupTekSync = Join-Path $DestinoSistema ("Backup_TekSync_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
    $ArquivosNovos = @()
    $BackupCriado = $false

    LogMsg "====================================="
    LogMsg "ATUALIZACAO TEKSYNC 1.10.0"
    LogMsg "Destino: $DestinoSistema"

    if (!(Test-TekFarma1096)) {
        return $false
    }

    if (!(Test-Path -LiteralPath $TekSyncExe -PathType Leaf)) {
        LogMsg "ERRO: TekSync.exe nao encontrado. Execute esta opcao somente no servidor."
        return $false
    }

    if (!(Test-Path -LiteralPath $SyncIni -PathType Leaf)) {
        LogMsg "ERRO: sync.ini nao encontrado. Execute esta opcao somente no servidor."
        return $false
    }

    if (!(Test-SyncIniServidor $SyncIni)) {
        LogMsg "ERRO: O sync.ini nao possui um Endereco local valido ou possui chaves Autenticador duplicadas/fora da secao do servidor."
        LogMsg "Esta atualizacao deve ser executada somente no servidor."
        return $false
    }

    if (!(Test-Path -LiteralPath $TekSyncZip -PathType Leaf)) {
        LogMsg "ERRO: Pacote TekSync nao encontrado: $TekSyncZip"
        return $false
    }

    $HashZip = ObterSha256Arquivo $TekSyncZip
    if ($HashZip -ne $HashZipEsperado) {
        LogMsg "ERRO: O pacote TekSync 1.10.0 falhou na verificacao SHA-256."
        return $false
    }

    LogMsg "Pacote TekSync 1.10.0 validado."

    foreach ($ProcessoTekSync in @(Get-Process -Name "TekSync" -ErrorAction SilentlyContinue)) {
        LogMsg "Fechando processo TekSync.exe (PID $($ProcessoTekSync.Id))..."
        Stop-Process -Id $ProcessoTekSync.Id -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 2
    if (Get-Process -Name "TekSync" -ErrorAction SilentlyContinue) {
        LogMsg "ERRO: Nao foi possivel fechar o TekSync.exe."
        return $false
    }

    if (Test-Path $TempTekSync) {
        Remove-Item -LiteralPath $TempTekSync -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $TempTekSync -Force | Out-Null

    try {
        $CodigoExtracao = ExtrairArquivoVersaoCompat -Pacote $TekSyncZip -Destino $TempTekSync
        if ($CodigoExtracao -ne 0) {
            throw "Nao foi possivel extrair o pacote TekSync 1.10.0."
        }

        $LimiteExtracao = (Get-Date).AddMinutes(3)
        $ExtracaoValidada = $false

        do {
            $ExeExtraido = Get-ChildItem -Path $TempTekSync -Filter "TekSync.exe" -Recurse | Select-Object -First 1
            $AuthExtraido = Get-ChildItem -Path $TempTekSync -Filter "autenticador_sync.txt" -Recurse | Select-Object -First 1

            if ($null -ne $ExeExtraido -and $null -ne $AuthExtraido) {
                try {
                    $ExtracaoValidada = ((ObterSha256Arquivo $ExeExtraido.FullName) -eq $HashExeEsperado -and (ObterSha256Arquivo $AuthExtraido.FullName) -eq $HashAutenticadorEsperado)
                }
                catch {
                    $ExtracaoValidada = $false
                }
            }

            if (!$ExtracaoValidada) {
                Start-Sleep -Milliseconds 500
            }
        } while (!$ExtracaoValidada -and (Get-Date) -lt $LimiteExtracao)

        if (!$ExtracaoValidada) {
            throw "A extracao do TekSync 1.10.0 nao foi concluida ou os arquivos nao passaram na verificacao."
        }

        $NovoAutenticador = ObterNovoAutenticador $AuthExtraido.FullName
        $PastaPacote = $ExeExtraido.Directory.FullName
        $PastaPacotePrefixo = $PastaPacote.TrimEnd("\") + "\"

        New-Item -ItemType Directory -Path $BackupTekSync -Force | Out-Null
        $BackupCriado = $true
        Copy-Item -LiteralPath $SyncIni -Destination (Join-Path $BackupTekSync "sync.ini") -Force -ErrorAction Stop
        $BackupSyncIni = CriarBackupSyncIni $SyncIni
        LogMsg "Backup do sync.ini criado antes da edicao: $BackupSyncIni"

        foreach ($ArquivoPacote in @(Get-ChildItem -Path $PastaPacote -Recurse | Where-Object { !$_.PSIsContainer -and $_.Name -ine "autenticador_sync.txt" })) {
            $Relativo = $ArquivoPacote.FullName.Substring($PastaPacotePrefixo.Length)
            $ArquivoDestino = Join-Path $DestinoSistema $Relativo
            $PastaDestinoArquivo = Split-Path -Parent $ArquivoDestino

            if (!(Test-Path $PastaDestinoArquivo)) {
                New-Item -ItemType Directory -Path $PastaDestinoArquivo -Force | Out-Null
            }

            if (Test-Path -LiteralPath $ArquivoDestino -PathType Leaf) {
                $ArquivoBackup = Join-Path $BackupTekSync $Relativo
                $PastaBackupArquivo = Split-Path -Parent $ArquivoBackup
                if (!(Test-Path $PastaBackupArquivo)) {
                    New-Item -ItemType Directory -Path $PastaBackupArquivo -Force | Out-Null
                }
                Copy-Item -LiteralPath $ArquivoDestino -Destination $ArquivoBackup -Force -ErrorAction Stop
            }
            else {
                $ArquivosNovos += $ArquivoDestino
            }

            Copy-Item -LiteralPath $ArquivoPacote.FullName -Destination $ArquivoDestino -Force -ErrorAction Stop
            if ((ObterSha256Arquivo $ArquivoPacote.FullName) -ne (ObterSha256Arquivo $ArquivoDestino)) {
                throw "Falha ao validar o arquivo atualizado: $Relativo"
            }
        }

        $AcaoAutenticador = AtualizarAutenticadorSyncIni -CaminhoSyncIni $SyncIni -NovoAutenticador $NovoAutenticador

        if ((ObterSha256Arquivo $TekSyncExe) -ne $HashExeEsperado) {
            throw "A validacao final do TekSync.exe 1.10.0 falhou."
        }

        LogMsg "Arquivos do TekSync atualizados e chave Autenticador $AcaoAutenticador."
        LogMsg "O valor do autenticador foi protegido e nao foi exibido no log."
        LogMsg "Backup criado em: $BackupTekSync"

        $ProcessoNovo = Start-Process -FilePath $TekSyncExe -WorkingDirectory $DestinoSistema -PassThru
        $Finalizou = $ProcessoNovo.WaitForExit(8000)

        if ($Finalizou -and $ProcessoNovo.ExitCode -ne 0) {
            throw "TekSync.exe encerrou com ExitCode $($ProcessoNovo.ExitCode)."
        }

        if ($Finalizou) {
            LogMsg "TekSync.exe executado e finalizado sem erro imediato."
        }
        else {
            LogMsg "TekSync.exe iniciado com sucesso (PID $($ProcessoNovo.Id))."
        }

        LogMsg "Confirme na tela do TekSync se a sincronizacao foi concluida sem erros."
        return $true
    }
    catch {
        LogMsg "ERRO na atualizacao do TekSync: $($_.Exception.Message)"

        if ($BackupCriado) {
            LogMsg "Restaurando arquivos anteriores do backup..."
            try {
                foreach ($ArquivoBackup in @(Get-ChildItem -Path $BackupTekSync -Recurse | Where-Object { !$_.PSIsContainer })) {
                    $PrefixoBackup = $BackupTekSync.TrimEnd("\") + "\"
                    $RelativoBackup = $ArquivoBackup.FullName.Substring($PrefixoBackup.Length)
                    $DestinoRestauracao = Join-Path $DestinoSistema $RelativoBackup
                    $PastaRestauracao = Split-Path -Parent $DestinoRestauracao
                    if (!(Test-Path $PastaRestauracao)) {
                        New-Item -ItemType Directory -Path $PastaRestauracao -Force -ErrorAction Stop | Out-Null
                    }
                    Copy-Item -LiteralPath $ArquivoBackup.FullName -Destination $DestinoRestauracao -Force -ErrorAction Stop
                }

                foreach ($ArquivoNovo in $ArquivosNovos) {
                    if (Test-Path -LiteralPath $ArquivoNovo -PathType Leaf) {
                        Remove-Item -LiteralPath $ArquivoNovo -Force -ErrorAction Stop
                    }
                }
                LogMsg "Restauracao concluida."
            }
            catch {
                LogMsg "ERRO: A restauracao automatica ficou incompleta: $($_.Exception.Message)"
            }
        }

        try {
            if ((Test-Path -LiteralPath $TekSyncExe -PathType Leaf) -and !(Get-Process -Name "TekSync" -ErrorAction SilentlyContinue)) {
                Start-Process -FilePath $TekSyncExe -WorkingDirectory $DestinoSistema | Out-Null
                LogMsg "TekSync anterior iniciado novamente apos a restauracao."
            }
        }
        catch {
            LogMsg "AVISO: Nao foi possivel reiniciar o TekSync anterior automaticamente."
        }

        return $false
    }
    finally {
        if (Test-Path $TempTekSync) {
            Remove-Item -LiteralPath $TempTekSync -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
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

    RemoverBloqueioArquivo $CrystalMsi

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
        RemoverBloqueioArquivo $FixZip

        $CodigoExtracao = ExtrairArquivoVersaoCompat -Pacote $FixZip -Destino $TempFix
        if ($CodigoExtracao -ne 0) {
            throw "Nao foi possivel extrair crdb_adoplus.zip."
        }

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
LogMsg "Compatibilidade Windows 7: $CompatibilidadeWin7"
LogMsg "Verificar antes de baixar: $VerificarAntesDeBaixar"
LogMsg "Base: $Base"

if (!(Test-Admin)) {
    LogMsg "ERRO: Este script NAO esta rodando como Administrador."
    LogMsg "Execute pelo install.ps1/start.ps1 para elevar automaticamente."
    exit 1
}

if (!(@("1", "2", "3", "4", "5") -contains $Modo)) {
    LogMsg "ERRO: Modo invalido: $Modo"
    exit 1
}

if ($Modo -eq "1" -or $Modo -eq "3") {
    AtualizarVersaoTekFarma -TipoVersao $TipoVersao
}

if ($Modo -eq "5") {
    if (!(AtualizarTekSync)) {
        exit 1
    }
}

if ($Modo -eq "2" -or $Modo -eq "3" -or $Modo -eq "4") {
    if (!(GarantirDotNet48)) {
        LogMsg "ERRO: Falha ao instalar o .NET Framework 4.8."
        exit 1
    }

    $UsarCompatibilidadeWin7 = ($Modo -eq "4" -or $CompatibilidadeWin7 -eq "true")
    if ($UsarCompatibilidadeWin7) {
        if (!(InstalarAtualizacaoUcrtWindows7)) {
            exit 1
        }
        if ($VerificarAntesDeBaixar -eq "true" -and (Test-VisualCppInstalado $false)) {
            LogMsg "Visual C++ x86 para Windows 7 ja instalado. Instalacao ignorada."
        }
        else {
            if (!(InstalarExe $VCx86Win7 "/install /quiet /norestart" "Visual C++ Redistributable x86 para Windows 7")) {
                LogMsg "ERRO: Falha ao instalar o Visual C++ x86 para Windows 7."
                exit 1
            }
        }
    }
    else {
        if ($VerificarAntesDeBaixar -eq "true" -and (Test-VisualCppInstalado $false)) {
            LogMsg "Visual C++ x86 ja instalado. Instalacao ignorada."
        }
        else {
            if (!(InstalarExe $VCx86 "/install /quiet /norestart" "Visual C++ Redistributable x86")) {
                LogMsg "ERRO: Falha ao instalar o Visual C++ x86."
                exit 1
            }
        }
        if ($VerificarAntesDeBaixar -eq "true" -and (Test-VisualCppInstalado $true)) {
            LogMsg "Visual C++ x64 ja instalado. Instalacao ignorada."
        }
        else {
            if (!(InstalarExe $VCx64 "/install /quiet /norestart" "Visual C++ Redistributable x64")) {
                LogMsg "ERRO: Falha ao instalar o Visual C++ x64."
                exit 1
            }
        }
    }
}

if ($Modo -eq "2" -or $Modo -eq "3" -or $Modo -eq "4") {
    FinalizarProcessosTek
    if ($ReparoSemCrystal -ne "true") {
        RemoverCrystalAntigo
        InstalarCrystalNovo
    }
    else {
        LogMsg "Reparo sem Crystal selecionado: desinstalacao e instalacao do Crystal Runtime ignoradas."
    }

    if ($ReparoSemCrystal -eq "true" -and !(Test-Path $DestinoCrystal)) {
        LogMsg "AVISO: Pasta do Crystal nao encontrada. Aplicacao do crdb_adoplus ignorada no reparo sem Crystal."
    }
    else {
        AplicarFixCrystal
    }

    if ($UsarCompatibilidadeWin7) {
        if (!(RepararVisualCppWin7AposCrystal)) {
            LogMsg "ERRO: Nao foi possivel reaplicar o Visual C++ x86 depois do Crystal."
            exit 1
        }
    }

    if (!(ValidarRuntimeCrystal)) {
        exit 1
    }
}

LogMsg "====================================="
LogMsg "PROCESSO FINALIZADO COM SUCESSO"
LogMsg "Log geral: $Log"
LogMsg "Log Crystal install: $CrystalLog"
LogMsg "Log Crystal uninstall: $CrystalUninstallLog"
LogMsg "====================================="

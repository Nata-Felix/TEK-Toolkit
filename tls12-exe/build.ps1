$ErrorActionPreference = "Stop"

$RaizProjeto = Split-Path -Parent $PSScriptRoot
$ScriptTls = Join-Path $RaizProjeto "corrigir_tls12_windows.ps1"
$Launcher = Join-Path $PSScriptRoot "executar_tls12.cmd"
$IExpress = Join-Path $env:WINDIR "SysWOW64\iexpress.exe"

if (!(Test-Path -LiteralPath $IExpress -PathType Leaf)) {
    $IExpress = Join-Path $env:WINDIR "System32\iexpress.exe"
}
$NomeExe = "CorrigirTLS12-Win7-Server2012.exe"
$Saida = Join-Path $RaizProjeto $NomeExe
$Stage = Join-Path ([IO.Path]::GetTempPath()) ("TEK_TLS12_EXE_" + $PID + "_" + [Guid]::NewGuid().ToString("N"))
$BuildConcluido = $false

if (!(Test-Path -LiteralPath $ScriptTls -PathType Leaf)) {
    throw "Script TLS nao encontrado: $ScriptTls"
}

if (!(Test-Path -LiteralPath $Launcher -PathType Leaf)) {
    throw "Launcher nao encontrado: $Launcher"
}

if (!(Test-Path -LiteralPath $IExpress -PathType Leaf)) {
    throw "IExpress nao encontrado neste Windows: $IExpress"
}

New-Item -ItemType Directory -Path $Stage -Force | Out-Null

try {
    Copy-Item -LiteralPath $ScriptTls -Destination (Join-Path $Stage "corrigir_tls12_windows.ps1") -Force
    Copy-Item -LiteralPath $Launcher -Destination (Join-Path $Stage "executar_tls12.cmd") -Force

    $SedPath = Join-Path $Stage "pacote_tls12.sed"
    $ExeTemporario = Join-Path $Stage $NomeExe
    $StageComBarra = $Stage.TrimEnd("\") + "\"

    $Sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$ExeTemporario
FriendlyName=Correcao TLS 1.2 - TEK Toolkit
AppLaunched=cmd.exe /c executar_tls12.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles

[Strings]
FILE0="corrigir_tls12_windows.ps1"
FILE1="executar_tls12.cmd"

[SourceFiles]
SourceFiles0=$StageComBarra

[SourceFiles0]
%FILE0%=
%FILE1%=
"@

    [IO.File]::WriteAllText($SedPath, $Sed, [Text.Encoding]::Default)
    & $IExpress /N /Q $SedPath

    $LimiteGeracao = (Get-Date).AddSeconds(30)
    while (!(Test-Path -LiteralPath $ExeTemporario -PathType Leaf) -and (Get-Date) -lt $LimiteGeracao) {
        Start-Sleep -Milliseconds 250
    }

    if (!(Test-Path -LiteralPath $ExeTemporario -PathType Leaf)) {
        throw "IExpress nao gerou o executavel esperado."
    }

    $Arquivo = Get-Item -LiteralPath $ExeTemporario
    if ($Arquivo.Length -lt 10240) {
        throw "Executavel gerado com tamanho invalido: $($Arquivo.Length) bytes."
    }

    $Stream = [IO.File]::OpenRead($ExeTemporario)
    try {
        if ($Stream.ReadByte() -ne 0x4D -or $Stream.ReadByte() -ne 0x5A) {
            throw "O arquivo gerado nao possui assinatura MZ de executavel Windows."
        }
    }
    finally {
        $Stream.Dispose()
    }

    Copy-Item -LiteralPath $ExeTemporario -Destination $Saida -Force
    $BuildConcluido = $true
}
finally {
    if ($BuildConcluido -and (Test-Path -LiteralPath $Stage)) {
        $StageResolvido = (Resolve-Path -LiteralPath $Stage).Path
        $TempRaiz = [IO.Path]::GetTempPath()

        if ($StageResolvido.StartsWith($TempRaiz, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $StageResolvido -Recurse -Force
        }
    }
    elseif (!$BuildConcluido) {
        Write-Host "Arquivos temporarios preservados para diagnostico: $Stage" -ForegroundColor Yellow
    }
}

Get-Item -LiteralPath $Saida | Select-Object FullName, Length, LastWriteTime

@echo off
setlocal
set "TLS_SCRIPT=%~dp0corrigir_tls12_windows.ps1"
set "POWERSHELL_EXE=%WINDIR%\System32\WindowsPowerShell\v1.0\powershell.exe"

if exist "%WINDIR%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" (
    set "POWERSHELL_EXE=%WINDIR%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
)

if not exist "%TLS_SCRIPT%" (
    echo ERRO: corrigir_tls12_windows.ps1 nao foi encontrado.
    exit /b 2
)

if /I "%TEK_TLS12_SOMENTE_VERIFICAR%"=="1" (
    "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%TLS_SCRIPT%" -SomenteVerificar
    exit /b %ERRORLEVEL%
)

"%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -Command "$argumentos = '-NoProfile -ExecutionPolicy Bypass -File ""' + $env:TLS_SCRIPT + '"" -Elevado'; try { $processo = Start-Process -FilePath $env:POWERSHELL_EXE -Verb RunAs -ArgumentList $argumentos -Wait -PassThru -ErrorAction Stop; exit $processo.ExitCode } catch { Write-Host ('ERRO: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
exit /b %ERRORLEVEL%

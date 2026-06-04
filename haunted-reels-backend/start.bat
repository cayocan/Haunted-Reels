@echo off
setlocal enabledelayedexpansion
title Haunted Reels Backend
cd /d "%~dp0"

echo.
echo  ==========================================
echo   HAUNTED REELS BACKEND ^- Setup e Launch
echo  ==========================================
echo.

:: ── 1. Verificar Node.js ──────────────────────────────────────────
where node >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERRO] Node.js nao encontrado.
    echo        Instale em: https://nodejs.org ^(versao 20+^)
    pause
    exit /b 1
)
for /f "tokens=*" %%v in ('node -v') do echo [OK] Node.js %%v encontrado.

:: ── 2. npm install ────────────────────────────────────────────────
if not exist "node_modules" (
    echo.
    echo [1/4] Instalando dependencias npm...
    call npm install
    if !errorlevel! neq 0 (
        echo [ERRO] npm install falhou.
        pause
        exit /b 1
    )
    echo [OK] Dependencias instaladas.
) else (
    echo [OK] node_modules ja existe. Pulando npm install.
)

:: ── 3. Localizar ou baixar ngrok ──────────────────────────────────
echo.
echo [2/4] Verificando ngrok...

set NGROK_CMD=
where ngrok >nul 2>&1
if %errorlevel% equ 0 (
    set NGROK_CMD=ngrok
    echo [OK] ngrok encontrado no PATH.
    goto :ngrok_found
)
if exist "%~dp0ngrok.exe" (
    set "NGROK_CMD=%~dp0ngrok.exe"
    echo [OK] ngrok.exe encontrado na pasta local.
    goto :ngrok_found
)

:: Tentar instalar via winget
where winget >nul 2>&1
if %errorlevel% equ 0 (
    echo Instalando ngrok via winget...
    winget install Ngrok.Ngrok --silent --accept-package-agreements --accept-source-agreements
    where ngrok >nul 2>&1
    if !errorlevel! equ 0 (
        set NGROK_CMD=ngrok
        echo [OK] ngrok instalado via winget.
        goto :ngrok_found
    )
)

:: Fallback: download direto via PowerShell
echo Baixando ngrok diretamente...
powershell -NoProfile -Command ^
    "Invoke-WebRequest -Uri 'https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-windows-amd64.zip' -OutFile '%~dp0ngrok.zip' -UseBasicParsing"
if not exist "%~dp0ngrok.zip" (
    echo [ERRO] Falha ao baixar ngrok. Verifique sua conexao e tente novamente.
    pause
    exit /b 1
)
powershell -NoProfile -Command ^
    "Expand-Archive -Path '%~dp0ngrok.zip' -DestinationPath '%~dp0' -Force"
del "%~dp0ngrok.zip" >nul 2>&1
if not exist "%~dp0ngrok.exe" (
    echo [ERRO] ngrok.exe nao encontrado apos extracao.
    pause
    exit /b 1
)
set "NGROK_CMD=%~dp0ngrok.exe"
echo [OK] ngrok baixado com sucesso.

:ngrok_found

:: ── 4. Authtoken ngrok ────────────────────────────────────────────
echo.
echo [3/4] Verificando authtoken ngrok...

set NGROK_TOKEN=
if exist "%~dp0.env" (
    for /f "usebackq tokens=1,* delims==" %%a in ("%~dp0.env") do (
        if /i "%%a"=="NGROK_AUTHTOKEN" set NGROK_TOKEN=%%b
    )
)

if not defined NGROK_TOKEN (
    echo.
    echo  Authtoken nao encontrado.
    echo  Crie uma conta gratuita em: https://ngrok.com
    echo  Copie seu authtoken em:     https://dashboard.ngrok.com/get-started/your-authtoken
    echo.
    set /p NGROK_TOKEN=  Cole seu NGROK_AUTHTOKEN aqui:
    if not defined NGROK_TOKEN (
        echo [ERRO] Token nao informado.
        pause
        exit /b 1
    )
    :: Salvar no .env para proximas execucoes
    echo NGROK_AUTHTOKEN=!NGROK_TOKEN!>> "%~dp0.env"
    echo [OK] Token salvo em .env
)

%NGROK_CMD% config add-authtoken %NGROK_TOKEN% >nul 2>&1
echo [OK] Authtoken configurado.

:: ── 5. Iniciar servidor em janela separada ────────────────────────
echo.
echo [4/4] Iniciando servidor Fastify na porta 3000...
start "Haunted Reels - Server" cmd /k "cd /d "%~dp0" && node src/server.js"
timeout /t 2 /nobreak >nul
echo [OK] Servidor iniciado.

:: ── 6. Iniciar tunnel ngrok ───────────────────────────────────────
echo.
echo  ==========================================
echo   Tunnel ngrok iniciando...
echo   A URL publica aparece logo abaixo.
echo   Copie a URL https:// e cole em EnvConfig.cs
echo  ==========================================
echo.
%NGROK_CMD% http 3000

:: Ao fechar o ngrok, a janela do servidor continua rodando.
echo.
echo Tunnel encerrado. Feche a janela do servidor separadamente se desejar.
pause

@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ============================================
echo  WebPos - SHOP UPDATE
echo ============================================
echo.
echo This updates code only. Shop database volume is preserved.
echo Do NOT use "docker compose down -v".
echo.

where docker >nul 2>&1
if errorlevel 1 (
  echo ERROR: Docker is not installed or not on PATH.
  exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
  echo ERROR: Docker Desktop is not running.
  exit /b 1
)

if not exist ".env" (
  echo ERROR: .env is missing.
  echo Run INSTALL-POS.bat first, or copy .env.example to .env and edit secrets.
  exit /b 1
)

where git >nul 2>&1
if not errorlevel 1 (
  echo Pulling latest code...
  git pull
  if errorlevel 1 (
    echo WARNING: git pull failed. Continuing with current local files...
  )
) else (
  echo WARNING: git not found. Updating with current local files only.
)

echo.
echo Rebuilding and restarting containers (database volume kept)...
docker compose up -d --build
if errorlevel 1 (
  echo.
  echo ERROR: docker compose failed. Fix the message above and retry.
  exit /b 1
)

echo.
echo Waiting for API health...
set /a tries=0
:wait_health
set /a tries+=1
curl -fsS http://localhost:8080/health >nul 2>&1
if not errorlevel 1 goto healthy
if %tries% GEQ 60 (
  echo WARNING: API did not report healthy yet. Check: docker compose ps
  goto done
)
timeout /t 2 /nobreak >nul
goto wait_health

:healthy
echo API is healthy. EF migrations (if any) ran on startup.

:done
echo.
echo ============================================
echo  Update complete
echo ============================================
echo  Health:  http://localhost:8080/health
echo  Master:  http://localhost:8080/master
echo ============================================
exit /b 0

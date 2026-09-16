@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ============================================
echo  WebPos - SHOP INSTALLATION
echo ============================================
echo.

where docker >nul 2>&1
if errorlevel 1 (
  echo ERROR: Docker is not installed or not on PATH.
  echo Install Docker Desktop, start it, then run this script again.
  exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
  echo ERROR: Docker Desktop is not running.
  echo Start Docker Desktop, wait until it is ready, then run this again.
  exit /b 1
)

if not exist ".env" (
  if not exist ".env.example" (
    echo ERROR: .env.example is missing. Cannot create .env.
    exit /b 1
  )
  copy /Y ".env.example" ".env" >nul
  echo Created .env from .env.example
  echo.
  echo IMPORTANT: Edit .env now and replace every CHANGE_ME value:
  echo   - POSTGRES_PASSWORD
  echo   - SECURITY_JWT_KEY  (at least 32 characters)
  echo   - PILOT_OWNER_PASSWORD / PILOT_ADMIN_PASSWORD / PIN fields
  echo.
  notepad ".env"
  echo.
  echo After saving .env, press any key to continue building...
  pause >nul
)

echo.
echo Building and starting API + Postgres (shop data volume is kept)...
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
  echo Then open http://localhost:8080/health
  goto done
)
timeout /t 2 /nobreak >nul
goto wait_health

:healthy
echo API is healthy.

:done
echo.
echo ============================================
echo  Installation complete
echo ============================================
echo  Health:  http://localhost:8080/health
echo  Master:  http://localhost:8080/master
echo  Login:   http://localhost:8080/login
echo.
echo  Do NOT run "docker compose down -v" on a live shop.
echo  That deletes the database volume.
echo ============================================
exit /b 0

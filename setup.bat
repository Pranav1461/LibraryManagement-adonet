@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================================================
echo   LibraDesk - Library Management System Setup ^& Launcher
echo ============================================================================
echo.

:: Add standard .NET and sqlcmd folders in case PATH hasn't refreshed yet
if exist "C:\Program Files\dotnet" set "PATH=C:\Program Files\dotnet;%PATH%"
if exist "C:\Program Files\SqlCmd" set "PATH=C:\Program Files\SqlCmd;%PATH%"

:: Check .NET SDK
echo [1/6] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] No .NET SDK found. Install .NET 8 SDK first:
    echo         winget install --id Microsoft.DotNet.SDK.8 --exact --accept-package-agreements --accept-source-agreements
    pause
    exit /b 1
)
for /f "delims=" %%v in ('dotnet --version') do set "DOTNET_VER=%%v"
echo       Found .NET SDK %DOTNET_VER%

:: Check sqlcmd
echo [2/6] Checking sqlcmd...
where sqlcmd >nul 2>&1
if errorlevel 1 (
    echo [ERROR] sqlcmd was not found in PATH. Install it first:
    echo         winget install --id Microsoft.Sqlcmd --exact --accept-package-agreements --accept-source-agreements
    pause
    exit /b 1
)
echo       Found sqlcmd.

:: Find a working SQL Server instance
echo [3/6] Checking SQL Server connectivity...
set "SQL_SERVER="

sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -Q "SELECT 1" >nul 2>&1
if not errorlevel 1 set "SQL_SERVER=lpc:.\SQLEXPRESS"

if not defined SQL_SERVER (
    sqlcmd -S ".\SQLEXPRESS" -E -C -Q "SELECT 1" >nul 2>&1
    if not errorlevel 1 set "SQL_SERVER=.\SQLEXPRESS"
)

if not defined SQL_SERVER (
    sqlcmd -S "localhost" -E -C -Q "SELECT 1" >nul 2>&1
    if not errorlevel 1 set "SQL_SERVER=localhost"
)

if not defined SQL_SERVER (
    echo       Attempting to start SQL Server ^(SQLEXPRESS^)...
    net start "MSSQL$SQLEXPRESS" >nul 2>&1
    sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -Q "SELECT 1" >nul 2>&1
    if not errorlevel 1 set "SQL_SERVER=lpc:.\SQLEXPRESS"
)

if not defined SQL_SERVER (
    echo [ERROR] Could not connect to SQL Server ^(SQLEXPRESS or localhost^).
    echo         Check README.md Section 13 if SQL Server 2022 Express hit 0x851A001A on Windows 11.
    pause
    exit /b 1
)
echo       Connected to SQL Server: %SQL_SERVER%

:: Create and seed LibraryManagementDB on first run (or when --reset-db is passed)
echo [4/6] Checking LibraryManagementDB...
set "RESET_DB=0"
if /i "%~1"=="--reset-db" set "RESET_DB=1"

set "DB_EXISTS=0"
for /f "usebackq delims=" %%r in (`sqlcmd -S "%SQL_SERVER%" -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'LibraryManagementDB') IS NOT NULL AND OBJECT_ID(N'LibraryManagementDB.dbo.Books', N'U') IS NOT NULL THEN 1 ELSE 0 END;" 2^>nul`) do (
    if "%%r"=="1" set "DB_EXISTS=1"
)

if "%RESET_DB%"=="1" set "DB_EXISTS=0"

if "%DB_EXISTS%"=="1" (
    echo       LibraryManagementDB already initialized. ^(Run "setup.bat --reset-db" to re-seed.^)
) else (
    echo       Running SQL setup scripts...
    sqlcmd -S "%SQL_SERVER%" -E -C -b -i "Database\01_CreateDatabase.sql" || goto :sql_fail
    sqlcmd -S "%SQL_SERVER%" -E -C -b -i "Database\02_CreateTables.sql" || goto :sql_fail
    sqlcmd -S "%SQL_SERVER%" -E -C -b -i "Database\03_ConstraintsAndIndexes.sql" || goto :sql_fail
    sqlcmd -S "%SQL_SERVER%" -E -C -b -i "Database\04_SampleData.sql" || goto :sql_fail
    echo       Database created and sample data seeded.
)

:: Restore, build, and run tests
echo [5/6] Building and testing .NET solution...
dotnet restore LibraryManagement.sln --nologo || goto :build_fail
dotnet build LibraryManagement.sln --no-restore --nologo || goto :build_fail
set "LMS_RUN_DB_TESTS=true"
dotnet test LibraryManagement.sln --no-build --nologo || goto :build_fail

:: Start API (5056) and frontend (5506) in separate windows
echo [6/6] Starting Backend API and Frontend UI...

netstat -ano | findstr ":5056 " | findstr "LISTENING" >nul 2>&1
if errorlevel 1 (
    start "LibraDesk API (http://localhost:5056)" cmd /k "cd /d "%~dp0" && dotnet run --project LibraryManagement.API\LibraryManagement.API.csproj --launch-profile http --no-build"
) else (
    echo       Backend API already listening on port 5056.
)

netstat -ano | findstr ":5506 " | findstr "LISTENING" >nul 2>&1
if errorlevel 1 (
    where python >nul 2>&1
    if not errorlevel 1 (
        start "LibraDesk Web (http://localhost:5506)" cmd /k "python -m http.server 5506 --directory "%~dp0LibraryManagement.Web""
    ) else (
        where npx >nul 2>&1
        if not errorlevel 1 (
            start "LibraDesk Web (http://localhost:5506)" cmd /k "npx --yes serve -l 5506 "%~dp0LibraryManagement.Web""
        ) else (
            echo [WARN] Neither Python nor Node npx was found to host the frontend on port 5506.
        )
    )
) else (
    echo       Frontend server already listening on port 5506.
)

timeout /t 3 /nobreak >nul
start "" "http://localhost:5506/index.html"

echo.
echo ============================================================================
echo   LibraDesk is running!
echo   - Frontend UI : http://localhost:5506/index.html
echo   - Backend API : http://localhost:5056/api
echo   - Swagger UI  : http://localhost:5056/swagger
echo ============================================================================
echo.
pause
exit /b 0

:sql_fail
echo.
echo [ERROR] Database initialization failed.
pause
exit /b 1

:build_fail
echo.
echo [ERROR] .NET build or test failed.
pause
exit /b 1

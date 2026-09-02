@echo off
setlocal
set "ROOT=%~dp0"

echo ============================================
echo  StoryForge - starting backend and frontend
echo ============================================

echo.
echo [1/3] Starting backend (ASP.NET Core) on http://localhost:5276 ...
start "StoryForge API" cmd /k "cd /d "%ROOT%StoryForge.Api" && dotnet run --urls http://localhost:5276"

echo [2/3] Starting frontend (Angular) on http://localhost:4200 ...
start "StoryForge Web" cmd /k "cd /d "%ROOT%storyforge-web" && npm start"

echo [3/3] Waiting for the frontend dev server to come up...
timeout /t 12 /nobreak >nul

start "" "http://localhost:4200"

echo.
echo StoryForge is starting in two separate windows:
echo   - "StoryForge API"  (backend logs)
echo   - "StoryForge Web"  (frontend logs)
echo Close this window any time; the servers keep running until you run stop.bat.
endlocal

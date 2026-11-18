@echo off
chcp 65001 >nul
cd /d "%~dp0Builds\GhostMaze_Test"
echo Đang mở 3 cửa sổ GhostMaze (854x480) để test Multiplayer...
start "" "GhostMaze.exe" -screen-fullscreen 0 -screen-width 854 -screen-height 480
timeout /t 1 /nobreak >nul
start "" "GhostMaze.exe" -screen-fullscreen 0 -screen-width 854 -screen-height 480
timeout /t 1 /nobreak >nul
start "" "GhostMaze.exe" -screen-fullscreen 0 -screen-width 854 -screen-height 480

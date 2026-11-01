@echo off
chcp 65001 >nul
cd /d "%~dp0"
if not exist build mkdir build
"C:\Users\User\Desktop\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . --export-release "Windows Desktop" "build\Adventurer.exe"
echo.
echo [copy game data folders next to exe, please wait...]
robocopy rmz build\rmz /E /XF *.bak* /NFL /NDL /NJH /NJS
robocopy PortAssets build\PortAssets /E /XF *.bak* /NFL /NDL /NJH /NJS
echo.
echo ===== done =====
if exist build\Adventurer.exe (echo OK: build\Adventurer.exe) else (echo FAILED, see messages above)
pause

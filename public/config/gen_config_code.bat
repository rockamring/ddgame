@echo off
cd /d "%~dp0"
echo ========================================
echo  Generate Config Code (Excel -^> C#)
echo ========================================

for /r %%f in (*.xlsx) do (
    echo [%%~nxf]
    python ..\tools\codegen\config_codegen.py --input "%%f" --output-dir ..\..\client\GameFramework\Data\Generated
    echo.
)

echo Done.
pause

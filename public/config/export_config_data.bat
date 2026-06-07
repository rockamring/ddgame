@echo off
cd /d "%~dp0"
echo ========================================
echo  Export Config Data (Excel -> .cfgb)
echo ========================================

for /r %%f in (*.xlsx) do (
    echo [%%~nxf]
    python ..\tools\codegen\config_exporter.py --input "%%f" --output-dir ..\..\config --target client
    echo.
)

echo Done.
pause

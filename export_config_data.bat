@echo off
echo ========================================
echo  Export Config Data (Excel -> .cfgb)
echo ========================================

for %%f in (public\configs\*.xlsx) do (
    echo [%%~nxf]
    python public\tools\codegen\config_exporter.py --input "%%f" --output-dir config --target client
    echo.
)

echo Done.
pause

@echo off
echo ========================================
echo  Generate Config Code (Excel -> C#)
echo ========================================

for %%f in (public\configs\*.xlsx) do (
    echo [%%~nxf]
    python public\tools\codegen\config_codegen.py --input "%%f" --output-dir client\GameFramework\Data\Generated
    echo.
)

echo Done.
pause

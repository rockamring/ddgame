@echo off
cd /d "%~dp0"
echo ========================================
echo  Generate Proto Code (.proto -^> C#)
echo ========================================

python ..\tools\codegen\proto_codegen.py ^
    --proto-dir . ^
    --output-dir ..\..\client\GameFramework\Network\Protobuf ^
    --handler-dir ..\..\client\GameLogic\Network\Handlers

echo.
echo Done.
pause

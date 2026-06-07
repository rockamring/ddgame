@echo off
echo ========================================
echo  Generate Proto Code (.proto -> C#)
echo ========================================

python public\tools\codegen\proto_codegen.py ^
    --proto-dir public\proto ^
    --output-dir client\GameFramework\Network\Protobuf ^
    --handler-dir client\GameLogic\Network\Handlers

echo.
echo Done.
pause

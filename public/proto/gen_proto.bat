@echo off
cd /d "%~dp0"
echo ========================================
echo  Generate Proto Code (.proto -^> C#)
echo ========================================

python ..\tools\codegen\proto_codegen.py ^
    --proto-dir . ^
    --output-dir ..\..\client\UnityClient\Assets\Scripts\Generated\Network\Protobuf ^
    --handler-dir ..\..\client\UnityClient\Assets\Scripts\Generated\Network\Handlers

echo.
echo Done.
pause

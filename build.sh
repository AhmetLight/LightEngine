#!/usr/bin/env bash
# ==============================================================================
#   Light Engine - Cross-Platform Build & Test Automation Script (Linux / macOS)
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== [LightEngine] Solution derleniyor (Release) ==="
dotnet build LightEngine.slnx -c Release

echo "=== [LightEngine] 9 Asamali Otomasyon Test Paketi calistiriliyor ==="
dotnet run --project LightEngine.Tests -c Release

echo ""
echo "=============================================================================="
echo "  [BASARILI] Light Engine Linux/macOS ortaminda derlendi ve testler gecti!"
echo "=============================================================================="

if [ "$1" == "publish" ] || [ "$1" == "dist" ] || [ "$1" == "all" ]; then
    echo ""
    echo "=== [LightEngine] Standalone Linux paketleri hazirlaniyor... ==="
    dotnet publish LightEngine.Editor/LightEngine.Editor.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/linux
    dotnet publish Game/Game.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/linux
    
    mkdir -p dist/linux
    cp -f LightEngine.Editor/bin/Release/net9.0/runtimes/linux-x64/native/* dist/linux/ 2>/dev/null || true
    cp -f Icon.png dist/linux/ 2>/dev/null || true
    chmod +x dist/linux/run_editor.sh dist/linux/run_game.sh dist/linux/LightEngineEditor dist/linux/Game 2>/dev/null || true
    echo "=== [LightEngine] Linux yayini hazir: dist/linux ==="
fi

#!/bin/bash
# Build and publish DownloadSorter CLI for Windows
# Run from WSL2, outputs to ./publish/

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
PUBLISH_DIR="$PROJECT_ROOT/publish"
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"

echo "================================"
echo "DownloadSorter Build Script"
echo "================================"
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

cd "$PROJECT_ROOT"

# Build solution
echo "Building solution..."
"$DOTNET" build -c Release --nologo -v q

# Publish CLI
echo "Publishing CLI..."
"$DOTNET" publish src/DownloadSorter.Cli/DownloadSorter.Cli.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o ./publish \
    --nologo -v q

# List output
echo ""
echo "================================"
echo "Build complete! Output:"
echo "================================"
ls -lh "$PUBLISH_DIR"/*.exe 2>/dev/null || echo "No executables found"

echo ""
echo "Files are in: $PUBLISH_DIR"
echo ""
echo "To install:"
echo "  1. Copy sorter.exe to a folder in your PATH"
echo "  2. Run 'sorter init' to configure"
echo "  3. Run 'sorter sort --loop' for continuous monitoring"
echo "  4. Use 'sorter --help' for all commands"

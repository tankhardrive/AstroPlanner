#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
PUBLISH_DIR="$SCRIPT_DIR/bin/Release/net10.0/linux-x64/publish"
OUTPUT="$REPO_DIR/AstroPlanner-x86_64.AppImage"
TOOL="$REPO_DIR/appimagetool-x86_64.AppImage"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "ERROR: Publish output not found at $PUBLISH_DIR"
  echo "Run: dotnet publish -c Release -r linux-x64 --self-contained true"
  exit 1
fi

echo "Building AppImage from $PUBLISH_DIR..."

rm -rf "$REPO_DIR/AppDir"
mkdir -p "$REPO_DIR/AppDir/usr/bin"
mkdir -p "$REPO_DIR/AppDir/usr/share/applications"
mkdir -p "$REPO_DIR/AppDir/usr/share/icons/hicolor/256x256/apps"

cp -r "$PUBLISH_DIR"/. "$REPO_DIR/AppDir/usr/bin/"

cat > "$REPO_DIR/AppDir/AstroPlanner.desktop" << 'EOF'
[Desktop Entry]
Name=AstroPlanner
Exec=AstroPlanner
Icon=astroplanner
Type=Application
Categories=Science;
EOF

cp "$SCRIPT_DIR/Assets/astroplanner.png" "$REPO_DIR/AppDir/usr/share/icons/hicolor/256x256/apps/astroplanner.png"
cp "$SCRIPT_DIR/Assets/astroplanner.png" "$REPO_DIR/AppDir/astroplanner.png"
ln -sf usr/bin/AstroPlanner "$REPO_DIR/AppDir/AppRun"

if [ ! -f "$TOOL" ]; then
  echo "Downloading appimagetool..."
  wget -q https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage -O "$TOOL"
  chmod +x "$TOOL"
fi

ARCH=x86_64 "$TOOL" "$REPO_DIR/AppDir" "$OUTPUT" 2>&1

echo ""
echo "Done: $OUTPUT ($(du -h "$OUTPUT" | cut -f1))"
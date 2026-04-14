#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
APPIMAGE="$REPO_DIR/AstroPlanner-x86_64.AppImage"

if [ ! -f "$APPIMAGE" ]; then
  echo "ERROR: AppImage not found at $APPIMAGE"
  echo "Run build-appimage.sh first."
  exit 1
fi

echo "Installing AstroPlanner..."

mkdir -p ~/.local/bin
cp "$APPIMAGE" ~/.local/bin/AstroPlanner.AppImage
chmod +x ~/.local/bin/AstroPlanner.AppImage

mkdir -p ~/.local/share/icons/hicolor/256x256/apps
cp "$SCRIPT_DIR/Assets/astroplanner.png" ~/.local/share/icons/hicolor/256x256/apps/astroplanner.png

mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/astroplanner.desktop << 'EOF'
[Desktop Entry]
Name=AstroPlanner
Exec=/home/nhartmann/.local/bin/AstroPlanner.AppImage
Icon=astroplanner
Type=Application
Categories=Science;
Comment=Horizon-aware deep sky object planner
EOF

update-desktop-database ~/.local/share/applications 2>/dev/null || true

echo "Done. AstroPlanner is installed and should appear in your app launcher."

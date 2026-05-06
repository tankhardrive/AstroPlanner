#!/usr/bin/env bash
set -euo pipefail

APP_NAME="AstroPlanner"
APP_VERSION="1.0"
BUNDLE_ID="com.nhartmann.astroplanner"
MIN_MACOS="12.0"

PUBLISH_DIR="AstroPlanner/bin/Release/net10.0/osx-x64/publish"
SRC_ICON="AstroPlanner/Assets/astroplanner.png"
APP_BUNDLE="${APP_NAME}.app"
OUT_ZIP="${APP_NAME}-${APP_VERSION}-macos.tar.gz"

# ── Clean previous build ──────────────────────────────────────────────────────
rm -rf "$APP_BUNDLE" "$OUT_ZIP"

# ── Bundle structure ──────────────────────────────────────────────────────────
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# ── Binaries ──────────────────────────────────────────────────────────────────
cp "$PUBLISH_DIR/AstroPlanner"             "$APP_BUNDLE/Contents/MacOS/"
cp "$PUBLISH_DIR/libAvaloniaNative.dylib"  "$APP_BUNDLE/Contents/MacOS/"
cp "$PUBLISH_DIR/libHarfBuzzSharp.dylib"   "$APP_BUNDLE/Contents/MacOS/"
cp "$PUBLISH_DIR/libSkiaSharp.dylib"       "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/AstroPlanner"

# ── Icon ──────────────────────────────────────────────────────────────────────
ICON_FILE="AstroPlanner.icns"
if command -v png2icns &>/dev/null; then
    echo "Creating .icns from PNG..."
    TMP_ICONS=$(mktemp -d)
    for SIZE in 16 32 48 128 256 512; do
        magick "$SRC_ICON" -resize "${SIZE}x${SIZE}!" "$TMP_ICONS/icon_${SIZE}.png"
    done
    png2icns "$TMP_ICONS/$ICON_FILE" \
        "$TMP_ICONS/icon_16.png" "$TMP_ICONS/icon_32.png" "$TMP_ICONS/icon_48.png" \
        "$TMP_ICONS/icon_128.png" "$TMP_ICONS/icon_256.png" "$TMP_ICONS/icon_512.png"
    cp "$TMP_ICONS/$ICON_FILE" "$APP_BUNDLE/Contents/Resources/$ICON_FILE"
    rm -rf "$TMP_ICONS"
    ICON_REF="AstroPlanner"
else
    echo "Warning: png2icns not found (install with: sudo pacman -S libicns)"
    echo "Falling back to PNG icon — app will work but Finder icon may be low-res."
    cp "$SRC_ICON" "$APP_BUNDLE/Contents/Resources/AstroPlanner.png"
    ICON_REF="AstroPlanner"
fi

# ── Info.plist ────────────────────────────────────────────────────────────────
cat > "$APP_BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>          <string>AstroPlanner</string>
    <key>CFBundleIdentifier</key>          <string>${BUNDLE_ID}</string>
    <key>CFBundleName</key>                <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>         <string>${APP_NAME}</string>
    <key>CFBundleVersion</key>             <string>${APP_VERSION}</string>
    <key>CFBundleShortVersionString</key>  <string>${APP_VERSION}</string>
    <key>CFBundlePackageType</key>         <string>APPL</string>
    <key>CFBundleIconFile</key>            <string>${ICON_REF}</string>
    <key>NSPrincipalClass</key>            <string>NSApplication</string>
    <key>NSHighResolutionCapable</key>     <true/>
    <key>LSMinimumSystemVersion</key>      <string>${MIN_MACOS}</string>
</dict>
</plist>
EOF

# ── Zip ───────────────────────────────────────────────────────────────────────
tar -czf "$OUT_ZIP" "$APP_BUNDLE"
rm -rf "$APP_BUNDLE"

echo ""
echo "Done: $OUT_ZIP"
echo ""
echo "Note: unsigned app — macOS will quarantine it on first download."
echo "Users should right-click → Open, or run:"
echo "  xattr -dr com.apple.quarantine ${APP_NAME}.app"

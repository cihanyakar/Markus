#!/usr/bin/env bash
# Build a fully self-contained, standalone macOS .app for Markus.
#
# Output:
#   dist/Markus.app                       runnable bundle
#   dist/Markus-v<version>-osx-<arch>.zip ditto archive ready to share
#   dist/...sha256                        checksum file
#
# What "standalone" means here: NativeAOT compiles the entire .NET runtime
# plus every managed assembly into one native Mach-O. No external dotnet
# install is required on the target Mac — only the bundled SkiaSharp /
# HarfBuzzSharp / AvaloniaNative dylibs need to load.
#
# Signing: every Mach-O (Markus + each dylib) gets ad-hoc signed WITHOUT
# the hardened runtime so library validation accepts the ad-hoc-signed
# dylibs sitting next to the executable. With hardened runtime enabled,
# unsigned/foreign-team-id dylibs are refused and SkiaPlatform's static
# initializer throws before the first window appears.
#
# Apple Developer ID notarization is the only path to "double-click and
# go" on every Mac. Until that's set up, users transferring the .app via
# Safari / AirDrop / Slack still need either:
#     xattr -cr /path/to/Markus.app
# or a one-time right-click → Open from Finder to clear quarantine.

set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
RID=${RID:-osx-arm64}
CONFIG=${CONFIG:-Release}

# Honor whichever dotnet the caller already has on PATH (~/.dotnet or system).
# Required runtimes: .NET 11 preview (for build), .NET 10 (for csharpier tool).
export DOTNET_ROOT=${DOTNET_ROOT:-$HOME/.dotnet}
export PATH="$DOTNET_ROOT:$PATH"
export HUSKY=0

VERSION=$(grep -oE '"\.": "[^"]+"' "$ROOT/.release-please-manifest.json" \
              | head -1 \
              | sed -E 's/.*"([^"]+)"$/\1/')
[ -z "$VERSION" ] && VERSION=dev

PUBLISH_DIR="$ROOT/dist/$RID-aot"
if [ "${SKIP_PUBLISH:-0}" = "1" ] && [ -x "$PUBLISH_DIR/Markus" ]; then
    echo "==> Reusing existing publish at $PUBLISH_DIR (SKIP_PUBLISH=1)"
else
    echo "==> Publishing $RID NativeAOT ($CONFIG, v$VERSION)"
    rm -rf "$PUBLISH_DIR"
    dotnet publish "$ROOT/src/Markus/Markus.csproj" \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -p:PublishAot=true \
        -p:InvariantGlobalization=true \
        -p:EventSourceSupport=false \
        -p:DebuggerSupport=false \
        -p:UseSystemResourceKeys=true \
        -p:DebugType=embedded \
        -p:NoWarn=IL2104 \
        -p:IlcOptimizationPreference=Speed \
        -o "$PUBLISH_DIR" \
        | tail -3
fi

echo "==> Assembling Markus.app"
APP_DIR="$ROOT/dist/Markus.app"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

cp "$PUBLISH_DIR/Markus" "$APP_DIR/Contents/MacOS/"
cp "$PUBLISH_DIR"/*.dylib "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/Markus"
cp "$ROOT/src/Markus/Assets/markus.png" "$APP_DIR/Contents/Resources/"
cp "$ROOT/src/Markus/Build/Markus.icns" "$APP_DIR/Contents/Resources/"

sed -e "s/__VERSION__/$VERSION/g" \
    -e "s/__YEAR__/$(date -u +%Y)/g" \
    "$ROOT/src/Markus/Build/Info.plist.template" \
    > "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string Markus.icns" \
    "$APP_DIR/Contents/Info.plist" >/dev/null 2>&1 || true

echo "==> Ad-hoc signing (no hardened runtime)"
# Sign every Mach-O inside the bundle individually so each dylib's signature
# matches Markus's expectations under library validation.
for lib in "$APP_DIR/Contents/MacOS/"*.dylib; do
    codesign --sign - --force "$lib"
done
# Final pass on the bundle root seals Info.plist + Resources.
codesign --sign - --deep --force "$APP_DIR"
codesign --verify --verbose=2 "$APP_DIR" 2>&1 | sed 's/^/    /'

echo "==> Registering with LaunchServices"
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister \
    -f -R "$APP_DIR" >/dev/null

echo "==> Building DMG"
cd "$ROOT/dist"
ARCHIVE="Markus-v$VERSION-$RID.dmg"
STAGING="dmg-staging"
rm -rf "$STAGING" "$ARCHIVE" "$ARCHIVE.sha256"
mkdir "$STAGING"
cp -R "$(basename "$APP_DIR")" "$STAGING/"
# /Applications symlink turns the mounted volume into a "drag here" view.
ln -s /Applications "$STAGING/Applications"
hdiutil create \
    -volname "Markus" \
    -srcfolder "$STAGING" \
    -ov \
    -format UDZO \
    -fs HFS+ \
    "$ARCHIVE" \
    | tail -3
codesign --sign - --force "$ARCHIVE"
rm -rf "$STAGING"
shasum -a 256 "$ARCHIVE" > "$ARCHIVE.sha256"

echo
echo "Done."
echo "    $(du -sh "$APP_DIR")"
echo "    $(du -sh "$ROOT/dist/$ARCHIVE")"
echo
echo "Distribute the dmg; first-time users on other Macs may need to run:"
echo "    xattr -cr /Applications/Markus.app"
echo "after dragging into Applications, or right-click → Open in Finder."

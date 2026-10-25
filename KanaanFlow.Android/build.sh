#!/usr/bin/env bash
# Builds KanaanFlow.apk without Gradle, using the Debian/Ubuntu Android tools:
#   sudo apt install android-sdk android-sdk-platform-23 dalvik-exchange apksigner zipalign openjdk-17-jdk
# Signing: set KF_KEYSTORE (and KF_KEYSTORE_PASS) to the release keystore. Updates must use the same key,
# or phones refuse to install them over the existing app.
set -euo pipefail
cd "$(dirname "$0")"

SDK=${ANDROID_HOME:-/usr/lib/android-sdk}
PLATFORM="$SDK/platforms/android-23/android.jar"
KEYSTORE=${KF_KEYSTORE:-kanaanflow-release.jks}
KEYPASS=${KF_KEYSTORE_PASS:-}
OUT=build

rm -rf "$OUT" && mkdir -p "$OUT/gen" "$OUT/classes" "$OUT/res"

aapt2 compile --dir res -o "$OUT/res/res.zip"
aapt2 link -I "$PLATFORM" --manifest AndroidManifest.xml --java "$OUT/gen" -o "$OUT/unsigned.apk" "$OUT/res/res.zip"

javac --release 8 -Xlint:-options -classpath "$PLATFORM" -d "$OUT/classes" \
    $(find src "$OUT/gen" -name '*.java')
dalvik-exchange --dex --min-sdk-version=23 --output="$OUT/classes.dex" "$OUT/classes"

(cd "$OUT" && zip -q -j unsigned.apk classes.dex)
zipalign -f -p 4 "$OUT/unsigned.apk" "$OUT/aligned.apk"

if [ ! -f "$KEYSTORE" ]; then
    echo "No keystore at $KEYSTORE. Create one once (and keep it safe) with:" >&2
    echo "  keytool -genkeypair -keystore $KEYSTORE -alias kanaanflow -keyalg RSA -keysize 4096 -validity 36500" >&2
    exit 1
fi

apksigner sign --ks "$KEYSTORE" --ks-key-alias kanaanflow ${KEYPASS:+--ks-pass "pass:$KEYPASS"} \
    --out KanaanFlow.apk "$OUT/aligned.apk"
apksigner verify --print-certs KanaanFlow.apk | head -3
echo "Built $(pwd)/KanaanFlow.apk"

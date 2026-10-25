# KanaanFlow for Android

A small native app that opens KanaanFlow (`https://flow.mkanaan.de`) full-screen. The app itself is the web app:
offline storage, log-in and sync all work as in the browser, and deploys to the Pi update it without a new APK.
The native part only adds what a WebView can't do on its own:

- **Excel and backup files**: handed to Android's share sheet (save to Files or Drive, send by WhatsApp…).
- **PDF / print**: goes through Android's print service, which also offers *Save as PDF*.

`kf.js` uses these through `window.KanaanFlowAndroid` when it runs inside the app.

## Build

No Gradle or Android Studio needed:

```bash
sudo apt install android-sdk android-sdk-platform-23 dalvik-exchange apksigner zipalign openjdk-17-jdk
KF_KEYSTORE=/path/to/kanaanflow-release.jks KF_KEYSTORE_PASS=... ./build.sh
```

Every new version must be signed with the **same keystore**, or phones refuse to install it over the old app
(uninstalling first would delete the data that hasn't synced). Keep the keystore and its password safe, outside
the repository. To release a new version, raise `versionCode` (and `versionName`) in `AndroidManifest.xml`.

To point the app at another server, change `APP_URL` in `MainActivity.java`.

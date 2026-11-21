# EV-10 – Android build of 12/11/2026

Built on a team PC (Windows 11, Unity 6000.6.2f1 with the Android, SDK/NDK and OpenJDK modules) on 12/11/2026 at 21:06 (UTC+7)
from main @ 5963501 with `BuildPipeline.BuildPlayer` in batch mode. Project settings were not changed for the build.

| Item | Value |
|---|---|
| Output | PickleVerse-5963501.apk, 190.5 MB (199,746,501 bytes) |
| SHA-256 | 6DDA094A151188C033355C7AF372DC98BC2084A794BAE9443D85FCFEF9F60B45 |
| Scripting backend / ABI | IL2CPP, arm64-v8a |
| minSdkVersion / targetSdkVersion | 26 / 36 |
| Package and label | com.UnityTechnologies.com.unity.template.urpblank, "My project" |
| versionName / versionCode | server-82f6a7a46b16 / 1 |
| Signing | Android debug certificate; `apksigner verify` succeeded |
| Scenes | 15 (Login first) |
| Unity build result | Succeeded |

Manifest read from the APK with `aapt2 dump`:

- permissions: INTERNET, ACCESS_NETWORK_STATE, VIBRATE;
- `android:usesCleartextTraffic="false"`. The value comes from `Assets/Plugins/Android/AndroidManifest.xml`, which the App UI
  package (a dependency of com.unity.behavior) writes during the build, and it overrides the player setting "Allow downloads over
  HTTP: Always allowed".

Findings:

- The APK has not been installed on a device, so no run-time result exists yet (TC-N03 and TC-N10 stay pending).
- With cleartext traffic forbidden and `baseUrl: http://tad-min.io.vn:8080` in BackendApiSettings, API calls from an Android phone
  are expected to be refused (defect D-21). A second APK built with the flag set to true is kept for the device test.
- The package name, the label and the version name are still template or left-over values.
- Build notes for this PC (16 GB RAM): IL2CPP needs `BEE_BUILD_THREADS=4` and the Gradle cache on a short path.

Update of 13/11/2026: commit be556a1 changed the client address to `https://tad-min.io.vn` (and the player setting to allow HTTP
only in development builds), which fixes D-21 in the source. The APK described here was built before that commit, so it cannot
sign in; a new APK has to be built from main @ d9200cc or later for the device test.

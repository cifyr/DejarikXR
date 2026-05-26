# DejarikXR

## What this is

An AR build of **Dejarik** (Star Wars holochess) for **XREAL Air 2 Ultra** glasses running on the
**Beam Pro**. The holochess board is anchored in physical space and viewable/walkable from any angle
via 6DoF tracking. Personal, single-user, offline-first.

This project was scaffolded from a proven XREAL runtime app (XrealARApp): the headless build
pipeline, package set, configured ProjectSettings, vendored SDK, adb tooling, hand tracking,
persistent spatial anchoring, and a glTF runtime loader are all carried over and known-good. The
dragon scene-player app logic was stripped — Dejarik game logic gets built on this base.

## Stack

- Glasses / host: **Air 2 Ultra** (onboard 6DoF SLAM) + **Beam Pro** (Snapdragon 6 Gen 1, Adreno 710
  — a budget SoC; budget for 60 fps, not AAA).
- Engine: **Unity 6000.0 LTS** (6000.0.75f1). IL2CPP, ARM64, **OpenGL ES3**, min API 29, Built-in
  render pipeline (NOT URP).
- XR: **XREAL SDK 3.1.0** (vendored `com.xreal.xr` tarball) + **AR Foundation 6.0.7** (native anchor
  persistence) + **XR Interaction Toolkit 3.0.11** + **XR Hands 1.7.3**. Tracking MODE_6DOF.
- Render: HDR OFF (HDR breaks the XREAL composition layer's alpha-blend). Camera clears to black =
  transparent on the see-through optics.
- Asset bridge: **glTF 2.0 (.glb)**; runtime load via **glTFast** (`com.unity.cloud.gltfast`).
- Bundle id: `com.cadenwarren.dejarik` (distinct from the web Dejarik and from XrealARApp so all
  coexist on-device). Dev host: macOS; deploy: sideload via `adb`.

## Commands

```
scripts/check-device.sh                                   # Beam Pro reachable over adb
scripts/connect-wireless.sh <ip>                          # untether (frees the glasses USB-C port)
scripts/build-apk.sh                                      # headless build (REQUIRES -buildTarget Android)
scripts/install-apk.sh unity/build/DejarikXR.apk          # sideload (use adb install -r, NEVER uninstall)
scripts/push-scene.sh <scene.glb>                         # drop a glb on-device, no rebuild
scripts/logcat.sh                                         # tail filtered device logs
```

To change the scene rig, re-run the headless setup method, then build:
```
Unity -batchmode -nographics -buildTarget Android -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealXRSetup.BuildCubeScene -quit
```
(The C# namespace is still `XrealAR.*` from the source project — rename later if desired, but the
build scripts reference it, so update them together.)

## Project structure

- `unity/Assets/Scripts/` — reusable helpers kept from the source app:
  `RuntimeSceneLoader` (glb load), `SceneCatalog` (list on-device glb), `PlacementStore`
  (per-glb pos/rot/scale + anchor guid), `AnchorPlacementController` (PIN/restore spatial anchors),
  `ScenePlacementStore`. Dejarik game scripts go here.
- `unity/Assets/Editor/` — headless pipeline: `XrealXRSetup` (scene + XR enable + shaders + TMP),
  `XrealBuild` (player settings + APK), `XrealManifestPatch` (XREAL manifest meta-data).
- `unity/vendor/com.xreal.xr.tar.gz` — vendored SDK (gitignored; ~248MB; re-download from
  developer.xreal.com if missing).
- `scripts/` — adb/device workflow helpers.
- `blender/` — headless glTF export pipeline.
- `docs/SETUP.md`, `docs/BLENDER_PIPELINE.md` — carried over from the source project as reference;
  adapt for Dejarik. (See XrealARApp/docs/HOW_IT_WORKS.md for the lessons-learned writeup.)

## Project-specific rules

(Extends — does not restate — my global ~/.claude/CLAUDE.md.)

- **AR behavior can't be verified from the Mac.** Anything touching tracking, anchoring, or
  on-glasses rendering is verified only on the Beam Pro + Ultra. Build -> `adb install -r` -> test in
  glasses (Mac disconnected; can't connect both at once, no WiFi) -> reconnect Mac -> read logcat.
- **`adb install -r`, never `adb uninstall`** — uninstall wipes any glb pushed to
  `/sdcard/Android/data/com.cadenwarren.dejarik/files/scenes/`.
- **`-buildTarget Android` is mandatory** on every headless Unity invocation, or the build aborts
  with "script class layout is incompatible between editor and player".
- **Don't hallucinate XREAL/glTFast/AR Foundation signatures.** Verify against docs.xreal.com, the
  bundled SDK samples (`unity/Assets/Samples/XREAL/`), or package docs. Unverified signatures carry a
  `VERIFY` comment.
- **Performance is the binding constraint** (Adreno 710): budget for 60 fps from the start.
- **Offline-first**; no runtime cloud deps without asking.
- glTF carries baked PBR only; procedural Blender materials and lights/cameras don't travel.

## Current focus

Fresh scaffold. Definition of done for the first milestone: build + install + launch as a 6DoF MR app
showing the placeholder cube world-locked, then replace it with the Dejarik board.

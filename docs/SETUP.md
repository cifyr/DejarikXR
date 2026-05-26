# XrealAR setup & phase runbook

Target stack: **XREAL Air 2 Ultra** glasses + **Beam Pro** host, **Unity 6000.0 LTS**, **XREAL SDK 3.1.0**
(Unity XR Plug-in) + **AR Foundation 6** + **XR Interaction Toolkit**, scenes authored in **Blender** and
shipped as **glTF 2.0 (.glb)** loaded at runtime via **glTFast**.

Legend: 🤖 = automatable / already done in repo. 🧑 = needs you (GUI, a login, or the physical hardware).

---

## Phase 0 — Toolchain

- 🤖 `adb` installed via Homebrew (`android-platform-tools`). Verify: `scripts/check-device.sh`.
- 🧑 **Unity Hub + Unity 6000.0 LTS** with **Android Build Support** (incl. Android SDK & NDK + OpenJDK).
  Hub installs via Homebrew (`brew install --cask unity-hub`) but the editor + free Personal license
  require signing into a Unity account in the Hub GUI.
- 🧑 **Blender** (`brew install --cask blender`) — for authoring/exporting scenes. Headless export script
  is ready at `blender/export_glb.py`.
- 🧑 **Beam Pro developer mode**: open **MyGlasses**, tap the glasses icon (top-left) 10×; enable USB
  debugging under Settings → Developer Options. Then plug the Beam Pro into the Mac and accept the prompt.

**DoD:** `scripts/check-device.sh` prints an authorized device.

## Phase 1 — HelloMR baseline (no Blender yet)

1. 🧑 Create the Unity project (Unity 6000.0 LTS, **3D URP** template) under `unity/` in this repo
   (or create elsewhere and copy `unity/Assets/Scripts/` in). In the URP asset, **disable HDR** — HDR's
   R11G11B10 textures break alpha-blend on the XREAL composition layer.
2. 🧑 Download **XREAL SDK 3.1.0** (`com.xreal.xr.tar.gz`) from developer.xreal.com (login required).
3. 🧑 Package Manager → Add package from tarball → the XREAL SDK; add **AR Foundation 6**,
   **XR Interaction Toolkit**, **XR Plug-in Management**. Import the SDK's Interaction Basics + AR Features samples.
4. 🧑 Player Settings: Min API **29**, **OpenGL ES3** only (remove Vulkan), **IL2CPP**, **ARM64**, portrait,
   VSync off. XR Plug-in Management → Android → enable **XREAL**; XREAL settings → Initial Tracking Type
   **MODE_6DOF**, Stereo Rendering **Multi-view (single-pass)**.
5. 🤖 Build headless: `scripts/build-apk.sh` (uses `-buildTarget Android` — required, see below) →
   `scripts/install-apk.sh unity/build/XrealAR.apk` → launch from MyGlasses.

**DoD:** sample runs in 6DoF on the Ultra.

**Build gotchas (learned the hard way, both encoded in `scripts/build-apk.sh`):**
- `-buildTarget Android` is **mandatory** on the Unity CLI. The XREAL SDK gates `XREALSettings`
  fields behind `#if UNITY_ANDROID`; without the flag the editor (macOS) and Android player compile
  different layouts and the build aborts with "script class layout is incompatible…".
- Stop the brew adb daemon before building (`adb kill-server`) — Unity's bundled adb and brew's adb 1.0.41
  clash on port 5037 ("Connection reset by peer"). This is non-fatal noise but pollutes the log.
- The project is already configured headlessly: packages resolved, XREAL XR loader enabled
  (`XrealXRSetup.BaselineSetup`), Android player settings applied (`XrealBuild.ConfigurePlayerSettings`).

## Phase 2 — Blender → glTF, one scene, embedded

1. 🧑 Author/open a test `.blend` (keyframed object + one armature + baked PBR — see `BLENDER_PIPELINE.md`).
2. 🤖 Export: `blender scene.blend --background --python blender/export_glb.py -- assets/scenes/scene.glb`.
3. 🧑 In Unity, import the `.glb` via glTFast (editor import); verify materials + animation in Game view;
   place it ~2 m in front in the HelloMR scene; build + run.

**DoD:** scene renders and animates at real-world scale on the glasses.

## Phase 3 — Placement + anchoring + walkability

1. 🤖 Scripts ready in `unity/Assets/Scripts/`: `RuntimeSceneLoader`, `AnchorPlacementController` (AF6
   native persistence), `ScenePlacementStore`, `AppController`.
2. 🧑 Add `ARPlaneManager`, `ARRaycastManager`, `ARAnchorManager` to the XR Origin; wire `AppController`
   refs in the Inspector; wire input (Beam Pro controller via `NRInput`, or XRI ray interactor) to
   `AppController.CycleSelection()` / `PlaceSelectedAt(screenPoint)`.
3. 🧑 Cross-check against the bundled **AR Features/Anchors** sample (MapQualityIndicator, `TryRemap`).

**DoD:** place the scene on the floor, walk a full circle without drift, relaunch → it returns to the spot.

## Phase 4 — Runtime scene player

- 🤖 `SceneCatalog` reads `/sdcard/Android/data/<pkg>/files/scenes/*.glb`; `AppController` restores saved
  placements on launch and loads the selected `.glb` at runtime.
- 🤖 Drop new scenes without rebuilding: `scripts/push-scene.sh <scene.glb>`.
- 🧑 Build a minimal selection UI bound to `CycleSelection`/`PlaceSelectedAt`.

**DoD:** `push-scene.sh` a new `.glb`, select it in-app, it loads + anchors + plays.

## Phase 5 — Performance, comfort, polish

- 60 fps in 6DoF, single-pass stereo on. ≤ ~150–300k visible tris, ASTC textures ≤ 2K, 1 baked light,
  no realtime shadows, no HDR/bloom. Dark backgrounds, content in central ~30° cone, anchored within ~3 m.
- Enable XREAL **Auto Logcat**; tail with `scripts/logcat.sh`.

**DoD:** 60 fps sustained on a representative scene; malformed `.glb` fails loudly but safely.

---

## Daily workflow

```
scripts/check-device.sh                 # confirm Beam Pro is reachable
scripts/connect-wireless.sh <ip>        # optional: untether so glasses can use the USB-C port
# build APK in Unity ...
scripts/install-apk.sh <path.apk>
blender scene.blend --background --python blender/export_glb.py -- assets/scenes/scene.glb
scripts/push-scene.sh assets/scenes/scene.glb
scripts/logcat.sh                       # watch logs
```

<div align="center">

<img src="assets/icon/banner.png" alt="3D Dejarik - Holochess" width="100%">

<br>

**Walk around a holographic creature-combat board, anchored in your room, and play the**
***Star Wars*** **board game in true 3D - or in your browser if you don't have the glasses.**

<br>

[![▶ Play Online](https://img.shields.io/badge/▶%20%20PLAY%20ONLINE-072033?style=for-the-badge&logo=vercel&logoColor=38E1FF&labelColor=03060D)](https://dejarik.vercel.app/play?mode=bot)
[![⬇ XREAL APK](https://img.shields.io/badge/⬇%20%20XREAL%20APK-072033?style=for-the-badge&logo=android&logoColor=38E1FF&labelColor=03060D)](https://github.com/cifyr/DejarikXR/releases/latest)
[![⬇ Galaxy XR APK](https://img.shields.io/badge/⬇%20%20GALAXY%20XR%20APK-072033?style=for-the-badge&logo=android&logoColor=38E1FF&labelColor=03060D)](https://github.com/cifyr/DejarikXR/releases/latest)
[![◈ Source](https://img.shields.io/badge/%E2%97%88%20%20SOURCE-072033?style=for-the-badge&logo=github&logoColor=FFFFFF&labelColor=03060D)](https://github.com/cifyr/DejarikXR)

<br>

![Unity](https://img.shields.io/badge/Unity%206%20LTS-03060D?style=flat-square&logo=unity&logoColor=38E1FF&labelColor=03060D)
![XREAL](https://img.shields.io/badge/XREAL%20Air%202%20Ultra-03060D?style=flat-square&logoColor=38E1FF&labelColor=03060D)
![Galaxy XR](https://img.shields.io/badge/Galaxy%20XR%20(Android%20XR)-03060D?style=flat-square&logoColor=38E1FF&labelColor=03060D)
![Platform](https://img.shields.io/badge/Android%2010%2B%20·%20ARM64-03060D?style=flat-square&labelColor=03060D)
![License](https://img.shields.io/badge/Fan%20project%20·%20non--commercial-03060D?style=flat-square&labelColor=03060D)

</div>

<div align="center">

> ◈ &nbsp; **No headset?** &nbsp; The [browser version](https://dejarik.vercel.app/play?mode=bot)
> runs the identical ruleset against the bot - same engine, no VR required. &nbsp; ◈

</div>

<br>

<img src="https://img.shields.io/badge/⬡%20%20WHAT%20IT%20IS-072033?style=for-the-badge&labelColor=03060D" height="34">

A 3D, room-scale **Dejarik** (Holochess) for **XREAL Air 2 Ultra** glasses driven by a **Beam Pro**,
and for the **Samsung Galaxy XR** standalone headset. Animated creatures stand on a board
world-anchored in your physical space; walk around it and view the match from any angle in 6DoF,
pick your pieces with hand tracking, and command the board from a holographic control deck — on the
Beam Pro phone for XREAL, or on a wrist-attached worldspace panel for Galaxy XR.

<div align="center">

| ◇ | XREAL build | Galaxy XR build |
|---:|:---|:---|
| **Hardware** | XREAL Air 2 Ultra (onboard 6DoF SLAM) + Beam Pro (Snapdragon 6 Gen 1 / Adreno 710) | Samsung Galaxy XR (Android XR, standalone) |
| **XR stack** | XREAL SDK 3.1.0 · AR Foundation 6 · Unity XR Hands · XR Interaction Toolkit | OpenXR · Unity OpenXR Android XR 1.2 · AR Foundation 6 · Unity XR Hands |
| **Control deck** | Phone touchscreen (RECENTER · MOVE hold+tilt · NEW GAME · live 2D minimap) | Wrist-attached worldspace panel (RECENTER · NEW GAME) + pinch-and-drag board move |
| **APK** | `DejarikXR-<ver>-release.apk` (`com.cadenwarren.dejarik`) | `DejarikXR-<ver>-galaxyxr-release.apk` (`com.cadenwarren.dejarik.galaxyxr`) |
| **Engine** | Unity 6 LTS · IL2CPP · ARM64 · OpenGL ES3 · min API 29 | Unity 6 LTS · IL2CPP · ARM64 · OpenGL ES3 · min API 29 |
| **Models** | glTF 2.0 `.glb` creatures, runtime-loaded via glTFast, custom hologram shader | (identical) |
| **Mode** | Single-player vs. AI · offline-first · no network required | (identical) |

</div>

The full rules are a faithful, test-covered port of the web version's deterministic engine. Both
APKs are built from a single source tree — a `DEJARIK_ANDROID_XR` scripting define gates the
platform-specific input layer (`WorldDeck.cs` on Galaxy XR, phone IMGUI `HoloGui` on XREAL).

> ⚠ **Galaxy XR build is not yet hardware-verified.** Code compiles and the build pipeline produces
> a signed APK; on-device hand tracking, anchoring, and the wrist deck's wrist-pose assumptions
> need confirmation on a real Galaxy XR device. Search the source for `VERIFY` comments.

<br>

<img src="https://img.shields.io/badge/⬡%20%20HOW%20TO%20PLAY-072033?style=for-the-badge&labelColor=03060D" height="34">

You are **Player 0 - cyan / blue**. &nbsp; The opponent is **Player 1 - amber / red**.

<img src="https://img.shields.io/badge/▸%20CONTROLS-0A2A3A?style=flat-square&labelColor=03060D" height="24">

Both builds share the same piece-selection model — touch a piece with your fingertip — and the same
rules. The difference is the control deck: phone on XREAL, wrist panel on Galaxy XR.

- **Select a piece** - reach out and **touch a piece** with your fingertip (hand tracking). A
  reticle marks the cell nearest your finger so you can aim.
- **Move / attack** - with a piece selected, the legal squares light up. Touch a glowing square to
  move there, or touch an adjacent enemy to attack. Mis-touches never drop your selection.

<img src="https://img.shields.io/badge/▸%20XREAL%20-%20PHONE%20DECK-0A2A3A?style=flat-square&labelColor=03060D" height="24">

- **Phone control deck** (Beam Pro touchscreen):
  - **RECENTER** - re-place the board in front of where you're looking.
  - **MOVE** *(hold + tilt)* - hold and tilt the phone like a wand to nudge the board in X/Y/Z.
  - **NEW GAME** - reset the match.
  - A live **2D minimap** mirrors the board so a spectator on the phone can follow along.

<img src="https://img.shields.io/badge/▸%20GALAXY%20XR%20-%20WRIST%20DECK-0A2A3A?style=flat-square&labelColor=03060D" height="24">

- **Wrist control panel**: turn your **left palm up** to summon a glowing two-button panel on the
  back of your wrist. Poke a button with your right index fingertip:
  - **RECENTER** - re-place the board in front of where you're looking.
  - **NEW GAME** - reset the match.
- **Move the board** *(pinch + drag)* - **pinch** with your right hand over the board and move
  your hand to translate the board through the room. Release the pinch to drop it where it sits.
  This replaces the phone's hold-and-tilt gesture, which has no analogue on a standalone headset.

<img src="https://img.shields.io/badge/▸%20RULES-0A2A3A?style=flat-square&labelColor=03060D" height="24">

Dejarik follows the *Holochess* rules by Mike Kelly.

- **Board** - a disc of **25 spaces**: a center hub, a 12-space inner ring, and a 12-space outer
  ring. Center links to every inner space; each ring space links to its orbit neighbours and its
  same-ray partner in the other ring.
- **Turn** - 2 actions per turn; each is a move or an attack (mix freely).
- **Movement (exact-N)** - a piece moves **exactly** its Movement value through empty cells, never
  revisiting one - so it walks *around* other creatures. (A Movement-3 piece can't stop on an
  adjacent cell; it must spend all 3 steps.)

<div align="center">

**◇ Creatures ◇**

| Creature | ATK | DEF | MOV |
|:---|:--:|:--:|:--:|
| Mantellian Savrip | 6 | 6 | 2 |
| Monnok | 6 | 5 | 3 |
| Ghhhk | 4 | 3 | 2 |
| Houjix | 4 | 4 | 1 |
| Kintan Strider | 2 | 7 | 3 |
| Ng'ok | 3 | 8 | 1 |
| K'lor'slug | 7 | 3 | 2 |
| Molator | 8 | 2 | 2 |

**◇ Combat ◇** &nbsp; - attacker rolls *Attack* d6, defender rolls *Defense* d6 · `diff = attack − defense`

| Roll difference | Outcome |
|:---|:---|
| `diff ≥ 7` | **kill** - defender destroyed |
| `1 … 6` | **push** - attacker shoves the defender to an open neighbour |
| `−6 … 0` | **counter-push** - defender shoves the attacker |
| `diff ≤ −7` | **counter-kill** - attacker destroyed |

</div>

- **Winning** - eliminate all enemy pieces. When one piece remains per side, a final **duel**
  decides it. A start-of-turn position repeated three times is a **draw**.

<br>

<img src="https://img.shields.io/badge/⬡%20%20INSTALL%20(SIDELOAD)-072033?style=for-the-badge&labelColor=03060D" height="34">

Neither APK is on any store — sideload over `adb`. Two distinct APKs ship per release (different
bundle IDs, so they can coexist on a multi-device test rig).

<img src="https://img.shields.io/badge/▸%20XREAL%20AIR%202%20ULTRA%20+%20BEAM%20PRO-0A2A3A?style=flat-square&labelColor=03060D" height="24">

```bash
# grab DejarikXR-<version>-release.apk from the Releases page
adb install -r DejarikXR-1.0-release.apk
# then launch "Dejarik XR" from the Beam Pro launcher, glasses connected
```

Requires an XREAL Beam Pro (or compatible nebulaOS device) with an Air 2 Ultra attached.

<img src="https://img.shields.io/badge/▸%20SAMSUNG%20GALAXY%20XR-0A2A3A?style=flat-square&labelColor=03060D" height="24">

```bash
# grab DejarikXR-<version>-galaxyxr-release.apk from the Releases page
# enable Developer options + USB debugging on the headset, plug it in (or use wireless adb)
adb install -r DejarikXR-1.0-galaxyxr-release.apk
# launch "Dejarik XR" from the Android XR app drawer
```

Requires a Samsung Galaxy XR running Android XR. **Hand tracking must be enabled** in the system
settings — the game uses fingertip touch and wrist-pose orientation as its only input.

> Not yet hardware-verified. If hand tracking, anchoring, or the wrist panel misbehaves, please
> file an issue with the device's Android XR version and a `logcat -s Unity` capture.

<br>

<img src="https://img.shields.io/badge/⬡%20%20BUILD%20FROM%20SOURCE-072033?style=for-the-badge&labelColor=03060D" height="34">

Prerequisites: **Unity 6000.0 LTS** with Android Build Support (IL2CPP), the **XREAL SDK 3.1.0**
(for the XREAL build), the **Unity OpenXR + Android XR** packages (for the Galaxy XR build —
already pinned in `unity/Packages/manifest.json`), and `adb`.

```bash
# one-time: configure Android player settings (IL2CPP, ARM64, GLES3, minSdk 29)
Unity -batchmode -nographics -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.ConfigurePlayerSettings -quit -logFile -
```

<img src="https://img.shields.io/badge/▸%20XREAL%20BUILD-0A2A3A?style=flat-square&labelColor=03060D" height="24">

```bash
# development build (debug-signed, for iterating on-device)
scripts/build-apk.sh                       # -> unity/build/DejarikXR.apk
scripts/install-apk.sh unity/build/DejarikXR.apk
scripts/logcat.sh                          # tail filtered device logs
```

<img src="https://img.shields.io/badge/▸%20GALAXY%20XR%20BUILD-0A2A3A?style=flat-square&labelColor=03060D" height="24">

The Galaxy XR build path flips the active XR loader to OpenXR with the Android XR provider, sets
the `DEJARIK_ANDROID_XR` scripting define (so `WorldDeck` replaces the phone IMGUI), and writes a
separate APK with a distinct bundle ID. From a clean tree:

```bash
scripts/build-apk-galaxyxr.sh              # -> unity/build/DejarikXR-galaxyxr.apk
scripts/install-apk.sh unity/build/DejarikXR-galaxyxr.apk
```

If you build **both** APKs in the same Editor session, run `RestoreXrealBuildConfig` between them
to clear the Galaxy XR define and reset the XREAL bundle ID:

```bash
Unity -batchmode -nographics -buildTarget Android -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.RestoreXrealBuildConfig -quit -logFile -
```

**On the first import**, open Unity once and confirm in `Project Settings → XR Plug-in Management
→ OpenXR → Android`:

- **Hand Tracking** and **Anchors** are ticked under the Android XR feature group. The headless
  setup attempts this via reflection, but the OpenXR plug-in's feature ID surface has moved across
  versions — search `AndroidXrSetup.cs` for `VERIFY` if a feature shows as missing.
- At least one **interaction profile** is selected (verified against OpenXR 1.16.1: build halts
  at preprocess otherwise with `OpenXRProjectValidation: At least one interaction profile must be
  added`). Tick whichever Android XR controller / hand-interaction profile your headset advertises.
- The **Composition Layers Support** feature is enabled if the Composition Layers package was
  pulled in transitively (the androidxr-openxr 1.2 dep chain pulls it in).

If you switch back to building for XREAL after a Galaxy XR build in the same Editor session, run
`XrealAR.EditorTools.XrealBuild.RestoreXrealBuildConfig` — it re-assigns the XREAL loader and
removes the OpenXR loader so the XREAL APK doesn't silently boot through OpenXR.

<img src="https://img.shields.io/badge/▸%20RELEASE--SIGNED%20BUILD-0A2A3A?style=flat-square&labelColor=03060D" height="24">

Signing comes from environment variables, so secrets never touch the committed project settings:

```bash
export DEJARIK_KS_PATH=~/.dejarik-signing/dejarik-release.keystore \
       DEJARIK_KS_PASS=<store-pass> DEJARIK_KEY_ALIAS=dejarik DEJARIK_KEY_PASS=<key-pass> \
       DEJARIK_VERSION=1.0 DEJARIK_VERSION_CODE=1

# XREAL release
DEJARIK_OUT=build/DejarikXR-1.0-release.apk \
Unity -batchmode -nographics -buildTarget Android -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.BuildReleaseApk -quit -logFile -

# Galaxy XR release
DEJARIK_OUT=build/DejarikXR-1.0-galaxyxr-release.apk \
Unity -batchmode -nographics -buildTarget Android -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.BuildGalaxyXrReleaseApk -quit -logFile -
```

<img src="https://img.shields.io/badge/▸%20TESTS-0A2A3A?style=flat-square&labelColor=03060D" height="24">

The pure rules engine (`unity/Assets/Scripts/Game/`) is engine-independent and covered by EditMode
tests in the Unity Test Framework.

<br>

<img src="https://img.shields.io/badge/⬡%20%20PROJECT%20STRUCTURE-072033?style=for-the-badge&labelColor=03060D" height="34">

```
unity/Assets/Scripts/Game/             pure, deterministic rules engine (no Unity refs) + tests
unity/Assets/Scripts/View/             board, creatures, hand input, audio, HUD, phone deck
unity/Assets/Shaders/                  Dejarik/Hologram shader
unity/Assets/StreamingAssets/Models/   board.glb + per-creature .glb (runtime-loaded)
unity/Assets/Editor/                   headless build + offline screenshot harness
scripts/                               adb / device workflow helpers
```

<br>

<div align="center">

⬡ &nbsp; ⬡ &nbsp; ⬡

**Dejarik / Holochess is from _Star Wars_ (Lucasfilm). A non-commercial fan project.**

Ruleset after the *Holochess* rules by Mike Kelly · built with Unity, the XREAL SDK & glTFast.

</div>

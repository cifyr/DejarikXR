# 3D Dejarik (Holochess)

A 3D, room-scale **Dejarik** — the holographic creature-combat board game from *Star Wars* — for
**XREAL Air 2 Ultra** AR glasses driven by a **Beam Pro**. Animated creatures stand on a board
anchored in your physical space; walk around it and view the match from any angle in 6DoF, select
your pieces with hand tracking, and command the board from a holographic control deck on the phone.

The full game logic is a faithful, test-covered port of the web version's deterministic rules engine.

| | |
|---|---|
| ▶︎ **Play online (no headset needed)** | **https://dejarik.vercel.app/play?mode=bot** |
| ⬇︎ **Download the Android APK** | **[Latest release](https://github.com/cifyr/DejarikXR/releases/latest)** |
| 💻 **Source code** | **https://github.com/cifyr/DejarikXR** |

> No glasses? The [online version](https://dejarik.vercel.app/play?mode=bot) runs the identical
> ruleset in any browser against the bot.

---

## What it is

- **Hardware**: XREAL Air 2 Ultra (onboard 6DoF SLAM) + Beam Pro (Snapdragon 6 Gen 1 / Adreno 710).
- **Engine**: Unity 6 LTS, IL2CPP, ARM64, OpenGL ES3, min API 29.
- **XR**: XREAL SDK 3.1.0 + AR Foundation 6 + Unity XR Hands + XR Interaction Toolkit, 6DoF tracking.
- **Models**: glTF 2.0 (`.glb`) creatures loaded at runtime via glTFast, shaded with a custom
  holographic shader.
- Single-player vs. an AI opponent. Offline-first; no network required.

---

## How to play

You are **Player 0 (cyan / blue)**. The opponent is **Player 1 (amber / red)**.

### Controls

- **Select a piece**: reach out and **touch a piece** with your fingertip (hand tracking). A white
  reticle shows the cell nearest your finger so you can aim.
- **Move / attack**: with a piece selected, the legal squares light up — touch a glowing square to
  move there, or touch an adjacent enemy to attack it. Mis-touches don't drop your selection.
- **Phone control deck** (Beam Pro touchscreen):
  - **RECENTER** — re-place the board in front of where you're looking.
  - **MOVE** *(hold)* — hold and tilt the phone like a wand to nudge the board in X/Y/Z (sweep
    left/right, tilt up/down, roll for closer/farther).
  - **NEW GAME** — reset.
  - A live **2D minimap** mirrors the board so a spectator on the phone can follow the match.

### Rules (summary)

Dejarik follows the *Holochess* rules by Mike Kelly. Full spec lives in the
[web project's `GAME_SPEC.md`](https://dejarik.vercel.app).

- **Board**: a disc of **25 spaces** — a center hub, a 12-space inner ring, and a 12-space outer
  ring. Center connects to all inner spaces; each ring space connects to its orbit neighbours and
  its same-ray partner in the other ring.
- **Pieces**: each side has 4 of 8 creature types, each with **Attack / Defense / Movement**:

  | Creature | ATK | DEF | MOV |
  |---|---|---|---|
  | Mantellian Savrip | 6 | 6 | 2 |
  | Monnok | 6 | 5 | 3 |
  | Ghhhk | 4 | 3 | 2 |
  | Houjix | 4 | 4 | 1 |
  | Kintan Strider | 2 | 7 | 3 |
  | Ng'ok | 3 | 8 | 1 |
  | K'lor'slug | 7 | 3 | 2 |
  | Molator | 8 | 2 | 2 |

- **Turn**: 2 actions per turn; each is a move or an attack (mix freely).
- **Movement (exact-N)**: a piece moves **exactly** its Movement value in steps through empty
  cells, never revisiting a cell — so it walks *around* other creatures. (A Movement-3 piece can't
  stop on an adjacent cell; it must spend all 3 steps.)
- **Combat**: attacker rolls *Attack* d6, defender rolls *Defense* d6. `diff = attack − defense`:
  - `diff ≥ 7` → **kill** (defender destroyed)
  - `1…6` → **push** (attacker shoves the defender to an open neighbour)
  - `−6…0` → **counter-push** (defender shoves the attacker)
  - `diff ≤ −7` → **counter-kill** (attacker destroyed)
- **Winning**: eliminate all enemy pieces. When only one piece remains per side, a final **duel**
  decides it. A start-of-turn position repeated three times is a **draw**.

---

## Install (sideload)

The app isn't on any store — sideload the release APK onto the Beam Pro over `adb`:

```bash
# 1. download DejarikXR-<version>-release.apk from the Releases page
adb install -r DejarikXR-1.0-release.apk
# 2. launch "Dejarik XR" from the Beam Pro launcher, glasses connected
```

Requires an XREAL Beam Pro (or compatible nebulaOS device) with an Air 2 Ultra attached.

---

## Build from source

Prerequisites: **Unity 6000.0 LTS** with Android Build Support (IL2CPP), the **XREAL SDK 3.1.0**,
and `adb`. macOS dev host assumed; scripts use `set -euo pipefail`.

```bash
# one-time: configure Android player settings (IL2CPP, ARM64, GLES3, minSdk 29)
Unity -batchmode -nographics -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.ConfigurePlayerSettings -quit -logFile -

# development build (debug-signed, for iterating on-device)
scripts/build-apk.sh                       # -> unity/build/DejarikXR.apk
scripts/install-apk.sh unity/build/DejarikXR.apk

# tail device logs
scripts/logcat.sh
```

### Release-signed build

Signing comes from environment variables so secrets never touch the committed project settings:

```bash
export DEJARIK_KS_PATH=~/.dejarik-signing/dejarik-release.keystore \
       DEJARIK_KS_PASS=<store-pass> DEJARIK_KEY_ALIAS=dejarik DEJARIK_KEY_PASS=<key-pass> \
       DEJARIK_VERSION=1.0 DEJARIK_VERSION_CODE=1 \
       DEJARIK_OUT=build/DejarikXR-1.0-release.apk

Unity -batchmode -nographics -buildTarget Android -projectPath unity \
  -executeMethod XrealAR.EditorTools.XrealBuild.BuildReleaseApk -quit -logFile -
```

### Tests

The pure rules engine (`unity/Assets/Scripts/Game/`) is covered by EditMode tests in the Unity Test
Framework and is engine-independent.

---

## Project structure

```
unity/Assets/Scripts/Game/    pure, deterministic rules engine (no Unity refs) + tests
unity/Assets/Scripts/View/    rendering: board, creatures, hand input, audio, HUD, phone deck
unity/Assets/Shaders/         Dejarik/Hologram shader
unity/Assets/StreamingAssets/Models/   board.glb + per-creature .glb (runtime-loaded)
unity/Assets/Editor/          headless build + offline screenshot harness
scripts/                      adb / device workflow helpers
```

---

## Credits

Dejarik / Holochess is from *Star Wars* (Lucasfilm). This is a non-commercial fan project.
Ruleset after the *Holochess* rules by Mike Kelly. Built with Unity, the XREAL SDK, and glTFast.

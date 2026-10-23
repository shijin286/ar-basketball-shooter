# AR Basketball Shooter – Stage 10: Laptop Version (Windows .exe)

**Goal:** show the game working on a laptop. Laptops have no ARCore, so it can't run in AR there.

| Phone (APK) | Laptop (.exe) |
|---|---|
| ARCore finds your real floor through the camera | A virtual court: a wooden floor with lines and four walls |
| Move the phone to look around | **W A S D** / arrow keys walk, hold the **right** mouse button and drag to look |
| **Tap** the floor to place the hoop | **Left-click** the floor to place the hoop |
| **Swipe up** to shoot | **Drag the mouse up** with the left button and let go |
| Back button = pause | **Esc** = pause |

Shooting physics, aim assist, scoring, sounds, confetti, timer, challenge and leaderboard all use the **same scripts** as on the phone. Only how the hoop finds the floor and how the camera moves are different.

**Before you start:** all Stage 1–9 tests are green ✔.

---

## 1. New and changed files
```
Assets/Scripts/DesktopMode.cs            ← NEW  laptop version: AR off, virtual court, camera, UI column
Assets/Scripts/DesktopCameraRig.cs       ← NEW  WASD walk + right-mouse look
Assets/Scripts/DesktopAutoDemo.cs        ← NEW  self-test: "-autodemo" plays the game with a virtual mouse
Assets/Scripts/ARPlanePlacement.cs       ← UPDATED  UseDesktopFloor / TryGetDesktopFloorPose (no AR anchor on the court)
Assets/Scripts/SafeArea.cs               ← UPDATED  MaxAspect: phone layout stays a centred column on a wide screen
Assets/Scripts/Editor/WindowsBuilder.cs  ← NEW  Tools > AR Basketball > Add Desktop Mode To Scene / Build Windows App (.exe)
Assets/Materials/Desktop/DesktopSurface.mat   ← NEW (made by the menu item)
Assets/Tests/Stage10/ARBasketball.Tests.Stage10.asmdef
Assets/Tests/Stage10/Stage10DesktopTests.cs
```

## 2. How it works
1. `DesktopMode` sits on **GameSystems** and runs first (`DefaultExecutionOrder(-100)`).
2. `DesktopMode.ShouldUse` decides whether to switch on:
   - **laptop build:** yes;
   - **phone:** no (ARCore);
   - **Editor:** no, because the Editor keeps using XR Simulation.
3. When it switches on:
   1. It disables the **AR Session**, plane, raycast and anchor managers, the camera feed (`ARCameraBackground`) and phone tracking (`TrackedPoseDriver`).
   2. `BuildCourt` creates the court:
      - a 14 × 14 m floor whose top is at **y = 0**, with the floor Physic Material so the ball bounces;
      - a wooden plank texture made in code (`CreateWoodTexture`);
      - white lines with no colliders;
      - four 5 m walls, so balls stay on the court.
   3. `ARPlanePlacement.UseDesktopFloor(floor)` makes clicks raycast against the court floor instead of AR planes. The hoop still turns to face the player. No AR anchor is needed, because the court never drifts.
   4. `DesktopCameraRig` holds the camera at 1.5 m (like a phone held in front of you) and keeps it inside the walls. Walking pauses while the game is paused or while you type your leaderboard name.
   5. The UI was designed for a 1080 × 1920 phone. The canvas therefore scales by height, and `SafeArea.MaxAspect = 9/16` keeps every panel in a centred portrait column. The court stays visible on both sides.
   6. Instructions change to mouse wording, such as "Click the floor to place the hoop".

## 3. Build the .exe
1. **Tools → AR Basketball → Build Windows App (.exe)**. The first build takes about 3–5 minutes.
2. The output goes to `Builds/Windows/ARBasketballShooter.exe`.
3. To use it on another PC, copy the **whole** `Builds/Windows` folder (the `_Data` folder and DLLs are needed).

Command line (what `run_desktop_pipeline.ps1` does):
```
Unity -batchmode -quit -projectPath <project> -buildTarget Win64 -executeMethod WindowsBuilder.BuildExe
```
During the build, `WindowsBuilder` turns off "Initialize XR on Startup" for Desktop, because XR Simulation is Editor-only. It turns it back on afterwards, so Play mode in the Editor still uses XR Simulation.

Afterwards, switch back to Android before building the APK: **File → Build Profiles → Android → Switch Platform**. The pipeline script does this for you.

### Browser version (WebGL)
The same laptop version can run in a web browser:
1. Run **Tools → AR Basketball → Build Web Version (WebGL)**.
2. The output goes to `Builds/WebGL/`.
3. The page comes from `Assets/WebGLTemplates/ARBasketball/index.html`: a full-window canvas with a loading bar and the controls. Right-click doesn't open the browser menu.

How it fits the rest of the game:
- `DesktopMode.ShouldUse(..., isWebGL)` switches the court on in **every** browser, even on a phone, because browsers have no ARCore. On a phone you tap to place the hoop and swipe up to shoot.
- The build uses gzip with `decompressionFallback`, because GitHub Pages can't send `Content-Encoding` headers.
- `autoSyncPersistentDataPath` keeps the leaderboard in the browser between visits.

The live version is at **https://shijin286.github.io/ar-basketball-shooter/play/**.

## 4. Showing it to your teacher (2-minute demo)
1. Double-click **AR Basketball Shooter** on the Desktop, or `ARBasketballShooter.exe`. Press **Alt + Enter** for full screen.
2. Title screen: explain FREE PLAY and 60s CHALLENGE, then open **LEADERBOARD**.
3. Press **FREE PLAY**.
   - Hold the right mouse button and look at the floor. A cyan ring shows where the hoop will go.
   - **Click** to place the hoop.
   - Explain that on the phone this is ARCore plane detection with an AR anchor.
4. Stand 2–3 m away, using **S** to step back if needed. **Drag the mouse up** quickly and let go.
   - Show a slow drag against a fast one: power comes from swipe speed.
   - Show a sideways drag for aim.
   - Point out the score, shots, accuracy, the "SWISH!" message, the sound and the confetti.
5. Press **Reposition Hoop**, then place the hoop somewhere else. Press **Reset Ball**, then **Esc** to pause, resume and restart.
6. Back on the title screen, play the **60s CHALLENGE**:
   - the timer turns red for the last 10 s;
   - beeps sound for the last 5 s, then the buzzer;
   - a buzzer beater still counts.
   - Then type a name to save the score and show it on the leaderboard.
7. Finish by showing the phone APK for the real AR version, if a phone is available.

## 5. Tests
**Window → General → Test Runner → EditMode → Run All**. The 25 new Stage 10 tests check:

| Area | What is checked |
|---|---|
| When desktop mode switches on | on in a laptop build and in every browser (even on a phone), off in the phone app, off in the Editor unless forced |
| Court | floor top at y = 0 and 14 m wide; colliders only on the floor and 4 walls; walls surround the floor and are over 4 m tall; the wood texture tiles and isn't one flat colour |
| Camera | W walks where you look; W + D isn't faster; height never changes; stays inside the walls; look angle limited to −35°…75°; turns smoothly to face the placed hoop |
| UI column | 1920 × 1080 → a centred 607.5 px column; a phone screen and "no limit" are unchanged |
| Placement | a click on the court gives a pose there, upright and facing the player; looking at the sky or "no court" gives none |
| Scene | DesktopMode is on GameSystems with all 8 references assigned; Force In Editor is off; the surface material is URP Lit |

**Self-test of the real .exe:** `ARBasketballShooter.exe -autodemo` plays the game with a virtual mouse, using the same input path as you:
1. it clicks FREE PLAY;
2. it clicks the floor 3 m ahead;
3. it makes five upward drags.

It saves screenshots and `autodemo_result.txt` in `Builds/Windows/AutoDemo/`, then closes.

## 6. Troubleshooting
| Problem | Fix |
|---|---|
| "Windows protected your PC" on first start | **More info → Run anyway** (the app isn't code-signed) |
| Black or pink court | Run **Tools → AR Basketball → Add Desktop Mode To Scene** again, then rebuild (the URP material must be assigned) |
| Editor Play mode shows the court instead of XR Simulation | Untick **DesktopMode → Force In Editor** on GameSystems |
| UI too small or too big | Resize the window or press **Alt + Enter**; the UI scales with the window height |
| The camera doesn't turn | Hold the **right** mouse button while dragging; the left button is for placing and shooting |
| "Switch the platform to Android" when building the APK | File → Build Profiles → Android → Switch Platform |

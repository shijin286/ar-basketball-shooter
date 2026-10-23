# AR Basketball Shooter – Stage 8: Android Build & Delivery

**Goal:**
- an optimised, signed **APK** that installs and runs on an ARCore phone;
- a complete **testing checklist** and **troubleshooting** guide;
- the README and college report.

**Before you start:** EditMode Stages 1–7 and PlayMode are green ✔.

---

## 1. Add the files
```
Assets/Scripts/MobileSettings.cs              ← on GameSystems (keeps the screen on)
Assets/Scripts/Editor/AndroidBuilder.cs       ← Tools > AR Basketball > Check / Build / Build And Run
Assets/UI/AppIcon.png                         ← 1024×1024 app icon (made by Tools/generate_icon.py)
Assets/Tests/Stage8/ARBasketball.Tests.Stage8.asmdef
Assets/Tests/Stage8/Stage8ReleaseTests.cs
README.md                                     ← project root
Docs/Project_Report.md                        ← abstract, objectives, methodology, results, future work
```

## 2. Final project settings
### 2.1 Player Settings (Edit → Project Settings → Player → Android tab)
| Setting | Value | Why |
|---|---|---|
| Company Name | your name | Shown in the app info |
| **Product Name** | `AR Basketball Shooter` | Name under the app icon |
| **Version** | `1.0` | Shown to users; raise it for every new release |
| **Bundle Version Code** | `1` | Must go up by 1 for every APK you install over an older one |
| **Default Icon** (top of Player Settings) | `Assets/UI/AppIcon.png` | Launcher icon |
| **Package Name** (Other Settings) | `com.yourname.arbasketball` | Unique app ID. Lowercase, no spaces, never Unity's default. |
| **Minimum API Level** | Android 7.0 (API **24**) | ARCore's minimum |
| **Target API Level** | Automatic (highest installed) | Google Play needs a recent API level. Automatic always uses the newest SDK. |
| Scripting Backend / Architectures | **IL2CPP / ARM64** | Required for 64-bit phones and Google Play |
| Graphics APIs | **OpenGLES3** only | Most compatible with ARCore |
| Default Orientation | **Portrait** | The UI is designed for portrait |
| Managed Stripping Level | **Minimal** | Higher levels can strip code AR Foundation needs. Only raise it if you test carefully. |
| Application Entry Point | **Activity** | Avoids ARCore build issues |
| Camera Usage Description / permission | added automatically by ARCore | The phone asks the first time |
| XR Plug-in Management → ARCore → Requirement | **Required** | Google Play hides the app from phones without ARCore |

### 2.2 Build Profiles (File → Build Profiles → Android)
- **Scene List:** `Scenes/ARBasketballScene` is ticked and at index 0. It should be the only scene.
- **Texture Compression:** ASTC, or "Use Player Settings" with ASTC listed first. All ARCore phones support ASTC.
- **Development Build:** ✔ while testing (lets the Profiler and Console logs connect). **✘ for the final APK you hand in.**
- **Build App Bundle (Google Play):** ✘. You want an **.apk** to install directly. The build tool unticks it for you.

### 2.3 Scene: MobileSettings and AR Session
- **GameSystems → Add Component → Mobile Settings.** Set Keep Screen On ✔ and Target Frame Rate 0.
- **AR Session:** Attempt Update ✔, **Match Frame Rate ✔**. The game then renders at the camera's rate (usually 30 fps). That's smooth for AR, and it saves battery and heat.

## 3. Performance on phones
| Area | Setting | Effect |
|---|---|---|
| URP asset `Settings/Mobile_RPAsset` | HDR ✘, Anti-aliasing (MSAA) 2×, Render Scale 1, Depth Texture ✘, Opaque Texture ✘ | Less GPU work per frame |
| | Main Light Shadows ✔ with Max Distance **6 m** and **1** cascade, Soft Shadows ✘. Additional Lights: Disabled. | Cheap shadows for the hoop and ball |
| `Settings/Mobile_Renderer` | Post-processing ✘ (only *AR Background Renderer Feature* in the list) | No full-screen effects |
| Quality (Project Settings → Quality) | Android uses the **Mobile** level | The lighter URP asset is used on the phone |
| Physics | Fixed Timestep 0.02, Default Solver Iterations 6 | Enough for a ball and a hoop |
| AR | Plane Detection = Horizontal only; planes hidden after placing (Stage 1) | Less tracking work and less overdraw |
| Game code | Balls capped at 4; audio voices and confetti reused; no `Find` calls every frame | No garbage-collection stutter |
| Textures | Ball texture 1024 px, ASTC | Small download, fast loading |

**Measure it:**
1. Make a Development Build with **Autoconnect Profiler ✔**.
2. Open **Window → Analysis → Profiler** while playing on the phone.
3. Aim for a steady **30 fps** (33 ms per frame) and no GC spikes during shots.
4. Play for 10 minutes and check the phone doesn't get too hot or slow down.

## 4. Build the APK
### Option A: one click (recommended)
1. Run **Tools → AR Basketball → Check Build Settings**. It must say *"Everything looks ready"*.
2. Run **Tools → AR Basketball → Build Android APK**. The first IL2CPP build takes 5–15 minutes; later builds are faster.
3. The APK appears in the project's `Builds/` folder as `ARBasketballShooter_v1.0.apk` (about 40–70 MB), and the folder opens.

### Option B: Unity menus
In **File → Build Profiles → Android**, click **Build**, choose `Builds/ARBasketballShooter_v1.0.apk`, and save.

## 5. Install and test on the phone
**Prepare the phone (once):**
1. Check it's on Google's **ARCore supported devices** list.
2. Install or update **Google Play Services for AR** from the Play Store.
3. Turn on **Developer options → USB debugging**: tap Settings → About phone → Build number 7 times.

**Install it, using any of these:**
1. **Build And Run:** connect USB and run **Tools → AR Basketball → Build And Run On Phone**. It installs and starts by itself.
2. **adb:** Unity's copy of adb is in `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`.
   ```bash
   adb install -r Builds/ARBasketballShooter_v1.0.apk
   ```
3. **Copy the file:** copy the APK to the phone and open it in the Files app. Allow *Install unknown apps* for Files when asked.

**Watch the logs while testing:**
```bash
adb logcat -s Unity ARCore
```

## 6. Master testing checklist
Tick everything on the **real phone**. The automated tests cover the logic, and this list covers the real-world experience. That's 140 tests after Stage 8 (132 EditMode + 8 PlayMode) and 189 after Stage 9 (181 + 8).

### 6.1 Start & AR (Feature 1)
- [ ] The app opens on the title screen. Camera permission is requested once.
- [ ] Start Game → "Place the hoop" card. The camera image shows (no black screen).
- [ ] The floor is detected in under 10 s in a well-lit room. The ring indicator follows the screen centre.
- [ ] Walls aren't used for placement. Tapping a button never places the hoop.

### 6.2 Hoop (Feature 2)
- [ ] Tapping places the hoop on the floor with the rim facing you, at a believable size.
- [ ] Walking around it (2 m away, 360°), the hoop stays fixed.
- [ ] Reposition Hoop → place it elsewhere → only one hoop exists.

### 6.3 Ball (Feature 3)
- [ ] A textured ball appears at the bottom of the screen and follows the phone.
- [ ] Missed balls bounce realistically, roll, and disappear after resting. A new ball appears.
- [ ] Balls never fall through the floor, even outside the scanned area.
- [ ] Reset Ball clears all balls and gives a fresh one.

### 6.4 Shooting (Feature 4)
- [ ] An upward flick shoots, and a faster flick throws harder (shown as "Power %").
- [ ] Short, slow or downward swipes don't shoot, and a hint explains why.
- [ ] A sideways flick aims left or right. One swipe = exactly one shot.
- [ ] From 2–3 m, baskets are possible (about half of good flicks with Aim Assist 0.6).

### 6.5 Scoring (Feature 5)
- [ ] A ball through the hoop gives +1 immediately, with "SWISH!".
- [ ] Rim-only and backboard-only hits score 0. A ball from below scores 0.
- [ ] One ball never scores twice. Shots and accuracy are correct after 10 shots (count them by hand).

### 6.6 UI (Feature 6)
- [ ] Title, Start, Place Hoop and Aim & Shoot instructions all appear at the right time.
- [ ] Pause (button, Back key, going to the home screen) freezes the balls. Resume continues.
- [ ] Restart Game resets the score and shots and keeps the hoop.
- [ ] Texts are readable and nothing is under the notch. Check on 2 phones or in the Simulator.

### 6.7 Audio & effects (Feature 7)
- [ ] Bounce, rim, backboard, swish, chime, whoosh and click sounds all play.
- [ ] Confetti, net stretch and vibration happen on a basket. Buttons animate.

### 6.7b Challenge & leaderboard (Stage 9)
- [ ] The 60s clock waits while placing and while paused, then counts down. It turns red at 0:10, beeps from 0:05, and the buzzer sounds at 0:00.
- [ ] A shot in the air at the buzzer still counts (BUZZER BEATER!). Nothing counts after the results appear.
- [ ] A top-10 score asks for a name, and SAVE shows the highlighted leaderboard.
- [ ] The leaderboard is still there after closing and reopening the app. FREE PLAY never ends.

### 6.8 Build & performance
- [ ] Check Build Settings shows no problems, and Stage 8 tests are green.
- [ ] A steady ~30 fps in the Profiler, no stutter on shots, and the phone isn't too hot after 10 minutes.
- [ ] The app keeps the screen on, and recovers after switching to another app and back.

## 7. Troubleshooting (all stages)
| Problem | Most common causes and fixes |
|---|---|
| **AR plane detection does not work** | Poor light or a plain/shiny floor: use a textured floor and good light, and move the phone slowly from side to side. AR Plane Manager must be enabled with Detection Mode = Horizontal and a Plane Prefab assigned. Google Play Services for AR may be out of date. |
| **The camera displays a black screen** | Missing **AR Background Renderer Feature** on the URP renderer used on Android (Mobile_Renderer). Camera permission denied: allow it in App info → Permissions. Graphics API isn't OpenGLES3. Main Camera is missing **AR Camera Background**. |
| **The hoop cannot be placed** | The state must be PlacingHoop: press Start Game first. Hoop Prefab must be assigned. AR Raycast Manager must be on the XR Origin. Tap the highlighted grid, not a button. In the Editor, use left-click. |
| **The basketball falls through the floor** | BallSpawner → Create Virtual Floor must be ✔. The ball needs Collision Detection = **Continuous Dynamic** (run the ball builder again). The ARPlane prefab must keep its **Mesh Collider**. Nothing may scale the ball root. |
| **The ball does not respond to swipes** | The state must be Playing (hoop placed, not paused). There must be a ball in your hand. BasketballShooter must be enabled. Swipe **up**, quickly, more than 6% of the screen height. Active Input Handling must include the Input System. In the Editor, click the Game view first. |
| **The ball passes through the rim without scoring** | Rebuild the hoop (Stage 5) so `Rim/ScoreZone` exists, with **Is Trigger ✔** and **HoopScoreTrigger**. Only one GameManager. The ball must enter from **above**. Very fast shots: set Fixed Timestep to 0.01. |
| **Multiple points are awarded for one shot** | There are two GameManagers or two HoopScoreTriggers in the scene/prefab: remove the extra. An old BasketballController without `TryMarkScored`: copy the Stage 3 version. |
| **UI buttons do not work** | No EventSystem, or it's missing the Input System UI Input Module. A transparent object covers the button: instruction cards need Canvas Group → Blocks Raycasts ✘, and texts need Raycast Target ✘. On Click is empty, or points at a prefab instead of GameSystems. The Canvas is missing its Graphic Raycaster. |
| **Android build errors** | Read the **first** red error in the Console. Gradle/SDK/NDK errors: reinstall the Android modules from Unity Hub (with OpenJDK, SDK & NDK). "Minimum API": set it to 24. "Package name": run Check Build Settings. Duplicate classes: delete `Library/Bee` and rebuild. GameActivity errors: tick Activity only. Path problems: keep the project path short, with no spaces or non-English characters. |
| **ARCore reports that the device is unsupported** | The phone isn't on Google's ARCore supported-devices list (an emulator never is). Google Play Services for AR is missing or old: update it from the Play Store. Rooted or custom ROMs can fail certification. Test on a listed phone. |
| App closes right after opening | ARCore Requirement = Required on an unsupported phone, or Graphics API = Vulkan only. Check `adb logcat -s Unity ARCore`. |
| "App not installed" | An older version with a different signature is installed: uninstall it first. Or the Bundle Version Code is lower than the installed one. |

## 8. What to hand in
1. **APK:** `Builds/ARBasketballShooter_v1.0.apk`, built with Development Build ✘.
2. **Project source:** zip the project folder **without** `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/` and `Builds/`. Unity rebuilds those, and the zip shrinks from GBs to a few MB.
3. **Report:** `Docs/Project_Report.md`. Add your name, institution, screenshots and the test-runner screenshots.
4. **Demo video:** use Android's screen recorder while you place the hoop, shoot, score, miss off the rim, pause and restart (60–90 s).
5. **Test evidence:** screenshots of the Test Runner (EditMode and PlayMode, all green).

---

That completes the project brief's 8 stages. The **timer** and **leaderboard** you planned as extras are described in the report's *Future Enhancements*, and can be built as Stage 9.

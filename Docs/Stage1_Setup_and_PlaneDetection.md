# AR Basketball Shooter – Stage 1: Unity Setup, Plane Detection & Hoop Placement

**Goal of this stage:** the phone camera opens, the floor gets detected, a ring shows where the hoop will go, you tap to place a placeholder hoop, it stays put when you walk around, and a **Reposition Hoop** button lets you move it.

**Not in this stage yet:** the real hoop (Stage 2), the ball and shooting (Stages 3–4), scoring (Stage 5), the full UI (Stage 6), and sound and effects (Stage 7).

Finish the **Stage 1 checklist** (section 12) before you move on.

---

## 1. Recommended versions

| Item | Version | Notes |
|---|---|---|
| Unity Editor | **Unity 6 LTS (6000.0.x, latest patch)** | Install it through Unity Hub with the **Android Build Support** module, plus **OpenJDK** and **Android SDK & NDK Tools**. |
| AR Foundation | **6.0.x** | `com.unity.xr.arfoundation` |
| Google ARCore XR Plugin | **6.0.x** | `com.unity.xr.arcore`. Its **major.minor must match AR Foundation**. |
| XR Plugin Management | 4.5 or newer | Gets installed automatically with ARCore. |
| Input System | 1.11 or newer | Comes with the Unity 6 templates. |
| TextMeshPro | Built into Unity 6 (`com.unity.ugui` 2.x) | You only need to click "Import TMP Essentials" once. |

### How to check package compatibility before installing
1. Open **Window → Package Manager → Unity Registry** and select the package.
2. By default Package Manager offers the version that is **verified/recommended for your Editor**. Use that one and don't pick "latest" by hand.
3. Open **Version History** or **Documentation**. The package docs say which Unity versions they support.
4. Rule of thumb: **AR Foundation and ARCore XR Plugin must have the same major.minor number** (6.0.x with 6.0.x). If you use a newer Unity 6.x LTS, use the AR Foundation 6.x version Package Manager recommends for it, and the ARCore version with the same number.
5. After installing, open **Project Settings → XR Plug-in Management → Project Validation**. If it shows no red errors, your setup is consistent.

> Why Unity 6? It uses **XR Origin**, the current replacement for the old "AR Session Origin", which is deprecated. It also includes **XR Simulation**, which lets you test AR inside the Editor without a phone.

---

## 2. Create the project
1. In Unity Hub, click **New project**, pick the **Universal 3D** template (URP), name it `ARBasketballShooter`, and click **Create**.
2. Create these folders under `Assets/`:
   ```
   Assets/
     Scenes/  Scripts/  Prefabs/  Materials/  Models/  Audio/  Particles/  UI/
   ```
3. Copy `Assets/Scripts/ARPlanePlacement.cs` from this deliverable into your project's `Assets/Scripts/`. The file name and class name must both be `ARPlanePlacement`.

## 3. Switch to Android
**File → Build Profiles → Android → Switch Platform**. In Unity 6 this window is called Build Profiles instead of Build Settings.

## 4. Install AR packages
Go to **Window → Package Manager → Unity Registry** and install:
1. **AR Foundation**
2. **Google ARCore XR Plugin**

## 5. XR Plug-in Management
Open **Edit → Project Settings → XR Plug-in Management**:
- **Android tab** (robot icon): check **Google ARCore**.
- **Desktop tab** (monitor icon): check **XR Simulation**. This is only for testing in the Editor. If Unity asks to import the sample environments, say yes.
- Open **XR Plug-in Management → ARCore**: set **Requirement = Required** and **Depth = Optional**.
- Open **Project Validation**, select the Android tab, and click **Fix All**. Repeat until it shows no errors.

## 6. Player Settings (Android)
Open **Edit → Project Settings → Player → Android tab**:

| Setting | Value |
|---|---|
| Company Name / Product Name | Your name / `AR Basketball Shooter` |
| Other Settings → **Package Name** | `com.yourname.arbasketball` (lowercase, no spaces) |
| **Minimum API Level** | **Android 7.0 'Nougat' (API 24)**, the minimum ARCore needs |
| Target API Level | Automatic (highest installed) |
| **Scripting Backend** | **IL2CPP** |
| **Target Architectures** | **ARM64 only** (uncheck ARMv7). Google Play and ARCore need 64-bit. |
| **Auto Graphics API** | **Unchecked**, then keep only **OpenGLES3** in the list (remove Vulkan). This is the safest choice for ARCore. |
| Application Entry Point | **Activity** checked, **GameActivity** unchecked (avoids ARCore build problems) |
| Active Input Handling | **Input System Package (New)** or **Both** |
| Resolution and Presentation → **Default Orientation** | **Portrait** |

**Camera permission:** the ARCore XR Plugin adds the `CAMERA` permission and the ARCore metadata to the Android manifest for you. On first launch the phone asks the user to allow the camera.

## 7. URP: make the camera image visible (prevents a black screen)
URP doesn't draw the camera image unless you add a renderer feature:
1. In `Assets/Settings/`, select **Mobile_Renderer**. Do the same for **PC_Renderer** to be safe.
2. In the Inspector, click **Add Renderer Feature → AR Background Renderer Feature**.

Project Validation also flags this if it's missing.

---

## 8. Build the scene

### 8.1 Create the scene
**File → New Scene → Basic (URP)**, then save it as `Assets/Scenes/ARBasketballScene.unity`. **Delete the default Main Camera.**

### 8.2 Add AR objects
- **GameObject → XR → AR Session**
- **GameObject → XR → XR Origin (Mobile AR)**

Select **XR Origin (Mobile AR)** and click **Add Component** three times:

| Component | Settings |
|---|---|
| **AR Plane Manager** | **Detection Mode = Horizontal**, **Plane Prefab = ARPlane** (from 8.3) |
| **AR Raycast Manager** | Leave the defaults (Raycast Prefab = None) |
| **AR Anchor Manager** | Leave the defaults (Anchor Prefab = None) |

Leave XR Origin at **Position 0,0,0, Rotation 0,0,0, Scale 1**. Check that **XR Origin → Camera Offset → Main Camera** has the tag **MainCamera** and these components: *Camera, AR Camera Manager, AR Camera Background, Tracked Pose Driver*.

### 8.3 Plane prefab (shows the detected surfaces)
1. **GameObject → XR → AR Default Plane**.
2. Rename it `ARPlane`, drag it into `Assets/Prefabs/`, then delete it from the Hierarchy.
3. Optional: make a semi-transparent material for it in `Materials/`.
4. Keep its **Mesh Collider**. The ball needs it in later stages so it can land on the floor.

### 8.4 Placement indicator prefab (the ring on the floor)
1. **GameObject → Create Empty**, name it `PlacementIndicator`, and set its position to 0,0,0.
2. Right-click it and choose **3D Object → Cylinder** to make a child. Name the child `Disc`. Set **Position (0, 0.002, 0)** and **Scale (0.25, 0.002, 0.25)**.
3. **Remove the Capsule Collider** from `Disc`. A collider here would get in the way later.
4. Make a material `Materials/IndicatorMat` with shader **Universal Render Pipeline/Unlit**, **Surface Type = Transparent**, and a cyan base colour with alpha around 150. Assign it to `Disc`.
5. Drag `PlacementIndicator` into `Assets/Prefabs/` and delete it from the scene.

### 8.5 Placeholder hoop prefab (Stage 2 replaces it with the full hoop)
**The front of the hoop faces +Z (the blue arrow).** The script turns +Z towards the player.

1. **Create Empty**, name it `BasketballHoop`, and set its position to 0,0,0. The pivot sits on the floor.
2. Add these children (3D Objects):

| Child | Type | Position | Scale |
|---|---|---|---|
| Pole | Cylinder | (0, 0.6, 0) | (0.04, 0.6, 0.04) |
| Backboard | Cube | (0, 1.3, 0.03) | (0.6, 0.4, 0.02) |
| RimMarker | Cylinder | (0, 1.15, 0.22) | (0.3, 0.01, 0.3) |

3. Give RimMarker an orange material so you can see which way the hoop faces.
4. Drag `BasketballHoop` into `Assets/Prefabs/` and delete it from the scene.

> Scale: **1 Unity unit = 1 real metre.** This mini hoop is 1.5 m tall, which fits indoors. Place it on the **floor**.

### 8.6 GameSystems object + script
1. **Create Empty**, name it `GameSystems`, and click **Add Component → AR Plane Placement**.
2. Fill in the **Inspector references**:

| Field | Drag in |
|---|---|
| Raycast Manager | XR Origin (Mobile AR) |
| Plane Manager | XR Origin (Mobile AR) |
| AR Camera | XR Origin → Camera Offset → Main Camera |
| Hoop Prefab | Prefabs/BasketballHoop |
| Placement Indicator Prefab | Prefabs/PlacementIndicator |
| Status Text | Canvas/StatusText (from 8.7) |
| Reposition Button | Canvas/RepositionButton (from 8.7) |
| Hide Planes After Placement | ✔ |
| Hoop Yaw Offset | 0 |
| Placement Enabled On Start | ✔ (the GameManager will control this in a later stage) |

If you leave the three AR fields empty, the script finds them automatically. It logs a clear error if something is missing.

### 8.7 Stage 1 UI
1. **GameObject → UI → Canvas**. If Unity asks, click **Import TMP Essentials**.
2. On **Canvas Scaler**, set **UI Scale Mode = Scale With Screen Size**, **Reference Resolution = 1080 × 1920**, and **Match = 0.5**.
3. Select the **EventSystem**. If it shows *"Replace with InputSystemUIInputModule"*, click it.
4. Right-click Canvas → **UI → Text - TextMeshPro** and name it `StatusText`:
   - Anchor **top-stretch**, Pos Y = −120, Height = 150, Font Size = 48, Alignment = centre, white text.
   - The script ignores taps on plain text, but you can uncheck **Raycast Target** anyway.
5. Right-click Canvas → **UI → Button - TextMeshPro** and name it `RepositionButton`:
   - Anchor **bottom-centre**, Pos Y = 150, Size 500 × 140, label "Reposition Hoop", label Font Size = 44.
   - **On Click ()**: click **+**, drag in **GameSystems**, then choose **ARPlanePlacement → RepositionHoop()**.

### 8.8 Final Stage 1 hierarchy
```
ARBasketballScene
├── Directional Light
├── AR Session                     (AR Session, AR Input Manager)
├── XR Origin (Mobile AR)          (XR Origin, AR Plane Manager, AR Raycast Manager, AR Anchor Manager)
│   └── Camera Offset
│       └── Main Camera            (Camera, AR Camera Manager, AR Camera Background, Tracked Pose Driver) [MainCamera]
├── GameSystems                    (ARPlanePlacement)
├── Canvas                         (Canvas, Canvas Scaler, Graphic Raycaster)
│   ├── StatusText
│   └── RepositionButton
└── EventSystem                    (Event System, Input System UI Input Module)

Created at runtime:  PlacementIndicator,  HoopAnchor (ARAnchor) → BasketballHoop,  Trackables → ARPlane…
```

---

## 9. How the script works (ARPlanePlacement.cs)
- **Every frame**, it casts a ray from the centre of the screen with `ARRaycastManager.Raycast(..., TrackableType.PlaneWithinPolygon)`. It only accepts hits on planes whose `alignment == HorizontalUp`, so floors and tables count but walls and ceilings don't. The indicator moves to that spot and turns to face you.
- **On a tap** (`Pointer.current` is the touchscreen on a phone and the mouse in the Editor), it first checks that the tap isn't on a UI button. Then it raycasts at the tap position. If the tap misses a plane, it places the hoop at the indicator instead.
- **Placing** creates a `HoopAnchor` GameObject with an `ARAnchor` component and makes the hoop its child. ARCore keeps the anchor locked to the real-world spot, so the hoop doesn't drift.
- **After placing**, it hides only the plane *renderers*. The plane colliders stay active so the ball can land on the floor later. It also shows the Reposition button.
- **RepositionHoop()** hides the hoop, deletes the anchor, shows the planes again, and turns placement back on. It reuses the same hoop object, so later scripts keep a valid reference to it.
- **How other scripts connect:** later stages subscribe to `HoopPlaced` and `HoopRemoved` and read `IsHoopPlaced` / `HoopTransform`. The GameManager calls `SetPlacementEnabled()`.

---

## 9b. Automated setup tests (run these first)
1. Copy `Assets/Tests/Stage1/` (the `.asmdef` file and `Stage1SetupTests.cs`) into your project.
2. Open **Window → General → Test Runner → EditMode** and click **Run All**.
3. Each red test explains exactly what to fix, for example *"Untick Auto Graphics API"* or *"Assign Hoop Prefab."* Fix it and run again until every test is green.

These tests check the project settings and scene wiring. The camera and plane detection still need the manual tests below.

## 10. Test in the Unity Editor (XR Simulation)
1. Open **Window → XR → AR Foundation → XR Environment** and pick an environment such as *DefaultSimulationEnvironment* or a living room.
2. Press **Play**. Hold the **right mouse button** and use **WASD** to move, and **Q/E** to go down and up. Look at the floor.
3. Planes should appear, and the cyan ring should follow the centre of the Game view.
4. **Left-click** the floor. The hoop appears facing you, the planes hide, and the Reposition button shows up.
5. Move around. The hoop stays fixed. Click **Reposition Hoop**, then click somewhere else. The hoop moves there.
6. Check the **Console** for red errors. There should be none.

## 11. Test on an Android phone
1. Check that your phone is on Google's **ARCore supported devices** list.
2. On the phone, open **Settings → About phone** and tap **Build number** 7 times. Then go to **Developer options** and turn on **USB debugging**.
3. Connect the phone by USB and accept the "Allow USB debugging" prompt.
4. In **File → Build Profiles → Android**, click **Add Open Scenes**, make sure ARBasketballScene is ticked, choose your phone under **Run Device**, and click **Build And Run**. Save the APK as `Builds/ARBasketball_Stage1.apk`.
5. On the phone, allow the camera. If asked, install or update **Google Play Services for AR**. Then point the phone at a textured floor and move it slowly.

## 12. Stage 1 checklist (all must pass before Stage 2)
- [ ] Project Validation shows no errors for Android.
- [ ] Test Runner → EditMode: all `Stage1SetupTests` are green.
- [ ] Editor: Play mode with XR Simulation shows planes and the indicator, and has no Console errors.
- [ ] Phone: the camera image shows (no black screen).
- [ ] Status text changes from "Move your phone slowly…" to "Tap a highlighted surface…".
- [ ] Floor planes appear within about 10 s. Walls are **not** used for placement.
- [ ] The indicator follows the screen centre and lies flat on the floor.
- [ ] Tapping places the hoop with its rim facing you.
- [ ] Tapping the Reposition button does **not** place the hoop behind it.
- [ ] Walk around the hoop: it stays in the same real-world spot.
- [ ] Reposition → tap elsewhere → the hoop moves, and only one hoop exists.

## 13. Troubleshooting (Stage 1)
| Problem | Fix |
|---|---|
| **Black camera screen** | Add the **AR Background Renderer Feature** to the URP renderer that Android uses (section 7). Check that Main Camera has **AR Camera Background**. Use **OpenGLES3** and check that camera permission was granted. |
| **No planes detected** | Use good lighting and a textured floor (plain white or shiny surfaces are hard). Move the phone slowly from side to side. Check that **AR Plane Manager** is enabled, that Detection Mode includes Horizontal, and that Plane Prefab is assigned. |
| **Planes appear but the hoop can't be placed** | Check that Hoop Prefab is assigned (look for a Console error), that **AR Raycast Manager** is on the XR Origin, and that Active Input Handling includes the Input System. In the Editor, use the **left** mouse button. |
| **Tapping does nothing at all** | Check that EventSystem uses **Input System UI Input Module** and that the GameSystems object and script are enabled. Look at the Console or `adb logcat -s Unity`. |
| **Hoop faces the wrong way** | Build the prefab so the rim is on the **+Z** side, or set **Hoop Yaw Offset = 180**. |
| **Hoop drifts or slides** | Check that **AR Anchor Manager** is on the XR Origin and that XR Origin's transform is at the origin. Scan more of the floor before placing. |
| **"Device not supported"** | The phone isn't on the ARCore list, or Google Play Services for AR is missing or old. Update it from the Play Store. Emulators don't run ARCore. |
| **Build errors** | Run Project Validation → Fix All. Use IL2CPP + ARM64 and Min API 24. Make sure the Android SDK/NDK/JDK modules are installed through Unity Hub. If you see Gradle errors, delete the `Library/Bee` folder and rebuild. If you see GameActivity errors, switch to Activity. |

---

**Next: Stage 2.** Build the full basketball hoop prefab (backboard, rim made of colliders, net) and its Physic Materials. The prefab will be called `BasketballHoop` again, so the reference in `ARPlanePlacement` stays the same.

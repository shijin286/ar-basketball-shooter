# AR Basketball Shooter – Stage 3: The Basketball

**Goal:** once the hoop is placed, a real-size basketball (0.24 m, 0.6 kg) appears at the bottom of the screen in front of the player. It has realistic physics: gravity, drag, bounce, and no passing through the rim or floor. A **Reset Ball** button gives you a fresh ball. A temporary **Test Drop** button lets you check the physics before swipe shooting arrives in Stage 4.

**Before you start:** all Stage 1 and Stage 2 tests are green ✔.

---

## 1. Add the files
```
Assets/Scripts/BasketballController.cs        ← on the ball prefab
Assets/Scripts/BallSpawner.cs                 ← on GameSystems
Assets/Scripts/Editor/BasketballPrefabBuilder.cs
Assets/Tests/Stage3/ARBasketball.Tests.Stage3.asmdef
Assets/Tests/Stage3/Stage3BallTests.cs
Assets/Tests/Stage3/Stage3SceneTests.cs
```
`BallSpawner.cs` is a new script that isn't in the original structure. It keeps "creating balls" separate from "being a ball" (`BasketballController`) and from "shooting" (`BasketballShooter`, Stage 4).

Wait until the Console shows **no red errors** before continuing.

## 2. Build the ball prefab
1. Open the menu **Tools → AR Basketball → Create Basketball Prefab**.
2. Keep the defaults and click **Create / Update Prefab**.
3. The tool creates these files:

| Asset | What it is |
|---|---|
| `Prefabs/Basketball.prefab` | The ball (see the structure below) |
| `Materials/Ball/Basketball_Albedo.png` | Procedural orange texture with black seams and a slight leather grain |
| `Materials/Ball/Basketball.mat` | URP Lit, smoothness 0.3 |
| `Materials/Physics/Ball_Physics` | Bounciness 0.8, friction 0.6 |
| `Materials/Physics/Floor_Physics` | Bounciness 0.7, friction 0.6. **Also assigned to `Prefabs/ARPlane`'s Mesh Collider.** |

### Ball prefab structure
```
Basketball                      ← root, scale 1
  Sphere Collider               radius 0.12, material Ball_Physics
  Rigidbody                     mass 0.6, Linear Damping 0.05, Angular Damping 0.3,
                                Use Gravity ✔, Is Kinematic ✘, Interpolate,
                                Collision Detection = Continuous Dynamic
  BasketballController
  └── Visual                    sphere mesh, scale 0.24, material Basketball, no collider
```

### Why these physics settings?
| Setting | Reason |
|---|---|
| **Mass 0.6 kg, 0.24 m** | A real size-7 basketball. The hoop from Stage 2 is real size too, so the ball fits the rim the way a real one does. |
| **Linear Damping 0.05** | A little air resistance. Higher values make shots fall short. |
| **Continuous Dynamic** | A shot can travel 0.3 m per physics step, which is more than the ball's own size. Without continuous detection the ball can pass straight through the thin rim or the backboard ("tunnelling"). |
| **Interpolate** | Physics runs 50 times per second, but the AR camera draws 60 frames per second or more. Interpolation stops the ball from looking jittery. |
| **Bounce combine = Average** | Floor: (0.8 + 0.7) / 2 = 0.75, so the ball bounces to about 55% of its drop height, like a real basketball. Rim: (0.8 + 0.5) / 2 = 0.65. Backboard: 0.7. |
| **Collider on the root, mesh on a scaled child** | The root keeps scale 1, which avoids scaled-physics problems. Effects added in Stage 7 won't be scaled either. |

## 3. Scene setup
### 3.1 BallSpawner
Select **GameSystems**, click **Add Component → Ball Spawner**, and set:

| Field | Value |
|---|---|
| Placement | GameSystems (ARPlanePlacement). Optional; found automatically. |
| AR Camera | Main Camera. Optional; found automatically. |
| **Ball Prefab** | `Prefabs/Basketball` |
| Hold Offset | (0, −0.22, 0.7): ball 0.7 m in front of the camera and 0.22 m below its centre |
| Spawn When Hoop Placed | ✔ |
| Respawn Delay | 0.75 |
| Max Balls In Scene | 4 |
| Create Virtual Floor | ✔ |
| Virtual Floor Size | 20 |
| **Floor Material** | `Materials/Physics/Floor_Physics` |
| **Show When Hoop Placed** | Size 2: `ResetBallButton`, `DropBallButton` (from 3.2) |

### 3.2 Buttons
Rearrange the bottom of the Canvas:

| Button | Anchor | Pos X, Y | Size | Label | On Click () |
|---|---|---|---|---|---|
| `ResetBallButton` (new) | bottom-left | 220, 150 | 380 × 130 | Reset Ball | GameSystems → **BallSpawner.ResetBall** |
| `RepositionButton` (existing) | bottom-right | −220, 150 | 380 × 130 | Reposition Hoop | GameSystems → ARPlanePlacement.RepositionHoop |
| `DropBallButton` (new, temporary) | bottom-centre | 0, 320 | 360 × 110 | Test Drop | GameSystems → **BallSpawner.TestDropBall** |

To create each new button, right-click Canvas → **UI → Button - TextMeshPro**. To set the anchor, click the anchor square in the Rect Transform and hold **Alt + Shift** while you pick it.

> `DropBallButton` is only for testing Stage 3. You'll delete it in Stage 4, when swipe shooting replaces it.

### 3.3 Hierarchy after Stage 3
```
ARBasketballScene
├── Directional Light
├── AR Session
├── XR Origin (Mobile AR)          (XR Origin, AR Plane/Raycast/Anchor Managers)
│   └── Camera Offset
│       └── Main Camera
│           └── BallHoldPoint      ← created at runtime; the held ball is its child
├── GameSystems                    (ARPlanePlacement, BallSpawner)
├── Canvas
│   ├── StatusText
│   ├── ResetBallButton
│   ├── RepositionButton
│   └── DropBallButton  (temporary)
└── EventSystem

Created at runtime when the hoop is placed:
HoopAnchor → BasketballHoop → … + VirtualFloor (Box Collider 20 × 0.2 × 20, Floor_Physics)
Basketball (one per ball)
```

## 4. How the scripts work together
```
ARPlanePlacement ──HoopPlaced(hoop)──▶ BallSpawner ──Instantiate, Initialize, Hold──▶ BasketballController
                 ──HoopRemoved──────▶   │  ▲                                        (one per ball)
                                         │  └──Bounced / Finished──────────────────────┘
       UI: Reset Ball ──▶ ResetBall()    │
       UI: Test Drop  ──▶ TestDropBall() ├──▶ events for later stages:
  Stage 4 Shooter ──▶ LaunchHeldBall()   │    BallSpawned, BallLaunched (Shooter, GameManager)
                                         │    BallBounced (AudioManager, Stage 7)
```
- **BasketballController** has the states **Held → Flying → Finished**.
  - While **Held**, the ball is kinematic, its collider is **off**, and it is a child of `BallHoldPoint`. It moves with the phone and can't hit or score anything.
  - `Launch(impulse, spin)` works only once per ball. It switches physics on and applies `Rigidbody.AddForce(impulse, ForceMode.Impulse)`, so the velocity change equals impulse ÷ mass.
  - A flying ball removes itself when any of these happens: it rests for 1.5 s, it's in the air for more than 8 s, it falls 2 m below the floor, or it goes more than 25 m away.
  - `TryMarkScored()` returns *true* only the first time, so Stage 5 can never give one ball two points.
- **BallSpawner** keeps a list of balls. After a shot it gives you a new ball after 0.75 s, and it removes the oldest ball if there would be more than 4. On **Reset Ball** it clears everything and spawns a fresh ball. When the hoop is repositioned it removes all balls.
- **Virtual floor:** ARCore only detects *part* of your floor. Outside that area, the ball would fall forever. BallSpawner therefore adds a large invisible floor collider at the hoop's base. It is a child of the hoop, so it follows the AR anchor.

## 5. Test Stage 3
### 5.1 Automated (Test Runner → EditMode → Run All)
All Stage 1, 2 and 3 tests must be green:

| Test | What it proves |
|---|---|
| `Prefab_RigidbodyIsConfigured` | Mass, gravity, Continuous Dynamic, Interpolate, damping |
| `Prefab_IsRealBasketballSize` | Collider radius 0.12 m, visual matches, bouncy physics material |
| `Prefab_HasControllerAndTexture` | Script and texture are present |
| `ARPlanePrefab_UsesFloorPhysics` | The real AR floor bounces |
| `Ball_BouncesRealisticallyOnFloor` | A drop from 1.5 m bounces back to 35–85% of the height |
| `Ball_ComesToRestOnFloor` | The ball doesn't fall through the floor and stops bouncing |
| `Ball_FastShotDoesNotPassThroughBackboard` | A 15 m/s ball doesn't tunnel through the board |
| `Ball_GoodShotGoesThroughHoop` | A normal 2.5 m shot drops through the rim (so scoring is possible in Stage 5) |
| `Controller_HoldMakesBallKinematicAndHarmless` | A held ball can't hit or score anything |
| `Controller_LaunchAppliesImpulseExactlyOnce` | Launch gives the right speed and works only once |
| `Controller_BallCanScoreOnlyOnce` | The one-point-per-ball rule |
| `BallSpawner_IsWired`, `ResetBallButton_CallsResetBall`, `DropBallButton_CallsTestDropBall`, `BallButtons_AreShownOnlyAfterPlacement` | Scene wiring |

### 5.2 XR Simulation (Editor)
1. Press Play and place the hoop.
2. **Pass if:**
   - a basketball appears at the bottom-centre of the Game view, with visible seams;
   - Reset Ball and Test Drop appear only now;
   - the ball moves with the camera (hold right mouse and move).
3. Click **Test Drop**. The ball falls in front of you, bounces a few times with each bounce lower, rolls, then disappears after resting. A new ball appears in your hand about 0.75 s after the drop.
4. Click Test Drop 5 times quickly. You never see more than 4 balls.
5. Click **Reset Ball**. All balls disappear and one fresh ball is in your hand.
6. Click **Reposition Hoop**. All balls disappear. Place the hoop again and you get a new ball.
7. Walk next to the hoop and Test Drop **outside the detected plane**. The ball still lands on the virtual floor.
8. The Console has no errors.

### 5.3 Phone
Build And Run (Stage 1 guide, section 11), then repeat 5.2:
- The ball at the bottom of the screen shouldn't hide the hoop. If it's too big, raise Hold Offset z to 0.8–0.9.
- Dropped balls land on the **real** floor, not above or below it.
- The ball looks smooth (no jitter) while you move the phone.

## 6. Stage 3 checklist
- [ ] The Create Basketball Prefab tool runs with no errors.
- [ ] All Stage 1, 2 and 3 EditMode tests are green.
- [ ] The ball appears only after the hoop is placed.
- [ ] The ball follows the phone while held.
- [ ] Test Drop makes the ball fall, bounce realistically and come to rest on the floor.
- [ ] A new ball appears automatically after a drop, with at most 4 balls at once.
- [ ] Reset Ball and Reposition Hoop clear the balls correctly.
- [ ] Works the same on the phone.

## 7. Troubleshooting (Stage 3)
| Problem | Fix |
|---|---|
| **The ball falls through the floor** | Check that **Create Virtual Floor** is ✔ and that ARPlane still has its **Mesh Collider**. The ball needs **Continuous Dynamic**; run the tool again if it doesn't have it. Make sure nothing scales the ball root. |
| No ball appears after placing | Check the Console for *"Ball Prefab is not assigned"*. Spawn When Hoop Placed must be ✔ and GameSystems must be active. |
| Ball is pink | It's missing the URP shader. Run the tool again in a URP project. |
| Ball is invisible but exists | Hold Offset z is smaller than the camera's Near Clip Plane (0.1). Keep z at 0.5 or more. |
| Ball looks huge on screen | Increase Hold Offset z (further away) or make y more negative (lower on screen). Don't shrink the ball, or it won't match the hoop. |
| Ball jitters while held | Don't move the BallHoldPoint from your own script. The ball must stay a child of the camera. |
| Ball jitters while flying | Interpolation must be **Interpolate** (the controller sets it on launch). |
| Ball never disappears | It's still bouncing or rolling. Increase Linear/Angular Damping slightly, or lower Rest Speed. |
| Buttons don't respond | Check that EventSystem uses Input System UI Input Module (Stage 1 test). Check that the buttons aren't hidden behind StatusText (StatusText Raycast Target ✘). |
| Tests can't find `BasketballController.Launch` | You have an old copy of the script. Copy the Stage 3 version again. |

---

**Next: Stage 4, swipe shooting.** `BasketballShooter.cs` reads the swipe (direction, length and speed), turns it into a clamped impulse with ForceMode.Impulse, adds backspin, allows only one shot per swipe, and shows an aim hint. The Test Drop button gets removed.

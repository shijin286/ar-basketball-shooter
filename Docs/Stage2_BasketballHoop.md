# AR Basketball Shooter – Stage 2: The Basketball Hoop

**Goal:** replace the Stage 1 placeholder with a proper hoop that has a backboard, a rim made of colliders, a net and Physic Materials. The ball must be able to bounce off the rim and backboard and drop through the net.

**Before you start:** all Stage 1 automated tests pass (see *How testing works* below), and placing the hoop works in XR Simulation.

---

## How testing works (every stage)
Unity isn't installed on the PC where this code was written, so the code hasn't been compiled or run there. Each stage therefore comes with **automated tests** that you run in your own Unity Editor:

1. Open **Window → General → Test Runner** and select the **EditMode** tab.
2. Click **Run All**. Every test should show a green tick ✔.
3. If a test fails, click it. The message at the bottom tells you exactly what to fix, for example *"Assign Hoop Prefab."*

| Stage | Test file | What it checks |
|---|---|---|
| 1 | `Assets/Tests/Stage1/Stage1SetupTests.cs` | Android target, ARCore enabled, Player Settings, URP AR background feature, scene objects, AR managers, camera, script references, button wiring, EventSystem |
| 2 | `Assets/Tests/Stage2/Stage2HoopTests.cs` | Hoop prefab structure, plus **real physics**: a ball through the centre falls through the net, a ball on the rim bounces, a ball at the backboard bounces back |

The tests can't check the phone camera or real plane detection, so each stage still ends with a short manual test in XR Simulation and on the phone.

> The tests need the **Test Framework** package, which Unity 6 templates include. If the Test Runner window is missing, install **Test Framework** from Package Manager.

> **Command-line run (optional):** this is what I would run if Unity were installed on this PC:
> ```bash
> "C:/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe" -batchmode -projectPath "<your project>" -runTests -testPlatform EditMode -testResults results.xml -logFile unity.log
> ```

---

## 1. Add the files
Copy these files into your project. Keep the folder names exactly as shown:
```
Assets/Scripts/Editor/HoopPrefabBuilder.cs   ← must be inside a folder named "Editor"
Assets/Tests/Stage2/ARBasketball.Tests.Stage2.asmdef
Assets/Tests/Stage2/Stage2HoopTests.cs
```
`HoopPrefabBuilder` is an **Editor tool**. It isn't attached to any GameObject and isn't included in the APK.

## 2. Build the hoop prefab
1. Delete the Stage 1 placeholder `Assets/Prefabs/BasketballHoop.prefab`. Its contents get replaced; if you prefer, keep it and the tool overwrites it.
2. Open the menu **Tools → AR Basketball → Create Hoop Prefab**.
3. Keep the default values and click **Create / Update Prefab**.
4. The Console shows something like *"Saved Assets/Prefabs/BasketballHoop.prefab. Rim centre is 1.60 m high and 0.71 m in front of the pole."*
5. Check that **GameSystems → ARPlanePlacement → Hoop Prefab** still points to `BasketballHoop`. If you deleted the old prefab, drag the new one in.

### Builder settings (all in metres)
| Field | Default | Meaning |
|---|---|---|
| Rim Height | 1.6 | Rim height above the floor. A real hoop is 3.05 m, which is too high indoors. |
| Rim Inner Diameter | 0.45 | Real size. The ball (Stage 3) is 0.24 m. |
| Rim Tube Radius | 0.012 | Thickness of the rim. Slightly thick so fast balls don't slip through. |
| Rim Distance From Board | 0.15 | Gap from the board to the inner edge of the rim. Real size. |
| Backboard Size / Thickness | 1.2 × 0.75 / 0.03 | A smaller board than the real 1.83 × 1.07 m. |
| Net Length | 0.4 | Visual only. |
| Rim Collider Segments | 16 | Number of capsules that form the rim ring. |
| Rim / Backboard Bounciness | 0.5 / 0.6 | Values for the Physic Materials. |

You can change any value and click the button again. The prefab updates in place.

## 3. What the tool creates
```
BasketballHoop                  (Rigidbody: Is Kinematic ✔, Use Gravity ✘)  ← pivot on the floor, front = +Z
├── Stand
│   ├── Base                    (Box Collider)
│   ├── Pole                    (Capsule Collider)
│   └── Arm                     (Box Collider)
├── Backboard                   (empty, at board centre)
│   ├── BoardPanel              (Box Collider + Backboard_Physics)
│   ├── Border                  (4 red lines, no colliders)
│   └── ShooterSquare           (4 red lines, no colliders)
└── Rim                         ← exactly at the centre of the hoop opening (Stage 5 puts the score triggers here)
    ├── RimVisual               (torus mesh, no collider)
    ├── RimColliders
    │   └── RimSegment_00 … _15 (Capsule Collider + Rim_Physics, direction X, radius 0.012)
    ├── Bracket                 (Box Collider, joins the rim to the board)
    └── Net                     (diamond-pattern mesh, NO collider)
```
It also creates these assets:
- `Materials/Hoop/Hoop_Backboard, Hoop_BoardLines, Hoop_Rim, Hoop_Net, Hoop_Pole` (URP Lit)
- `Materials/Physics/Rim_Physics, Backboard_Physics` (Physic Materials, combine = Average)
- `Models/Generated/RimTorus, HoopNet` (meshes)

### Why it is built this way
- **The rim is a ring of 16 capsule colliders, not one Mesh Collider.** A Mesh Collider of a torus is *concave*, and concave Mesh Colliders are slow and unreliable for fast balls. Capsules are cheap and smooth, and they leave the centre of the ring empty so the ball can pass through.
- **The net has no collider.** The ball falls straight through it. In Stage 5, trigger colliders inside the rim detect the basket. Because touching the rim or backboard only hits *solid* colliders, it can never count as a score.
- **Kinematic Rigidbody on the root.** The AR anchor moves the hoop slightly as ARCore improves its tracking. Unity's rule is that moving colliders should have a kinematic Rigidbody, which keeps physics stable and fast.
- **Physic Materials:** the rim bounce is 0.5 and the backboard bounce is 0.6. The ball in Stage 3 gets 0.8, and with the *Average* combine mode a rim hit gives about 0.65, which feels realistic.
- **Front = +Z.** `ARPlanePlacement` turns the hoop's +Z towards the player, so the rim always faces you.

## 4. Test Stage 2
### 4.1 Automated (Test Runner → EditMode → Run All)
All **Stage 1 and Stage 2** tests must be green:
- `Root_HasKinematicRigidbody`, `Rim_ExistsInFrontAndAboveFloor`, `Rim_HasSolidCapsuleRing`, `Rim_OpeningFitsTheBall`, `Net_HasNoCollider`, `Backboard_HasSolidCollider`
- `Ball_DroppedThroughCentre_FallsThroughNet`: a clean shot isn't blocked.
- `Ball_DroppedOnRim_BouncesOff`: the rim is solid.
- `Ball_ThrownAtBackboard_BouncesBack`: the board is solid.

The physics tests create temporary objects 1 km away in your open scene and delete them afterwards. If Unity marks the scene as modified (`*`), that's fine.

### 4.2 Look at it
Double-click `Prefabs/BasketballHoop` to open Prefab Mode. With the Move tool in **Local** mode, check:
- The blue arrow (Z) points from the board towards the rim.
- The pivot is at the floor under the pole.
- Select `RimColliders`: the green capsule outlines form a closed ring.

### 4.3 In AR (XR Simulation, then the phone)
1. Press Play in XR Simulation and place the hoop on the floor.
2. **Pass if:** the rim faces you, the hoop stands on the floor (not floating or sunk), and its size looks right (rim about chest height).
3. Walk around it with WASD. It stays in place, and the net and red lines on the board are visible.
4. Build to the phone (Stage 1 guide, section 11) and repeat. Check from about 2–3 m away that the size feels right.

## 5. Stage 2 checklist
- [ ] The Create Hoop Prefab tool runs with no Console errors.
- [ ] All Stage 1 + Stage 2 EditMode tests are green.
- [ ] Hoop Prefab is assigned in ARPlanePlacement.
- [ ] In XR Simulation the hoop is placed on the floor with the rim facing you.
- [ ] On the phone the hoop looks the right size and stays anchored while you walk around.
- [ ] Reposition Hoop still works.

## 6. Troubleshooting (Stage 2)
| Problem | Fix |
|---|---|
| Menu *Tools → AR Basketball* is missing | The script must be in a folder named `Editor` and the Console must have no compile errors. |
| Error: `PhysicsMaterial` not found | You're on Unity 2022 or older. Use Unity 6, or rename `PhysicsMaterial` → `PhysicMaterial` and `PhysicsMaterialCombine` → `PhysicMaterialCombine`. |
| Hoop is pink | The URP Lit shader wasn't found. Make sure the project uses URP, then run the tool again. |
| Rim faces away from you | Set **Hoop Yaw Offset = 180** in ARPlanePlacement. That only happens if you built your own model with the rim on −Z. |
| Hoop floats or sinks | Check that the prefab root's pivot is at y = 0 under the pole, and scan the floor longer before placing. |
| Too big or small for your room | Change Rim Height or Backboard Size in the tool and click Create again. Don't scale the prefab root, because scaling changes the physics. |
| `Ball_DroppedOnRim_BouncesOff` fails | Rim Tube Radius is too small or Rim Collider Segments is too low. Use 0.012 and 16. |
| `Ball_DroppedThroughCentre` fails | Something has a collider in the opening. Make sure `Net` has no collider and that Rim Inner Diameter ≥ 0.40. |

---

**Next: Stage 3, the basketball.** A prefab with a Rigidbody (0.6 kg, Continuous Dynamic), Sphere Collider and Ball Physic Material; `BasketballController.cs` spawns it in front of the camera, plus a Reset Ball button and Stage 3 tests.

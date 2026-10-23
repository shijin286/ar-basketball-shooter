# AR Basketball Shooter – Stage 5: Basket Detection & Scoring

**Goal:** a ball that goes **down through the hoop** earns **1 point**, and the score appears on screen immediately with a short "SWISH!" message. Shots are counted too.

These **never** score:
- a ball that only touches the rim or backboard;
- a ball that bounces around on the rim and pops back out;
- a ball that comes up through the net from below;
- the same ball a second time.

**Before you start:** all Stage 1–4 tests are green ✔.

---

## 1. Add the files
```
Assets/Scripts/HoopScoreTrigger.cs                    ← on the hoop's Rim/ScoreZone (added by the builder)
Assets/Scripts/GameManager.cs                         ← on GameSystems
Assets/Scripts/UIManager.cs                           ← on the Canvas
Assets/Scripts/Editor/HoopPrefabBuilder.cs            ← UPDATED (now adds the ScoreZone)
Assets/Tests/Stage5/EditMode/ARBasketball.Tests.Stage5.asmdef
Assets/Tests/Stage5/EditMode/Stage5ScoringSetupTests.cs
Assets/Tests/Stage5/PlayMode/ARBasketball.Tests.Stage5.PlayMode.asmdef
Assets/Tests/Stage5/PlayMode/Stage5ScoringPlayTests.cs
```
Copy `HoopScoreTrigger.cs` **together with** the updated `HoopPrefabBuilder.cs`, because the builder uses it. Wait for **no red errors** in the Console.

## 2. Rebuild the hoop
Run **Tools → AR Basketball → Create Hoop Prefab → Create / Update Prefab**. The prefab updates in place, so all references keep working. It now has:
```
BasketballHoop
└── Rim
    ├── RimVisual, RimColliders, Bracket, Net   (as before)
    └── ScoreZone        ← NEW: Box Collider (Is Trigger ✔), center (0, −0.1, 0), size (0.12, 0.4, 0.12)
                            HoopScoreTrigger (Exit Below Rim 0.02, Max Pass Time 2)
```
The zone is a **narrow** box (12 cm) **inside** the ring, from 10 cm above the rim to 30 cm below it, into the net.

Why a narrow *box*?
- A ball resting on or bouncing off the rim has its centre about 24 cm from the middle, so it can't reach a 12 cm box.
- It's a box rather than a capsule so the Stage 2 rim tests (which check that every capsule under the rim is solid) still pass.

## 3. Scene setup
### 3.1 GameManager
**GameSystems → Add Component → Game Manager.** Placement and Shooter are found automatically. Points Per Basket = 1.

### 3.2 HUD texts (Canvas)
| Object (UI → Text - TextMeshPro) | Anchor | Pos | Size | Font | Notes |
|---|---|---|---|---|---|
| `ScoreText` | top-left | (40, −60), pivot (0, 1) | 450 × 100 | 60, bold, left | Text "Score: 0" |
| `ShotsText` | top-right | (−40, −60), pivot (1, 1) | 450 × 100 | 48, right | Text "Shots: 0" |
| `BasketMessage` | middle-centre | (0, 150) | 900 × 220 | 130, bold, centre, orange-yellow with outline | Text "SWISH!" (hidden at start) |
| `StatusText` (existing) | top-stretch | **move to Pos Y −200** | | | Makes room for the score |

For all three new texts, **uncheck Raycast Target**.

### 3.3 UIManager
**Canvas → Add Component → UI Manager** and assign:

| Field | Value |
|---|---|
| Game Manager | GameSystems (optional; found automatically) |
| Score Text | `ScoreText` |
| Shots Text | `ShotsText` |
| Basket Message | `BasketMessage` |
| Message Duration | 1.2 |
| Success Messages | SWISH!, BASKET!, Nice shot!, +1 |

### Hierarchy after Stage 5
```
GameSystems    (ARPlanePlacement, BallSpawner, BasketballShooter, GameManager)
Canvas         (Canvas, Canvas Scaler, Graphic Raycaster, UIManager)
├── ScoreText        (new)
├── ShotsText        (new)
├── BasketMessage    (new)
├── StatusText       (moved down)
├── ShootHintText
├── ResetBallButton
└── RepositionButton
```

## 4. How scoring works
```
ball enters ScoreZone ──▶ HoopScoreTrigger.OnTriggerEnter
   moving UP and below the rim?      → remember "came from below" (this ball can never score)
   moving DOWN and above the rim?    → remember "entered from above" + time
ball leaves ScoreZone ──▶ HoopScoreTrigger.OnTriggerExit
   entered from above  AND  centre now below the rim  AND  still moving down  AND  within 2 s
   AND ball.TryMarkScored() == true (first time only)
        └──▶ BasketScored ──▶ GameManager.RegisterBasket ──▶ Score +1
                                   ├──▶ BasketScored  ──▶ UIManager: "SWISH!" + score punch
                                   └──▶ ScoreChanged  ──▶ UIManager: "Score: 3"  (same frame)
BasketballShooter.ShotFired ──▶ GameManager.RegisterShot ──▶ Shots +1 ──▶ ScoreChanged ──▶ "Shots: 7"
```
| Rule from the brief | How it's guaranteed |
|---|---|
| Ball passes through from above | Must *enter* above the rim moving down, and *leave* below the rim moving down |
| One point per basket | `pointsPerBasket = 1` |
| Each ball scores at most once | `BasketballController.TryMarkScored()` returns true only the first time (Stage 3) |
| Not from below | A ball entering the zone upward is put on a block list for good |
| Not bouncing around inside | Popping out of the top (still above the rim) doesn't count. Only leaving *below* the rim counts. |
| Rim/backboard touch ≠ basket | Those colliders are solid, and only ScoreZone is a trigger. A ball touching the rim is too far from the narrow zone. |
| A ball in the hand can't score | Held balls have their collider switched off (Stage 3) |
| Score shown immediately | `ScoreChanged` fires in the same physics step as the basket, and UIManager writes the text right away |

The GameManager finds the hoop's HoopScoreTrigger when the hoop is placed. The hoop is created at runtime, so it can't be dragged in by hand.

## 5. Test Stage 5
### 5.1 EditMode tab → Run All (Stages 1–5)
| Test | What it proves |
|---|---|
| `ScoreZone_IsTriggerWithScript` | The trigger and script are in the right place |
| `ScoreZone_IsNarrowEnoughThatRimTouchesDontCount` | The zone width is safe |
| `ScoreZone_ReachesAboveAndBelowRim` | The zone covers the path through the rim |
| `ScoreZone_IsTheOnlyTriggerOnTheHoop` | The rim and backboard are solid |
| `GameManager_CountsShotsAndBaskets` | 3 shots + 1 basket = Score 1, Shots 3, Accuracy 33%. Reset sets everything to 0. |
| `GameManager_ReportsScoreChangeImmediately` | An event fires right after every shot and basket |
| `UIManager_WritesScoreAndShots` | The screen text reads "Score: 3" and "Shots: 7" |
| `Scene_HasGameManagerAndWiredUI` | Scene wiring |

The Stage 2–4 tests must also still be green, now with the rebuilt hoop.

### 5.2 PlayMode tab → Run All (new, takes about 20 s)
Open **Window → General → Test Runner**, choose the **PlayMode** tab, and click **Run All**. Unity enters Play mode by itself and fires real balls at a real hoop:

| Test | Expected result |
|---|---|
| `CleanShot_ScoresOnePoint` | 1 point |
| `BallDroppedThroughCentre_ScoresOnePoint` | 1 point |
| `TwoBalls_ScoreTwoPoints` | 2 points |
| `BallFromBelow_NeverScores` | 0. The ball flies up through the net, out of the top, then falls back through the hoop. |
| `RimTouch_BouncesOutAndDoesNotScore` | 0, and the test checks that the ball really bounced |
| `BackboardHit_DoesNotScore` | 0, and the test checks that the ball really bounced |
| `SameBall_CannotScoreTwice` | Still 1 after the same ball goes through a second time |
| `HeldBall_InsideHoop_DoesNotScore` | 0 |

### 5.3 XR Simulation
1. Press Play, place the hoop, and stand about 2.5 m away. The HUD shows **Score: 0** and **Shots: 0**.
2. Shoot and miss. Shots goes to 1, Score stays 0.
3. Score a basket (set Aim Assist to 1 for this test). **"SWISH!"** (or another message) pops up, the Score text pulses, and **Score: 1** appears as the ball drops through the net.
4. Hit only the rim or only the backboard. **No point.**
5. Shoot several baskets in a row. Each counts **exactly once**.
6. Click Reposition Hoop, place it again, and score. The score keeps counting. Restart arrives in Stage 6.

### 5.4 Phone
Repeat 5.3 on the device. Check that the score texts are readable and not hidden by the phone's notch. A safe-area fix comes in Stage 6.

## 6. Stage 5 checklist
- [ ] The hoop prefab was rebuilt and has `Rim/ScoreZone`.
- [ ] EditMode tests: Stages 1–5 are green (only Stage 3's Test Drop test is skipped).
- [ ] PlayMode tests: all 8 Stage 5 tests are green.
- [ ] A basket gives exactly 1 point and the score updates instantly with a message.
- [ ] Rim/backboard touches, balls from below and repeat passes don't score.
- [ ] Shot count goes up on every shot.
- [ ] Works on the phone.

## 7. Troubleshooting (Stage 5)
| Problem | Fix |
|---|---|
| **The ball passes through the rim without scoring** | Rebuild the hoop prefab so ScoreZone exists. ScoreZone must have **Is Trigger ✔** and a **HoopScoreTrigger**. Check the Console for *"The hoop has no HoopScoreTrigger"*. GameManager must be on GameSystems. The ball must have a Rigidbody (triggers only fire for Rigidbodies). |
| **Multiple points for one shot** | Make sure there's only **one** GameManager and one HoopScoreTrigger, and that you didn't add a second trigger script to the hoop. Make sure BasketballController is the Stage 3 version (it has `TryMarkScored`). |
| Score changes but the screen doesn't | Assign UIManager > Score Text. UIManager must be on an **active** object (Canvas). |
| "SWISH!" never shows | Assign Basket Message. The text object may be inactive in the editor; UIManager switches it on. |
| A fast shot sometimes isn't counted | Leave the ScoreZone height at 0.4 m. A shorter zone can be skipped in one physics step. If needed, set Project Settings → Time → Fixed Timestep to 0.01. |
| PlayMode tests don't appear | They're in the **PlayMode** tab, not EditMode. The PlayMode asmdef must have no platforms ticked (Any Platform). |
| PlayMode tests fail with "prefab missing" | Run both builders (hoop and ball) first, so the prefabs exist at `Assets/Prefabs/`. |

---

**Next: Stage 6, the complete game UI.**
- A title screen with **Start Game**, and "Place Hoop" and "Aim and Shoot" instructions.
- **Pause/Resume** and **Restart Game**.
- Score, shots and accuracy, all built on this GameManager and UIManager.
- Safe-area and screen-size fixes, plus button press animations.

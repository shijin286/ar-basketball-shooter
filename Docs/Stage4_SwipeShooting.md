# AR Basketball Shooter – Stage 4: Touch & Swipe Shooting

**Goal:** swipe up on the screen to shoot the ball in your hand.
- The **speed** of the swipe sets the power.
- The **sideways angle** of the swipe aims left or right.
- The shot uses `Rigidbody.AddForce(…, ForceMode.Impulse)`, with the force clamped between a minimum and a maximum.
- One swipe = one shot, never more.
- An adjustable **aim assist** makes the game fun for beginners and reliable for your demo.

**Before you start:** all Stage 1–3 tests are green ✔ and Test Drop works.

---

## 1. Add the files
```
Assets/Scripts/BasketballShooter.cs   ← on GameSystems
Assets/Scripts/ShotMath.cs            ← plain C# (static class), not attached to anything
Assets/Scripts/SwipeGesture.cs        ← plain C# class, created by BasketballShooter
Assets/Tests/Stage4/ARBasketball.Tests.Stage4.asmdef
Assets/Tests/Stage4/Stage4ShootingTests.cs
Assets/Tests/Stage4/Stage4SceneTests.cs
```
`ShotMath` and `SwipeGesture` are new helper files that aren't in the original structure. Keeping the maths and the swipe tracking out of the MonoBehaviour means they can be tested without a phone.

## 2. Scene changes
1. **GameSystems → Add Component → Basketball Shooter.** The defaults are tuned, so you only need to set **Hint Text** (step 3).
2. **Delete `DropBallButton`** from the Canvas.
3. Right-click Canvas → **UI → Text - TextMeshPro** and name it `ShootHintText`:
   - Anchor bottom-stretch, Pos Y = 330, Height = 90, Font Size = 44, Alignment centre, white with an outline (Material Preset → *LiberationSans SDF - Outline*).
   - **Uncheck Raycast Target.**
4. **GameSystems → Basketball Shooter → Hint Text** = `ShootHintText`.
5. **GameSystems → Ball Spawner → Show When Hoop Placed:**
   - remove the empty element (it was DropBallButton);
   - add `ShootHintText`.
   - The final list is `ResetBallButton`, `ShootHintText`.

### BasketballShooter Inspector (defaults)
| Group | Field | Default | Meaning |
|---|---|---|---|
| References | Spawner / Placement | (auto) | Found automatically on GameSystems |
| Swipe | Min Swipe Length | 0.06 | The finger must move up at least 6% of the screen height |
| | Max Swipe Duration | 1.2 s | Slower drags are ignored, so a resting finger never fires |
| | Max Swipe Angle | 60° | Swipes more sideways than this are ignored |
| | Min / Max Swipe Speed | 0.6 / 3.0 | Screen heights per second that give 0% / 100% power |
| | Shot Cooldown | 0.35 s | Minimum time between shots |
| Shot | **Min / Max Shot Force** | **1.5 / 5.4 N·s** | **The clamp.** With a 0.6 kg ball that's 2.5–9 m/s. |
| | Launch Angle | 55° | Arc of the shot |
| | Aim Sensitivity | 0.5 | Half of the swipe's sideways angle becomes the aim |
| | Max Aim Angle | 20° | Largest left/right turn |
| | Backspin | 15 rad/s | Real shooters' backspin; softens rim bounces |
| Aim assist | **Aim Assist** | **0.6** | 0 = pure skill, 1 = every valid swipe towards the hoop scores |
| | Assist Max Angle | 30° | Assist helps only if you already aim within 30° of the hoop |
| | Rim Target Height | 0.05 m | Aims just above the rim centre so the ball drops in |
| UI | Hint Text | ShootHintText | Shows "Swipe up to shoot", "Release to shoot!" and "Shot! Power 72%" |

### Hierarchy after Stage 4
```
GameSystems         (ARPlanePlacement, BallSpawner, BasketballShooter)
Canvas
├── StatusText
├── ShootHintText   (new)
├── ResetBallButton
└── RepositionButton            ← DropBallButton deleted
```

## 3. How a swipe becomes a shot
```
finger down ──▶ SwipeGesture.Begin()      (ignored if it starts on a button, or on the tap that placed the hoop)
finger moves ─▶ SwipeGesture.Move()       (keeps the last 0.1 s for flick speed)
finger up ────▶ SwipeGesture.End()  ──true only ONCE──▶ BasketballShooter.HandleSwipe()
                                                          │
  ShotMath.IsValidSwipe ── up ≥ 6%? ≤ 1.2 s? ≤ 60° sideways?  no → hint "Swipe UP to shoot"
                                                          │ yes
  power     = ShotMath.SwipeSpeedToPower(speed)            0..1
  force     = Lerp(Min Shot Force, Max Shot Force, power)  N·s
  direction = ShotMath.ShotDirection(camera, swipe)        flat camera forward, turned by swipe, tilted 55°
  velocity  = direction × force / mass
  aim assist: ideal = ShotMath.TryGetIdealVelocity(ball → rim)
              if within 30°: velocity = Lerp(velocity, ideal, Aim Assist)
  impulse   = ShotMath.ClampImpulse(velocity × mass, Min, Max)        ← clamp
  spin      = ShotMath.BackspinImpulse(...)
                                                          │
  BallSpawner.LaunchHeldBall(impulse, spin)
     └▶ BasketballController.Launch()  → Rigidbody.AddForce(impulse, ForceMode.Impulse)
  → ShotFired event (GameManager counts shots in Stage 5/6), new ball in hand after 0.75 s
```

### Why one swipe can never fire twice (three safety locks)
1. `SwipeGesture.End()` returns *true* only once per touch-down.
2. After `Launch()` the ball isn't *Held* any more, and `Launch()` refuses a second call.
3. **Shot Cooldown** (0.35 s) and the **0.75 s** respawn delay leave no ball to shoot right away.

### The "perfect shot" maths (aim assist)
For a launch angle θ, horizontal distance d and height difference h, the textbook projectile formula gives the speed needed:

  v² = g·d² / (2·cos²θ·(d·tanθ − h))

That gives the flight time T = d / (v·cosθ). The ball has a little air drag (Linear Damping k = 0.05), so `ShotMath.VelocityForFlightTime` solves the drag equation  x(T) = g·T/k + (v₀ − g/k)(1 − e^(−kT))/k  for v₀. That way the ball still arrives at the rim.

### Tuning the feel
| You want… | Change |
|---|---|
| A demo where almost every shot scores | Aim Assist = 0.9–1.0 |
| A real skill game | Aim Assist = 0–0.3 |
| Shots that are too weak even with fast swipes | Lower Max Swipe Speed (e.g. 2.2) |
| Gentle swipes that fire too hard | Raise Min Swipe Speed |
| A higher arc | Launch Angle 60° |

## 4. Test Stage 4
### 4.1 Automated (Test Runner → EditMode → Run All)
All Stage 1–4 tests must be green. One exception: Stage 3's `DropBallButton_CallsTestDropBall` now shows as **skipped/ignored**. That's expected, because the button was deleted.

| Test | What it proves |
|---|---|
| `Swipe_StraightUp_IsValid`, `Swipe_SlightlyRight_IsValid`, `Swipe_Down/TooShort/TooSlow/TooSideways_IsIgnored` | Swipe rules |
| `ShotMath_SwipeSpeedGivesPowerBetween0And1` | Power mapping |
| `ShotMath_ClampsForceButKeepsDirection` | **Min/max force clamp** |
| `ShotMath_StraightSwipeShootsForwardAndUp`, `ShotMath_SidewaysSwipeTurnsShotButNotTooMuch`, `ShotMath_PhoneFacingFloorStillShootsForward` | Direction and aiming |
| `ShotMath_ImpossibleTargetHasNoIdealShot`, `ShotMath_BackspinSpinsTopTowardsPlayer` | Edge cases, spin direction |
| `SwipeGesture_OneSwipeGivesOnlyOneShot`, `SwipeGesture_CancelledSwipeGivesNoShot` | **One shot per swipe**, and buttons don't shoot |
| `SwipeGesture_MeasuresFinalFlickSpeed` | A quick flick at the end counts, even after a pause |
| `PerfectShot_Scores_From1_5m / 2_5m / 4m / FromTheSide` | With full aim assist, the real ball and hoop prefabs score from every distance, and the needed force is inside the clamp |
| `BasketballShooter_IsInScene`, `TestDropButton_IsRemoved`, `BallSpawner_HasNoEmptyUiSlots` | Scene wiring |

### 4.2 XR Simulation (Editor)
In the Game view, a **left-mouse drag** is a swipe. Holding the right mouse button still moves the camera.

1. Place the hoop and stand about 2–3 m in front of it. The hint reads **"Swipe up to shoot"**.
2. Drag the mouse **up quickly** and release. The ball flies in an arc towards the hoop. The hint shows **"Shot! Power …%"**, and a new ball appears after about 0.75 s.
3. Do a slow, short drag. **No shot**, and the hint says to swipe longer and quicker.
4. Drag **down**. **No shot**, and the hint says "Swipe UP to shoot".
5. Drag up-left, then up-right. The ball curves its aim left or right.
6. Click **Reset Ball** and **Reposition Hoop**. Neither fires a shot.
7. Set Aim Assist to 1 in Play mode and shoot 10 times from different spots. Almost every ball should go through the rim. Set it back to 0.6. (Changes made in Play mode are lost when you stop, which is fine here.)
8. Do one long swipe that sweeps up and down again. **Only one shot.**

### 4.3 Phone
Build And Run, then repeat 4.2 with your thumb:
- Quick flicks feel responsive, and the power matches how fast you flick.
- Touching the buttons never shoots.
- From 2–4 m, a reasonable flick goes in about half the time with Aim Assist 0.6.

## 5. Stage 4 checklist
- [ ] All Stage 1–4 tests are green (Stage 3's Test Drop test is skipped).
- [ ] An upward swipe shoots, and the power follows swipe speed.
- [ ] Down, short and too-slow swipes don't shoot, and a hint explains why.
- [ ] Sideways swipes aim left/right, limited to 20°.
- [ ] One swipe = exactly one shot. Buttons never shoot.
- [ ] Shots can score from about 1.5–4 m (try Aim Assist 1 to confirm).
- [ ] Works on the phone.

## 6. Troubleshooting (Stage 4)
| Problem | Fix |
|---|---|
| **The ball doesn't respond to swipes** | Check that BasketballShooter is on GameSystems and enabled, with no Console errors. The hoop must be placed and a ball must be in your hand. In the Editor, click the Game view first so it has focus, then drag with the **left** mouse button. Active Input Handling must include the Input System. |
| Every swipe says "Swipe up a bit longer and quicker" | Lower Min Swipe Length (0.04) or raise Max Swipe Duration. |
| Shots always fall short | Raise Max Shot Force (up to 6.5), or lower Max Swipe Speed so normal flicks reach full power. |
| Shots always fly over the hoop | Lower Max Shot Force, or raise Min/Max Swipe Speed. |
| Ball goes the wrong way | Point the phone at the hoop: shots follow where the camera faces. Check that Max Aim Angle isn't huge. |
| Pressing a button fires a shot | The button must have a **Button** component, and the EventSystem must be in the scene. StatusText/ShootHintText must have **Raycast Target ✘**. |
| Two shots from one swipe | That can't happen through the code, so check you don't have **two BasketballShooter components** (one on another object). |
| Aim assist does nothing | The hoop prefab needs its child named **Rim** (a Console warning tells you). Aim within 30° of the hoop. |
| Tests can't find `ShotMath` / `SwipeGesture` | Copy both files into `Assets/Scripts` and fix any Console errors first. |

---

**Next: Stage 5, basket detection and scoring.** `HoopScoreTrigger.cs` uses two trigger zones inside the rim (entry from above, then exit below) and `TryMarkScored()` so each ball scores at most once. Balls from below and rim or backboard touches never count. `GameManager.cs` keeps the score and shot count and shows them right away.

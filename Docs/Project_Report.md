# AR Basketball Shooter
### An Augmented Reality Basketball Game for Android using Unity AR Foundation

**Course:** Immersive Technologies  **Programme:** B.Tech Computer Science and Engineering
**Submitted by:** *(name, register number)*  **Guide:** *(faculty name)*  **Institution:** *(college name)*  **Date:** *(date)*

---

## Abstract
Augmented Reality (AR) blends computer-generated objects with the user's real surroundings in real time, and today's smartphones make it widely available.

This project presents **AR Basketball Shooter**, an Android game that lets a player place a virtual basketball hoop in their own room and shoot a virtual ball at it with touch gestures. The app is built with **Unity 6**, **AR Foundation 6** and Google's **ARCore**.
- **Placement:** ARCore detects horizontal surfaces, and an **AR anchor** fixes the hoop to the chosen spot so it stays still as the player walks around it.
- **Physics:** the ball and hoop use real-world dimensions and Unity's rigid-body physics with tuned Physic Materials.
- **Shooting:** a swipe's speed and direction are converted into a clamped physical impulse. An optional aim assist solves the projectile equation, including air drag.
- **Scoring:** a trigger zone counts a basket only when the ball passes downward through the rim, and never twice for the same ball.
- **Interface:** a mobile UI with game states, plus procedurally generated audio and particle effects.
- **Game modes:** besides free play, a **60-second challenge** feeds a persistent **local top-10 leaderboard**.

The project was developed in nine incremental stages, each verified by automated tests: **181 EditMode and 8 PlayMode tests** covering configuration, physics, gameplay rules, UI and persistence. This was followed by testing in Unity's XR Simulation and on an ARCore phone.

**Keywords:** Augmented Reality, ARCore, AR Foundation, Unity, plane detection, anchors, game physics, gesture input, mobile game.

---

## 1. Introduction
AR applications overlay digital content on the camera view of the real world. Unlike Virtual Reality, they keep the user in their environment. That makes AR well suited to casual games that turn an ordinary room into a playground.

Basketball shooting suits AR particularly well:
- the player must judge distance and height in real 3D space;
- the result depends on believable physics;
- the interaction (a flick of the finger) maps naturally to throwing.

**Problem statement:** design and implement a mobile AR game that reliably places a virtual hoop on a real surface, lets the player shoot with intuitive touch gestures, detects valid baskets accurately, and runs smoothly on mid-range Android phones.

## 2. Objectives
1. Detect real horizontal surfaces with ARCore and let the user place a virtual hoop by tapping. The hoop must stay anchored while the user moves.
2. Model a basketball and hoop at real-world scale with realistic rigid-body physics: gravity, mass, drag, bounce and collisions.
3. Convert touch swipes into shots, with power from swipe speed, aim from swipe direction, clamped force, and exactly one shot per swipe.
4. Detect baskets correctly: count only balls passing downward through the rim, at most once per ball, and never rim/backboard touches or balls from below.
5. Provide a complete, readable mobile interface: title, instructions, score, shot count, pause/resume, restart, reset and reposition.
6. Add audio and visual feedback for impacts, shots and baskets.
7. Verify every stage with automated tests, and deliver an optimised, installable Android APK.
8. Add replay value with a timed challenge mode and a local leaderboard that persists between sessions.

## 3. Scope
- **Platform:** Android 7.0+ (API 24) phones on the ARCore supported-devices list.
- **Mode:** single player, portrait orientation, indoor use.
- **Gameplay:** Free Play (no time limit) and a 60-second Challenge, both with score, shot count and accuracy; a local top-10 leaderboard for the challenge.
- **Out of scope for this version:** online leaderboards, multiplayer, iOS. See §11.

## 4. Tools and Technologies
| Category | Technology | Purpose |
|---|---|---|
| Game engine | Unity 6 LTS (URP) | Rendering, physics, UI, build pipeline |
| AR framework | AR Foundation 6.0 + Google ARCore XR Plugin 6.0 | Camera, motion tracking, plane detection, raycasts, anchors |
| Language | C# | All game logic (15 runtime scripts, 4 editor tools) |
| Physics | Rigidbody, Sphere/Box/Capsule/Mesh Colliders, Physic Materials | Ball flight, bounces, rim and backboard |
| Input | Unity Input System (`Pointer`) | Touch on the phone, mouse in the editor |
| UI | uGUI, TextMeshPro, Canvas Scaler, safe area | Resolution-independent mobile interface |
| Testing | Unity Test Framework (NUnit), XR Simulation | Automated EditMode/PlayMode tests; AR testing without a phone |
| Assets | Procedural generation (C# editor tools, Python) | Hoop/ball meshes, textures, sounds, icon; no third-party assets |

## 5. System Architecture
The application is organised in five layers that communicate through **C# events**, which keeps each module independent and testable.

| Layer | Modules | Responsibility |
|---|---|---|
| AR | `ARPlanePlacement`, AR Session, XR Origin (Plane, Raycast, Anchor Managers) | Track the phone, detect floors, place and anchor the hoop |
| Physics & gameplay | `BallSpawner`, `BasketballController`, `BasketballShooter`, `ShotMath`, `SwipeGesture`, `HoopScoreTrigger` | Ball life cycle, shooting, basket detection |
| Game control | `GameManager` | Score, shots, accuracy; game states Title → PlacingHoop → Playing ⇄ Paused |
| Presentation | `UIManager`, `SafeArea`, `ButtonFeedback` | HUD, panels, instructions, feedback |
| Feedback | `AudioManager`, `ScoreEffects`, `MobileSettings` | 3D audio, particles, haptics, device settings |

```
ARPlanePlacement ─HoopPlaced─▶ BallSpawner / GameManager / Shooter / ScoreEffects
Swipe ─▶ SwipeGesture ─▶ BasketballShooter ─(ShotMath)─▶ BallSpawner.LaunchHeldBall ─▶ Rigidbody.AddForce(Impulse)
Ball ─OnCollisionEnter─▶ Bounced ─▶ AudioManager           Ball ─▶ ScoreZone trigger ─▶ HoopScoreTrigger
HoopScoreTrigger ─BasketScored─▶ GameManager ─ScoreChanged/BasketScored/StateChanged─▶ UIManager, AudioManager, ScoreEffects
```

## 6. Methodology
### 6.1 Development process
The project followed an **incremental, test-first staged process**. A stage started only after the previous stage's automated tests passed and it had been tried in XR Simulation:

| Stage | Deliverable | Verification |
|---|---|---|
| 1 | Unity/AR setup, plane detection, tap-to-place hoop, anchoring | 11 configuration and scene tests; XR Simulation; phone |
| 2 | Hoop prefab (backboard, 16-capsule rim, net, Physic Materials) | 9 tests, including physics drops on the rim and board |
| 3 | Basketball prefab, spawning, ball life cycle, Reset Ball | 15 tests: bounce height, resting, no tunnelling, a valid shot scores |
| 4 | Swipe shooting, impulse clamping, aim assist | 23 tests: swipe rules, maths, one shot per swipe, shots from 1.5–4 m |
| 5 | Basket detection, score and shot count | 8 EditMode + 8 PlayMode tests with real trigger events |
| 6 | Game states, title/instructions/pause/restart, safe area | 35 tests: state machine, panels, wiring, scaling |
| 7 | Audio and visual effects | 19 tests: sounds, surface detection, particle prefab, wiring |
| 8 | Android build, performance, documentation | 12 release-readiness tests; device checklist |
| 9 | 60-second challenge, results screen, local leaderboard | 49 tests: clock, countdown, buzzer beaters, ranking, saving, wiring |

Repetitive asset creation (hoop, ball, effect prefabs) is done by **editor tools**, so sizes and colliders are exact and can be regenerated.

### 6.2 Key techniques and algorithms
**a) Plane detection and placement.**
- Every frame, a ray from the screen centre is cast against detected planes (`ARRaycastManager.Raycast`, `TrackableType.PlaneWithinPolygon`). Only planes with `alignment == HorizontalUp` are accepted, and the placement ring is drawn at the hit pose.
- On a tap that isn't on a UI button, the hoop is created as the child of a new GameObject with an **`ARAnchor`**. ARCore keeps that anchor fixed to the real-world point as its map improves.
- The hoop is turned so its front (+Z) faces the player.

**b) Real-scale physics.**
- 1 Unity unit = 1 m. The ball is 0.24 m and 0.6 kg (size 7); the rim's inner diameter is 0.45 m.
- The rim is a ring of 16 capsule colliders rather than a concave mesh collider, which keeps collisions stable and leaves the centre open.
- The ball uses **Continuous Dynamic** collision detection, because a shot moves further in one physics step than the rim is thick.
- Physic Materials use *Average* combining (ball 0.8, floor 0.7, rim 0.5), which gives a bounce of about 55% of drop height, close to a real basketball.
- An invisible floor collider at the hoop's base catches balls outside the scanned area.

**c) Swipe-to-impulse.** A swipe is recorded in screen-height units, so it's resolution-independent.
- **Validity:** the swipe must go up at least 6% of the screen height, take ≤ 1.2 s, and stay within ≤ 60° of vertical.
- **Power:** p = InverseLerp(0.6, 3.0, v), where v is the faster of the average speed and the speed over the last 0.1 s.
- **Force:** F = Lerp(1.5, 5.4, p) N·s.
- **Direction:** the camera's horizontal forward direction, turned by half the swipe's sideways angle (capped at ±20°), then tilted up by 55°.
- The impulse is **clamped** to [1.5, 5.4] N·s and applied with `Rigidbody.AddForce(J, ForceMode.Impulse)`, with backspin ω = 15 rad/s (torque impulse = I·ω, where I = ⅖mr²).
- A gesture object can complete only once per touch, and a launched ball can't be launched again, which together guarantee one shot per swipe.

**d) Aim assist: projectile solution with drag.** For launch angle θ, horizontal distance d and height difference h, the speed without drag is

  v² = g·d² / (2·cos²θ·(d·tanθ − h)),  so the flight time is T = d / (v·cosθ).

With linear damping k (dv/dt = g − k·v), the start velocity that reaches the target in time T is

  **v₀ = g/k + (Δ − g·T/k) · k / (1 − e^(−kT))**.

The player's shot velocity is blended towards v₀ by an adjustable factor (default 0.6), but only when the shot is already within 30° of the hoop.

**e) Basket detection.** A narrow trigger box (12 cm wide) sits inside the rim, from 10 cm above it to 30 cm below. A basket is counted only if the ball:
1. enters the box **moving down with its centre above the rim**;
2. leaves it **with its centre below the rim, still moving down**, within 2 s;
3. returns true from `TryMarkScored()`, which succeeds only once per ball.

A ball that enters the box moving up from below is blocked permanently. A ball resting on the rim is about 24 cm from the centre, so it can't reach the box. A trajectory simulation of 15 shots (1.5–4 m, up to ±8 cm aim error) was used to choose these thresholds: all 15 entered at least 4.3 cm above the rim and left at least 15 cm below it.

**f) Game state machine.** `GameManager` has five states: Title, PlacingHoop, Playing, Paused and GameOver. Each state switches hoop placement, shooting and ball spawning on or off, and Paused sets `Time.timeScale = 0`, which freezes physics while AR tracking continues. Pause is also triggered by the Android Back button and when the app goes to the background.

**f2) Challenge clock and leaderboard.** In Challenge mode a per-frame `Tick(Δt)` lowers the remaining time.
- **When it runs:** only in the Playing state. Placing the hoop and pausing never cost the player time.
- **Countdown:** when the rounded-up whole second drops to 5, 4, 3, 2 or 1, a countdown event plays a beep.
- **Time up:** at zero the game enters GameOver and removes the ball in hand. Balls already in flight may still score during a 2-second grace period ("buzzer beaters"). After that the results are final and further baskets are ignored.
- **Ranking:** score descending, then shots ascending (higher accuracy), then submission time ascending.
- **Saving:** the board is stored as JSON in `Application.persistentDataPath`. It's written to a temporary file and then swapped in, so an interrupted save can't corrupt it, and a damaged file is ignored rather than crashing the game.
- **Names:** rich-text tags are stripped so a name can't change the leaderboard's formatting, and names are limited to 12 characters.
- Tick takes the time step as a parameter, so tests can drive the whole 60 seconds deterministically in milliseconds.

**g) Procedural assets.** All assets were generated for the project:
- the rim (torus) and net (diamond strand) meshes;
- the ball texture, built from per-pixel seam distances on the sphere;
- the sounds (sine partials, filtered noise and decay envelopes);
- the app icon.

## 7. Implementation Summary
| Module | Key responsibilities |
|---|---|
| ARPlanePlacement | Indicator raycast, horizontal-plane filter, tap-to-place (ignoring UI), ARAnchor, hide plane visuals but keep colliders, reposition |
| BallSpawner | Ball held 0.7 m in front of the camera; respawn 0.75 s after a shot; at most 4 balls; Reset Ball; virtual floor |
| BasketballController | States Held / Flying / Finished; Launch (impulse + spin); removes itself when resting, timed out, too far or too low; bounce events; `TryMarkScored` |
| BasketballShooter, ShotMath, SwipeGesture | Gesture capture, validation, power, aim, ideal shot with drag, clamping, backspin |
| HoopScoreTrigger | Entry/exit rules above, block list for balls from below, one score per ball |
| GameManager | Score / shots / baskets / accuracy, state machine, pause and restart, challenge clock, buzzer grace, results |
| Leaderboard | Top-10 ranking, qualifying check, JSON save/load with safe replace, name cleaning |
| UIManager, SafeArea, ButtonFeedback | Panels per state, live HUD and timer, timed instructions, success pop-up, results and leaderboard screens, notch-safe layout, button press animation |
| AudioManager, ScoreEffects | Pooled 3D audio chosen by surface and impact speed; confetti, net stretch, vibration |
| MobileSettings, AndroidBuilder | Keep screen on; pre-build validation and one-click APK build |

## 8. Testing and Results
### 8.1 Automated tests
| Suite | Tests | Examples |
|---|---|---|
| Stage 1 – setup and AR scene | 11 | Android target, ARCore loader, IL2CPP/ARM64/API 24, URP AR background feature, AR managers |
| Stage 2 – hoop | 9 | Rim opening fits the ball; ball through the centre falls through; rim and backboard bounce the ball |
| Stage 3 – ball | 15 | Bounce ratio 35–85%; comes to rest on the floor; a 15 m/s ball doesn't tunnel; a 2.5 m shot scores |
| Stage 4 – shooting | 23 | Swipe validity, power, clamping, aim limits, one shot per swipe, perfect shots from 1.5 / 2.5 / 4 m and from the side |
| Stage 5 – scoring | 8 + **8 PlayMode** | Clean shot = 1; two balls = 2; from below, rim touch, backboard, same ball twice, held ball = 0 |
| Stage 6 – UI and game flow | 35 | Every state transition, pause freezes time, restart, panels per state, safe area, button wiring |
| Stage 7 – audio and effects | 19 | Sound files, surface classification, volume curve, particle prefab, single AudioListener |
| Stage 8 – release | 12 | Build list, package name, version, icon, pre-build checks, keep-screen-on |
| Stage 9 – challenge and leaderboard | 49 | Clock runs only while playing, 5-4-3-2-1 countdown, time up, buzzer beater, top-10 ranking and limit, save/reload, damaged file, name cleaning, results UI |
| **Total** | **181 EditMode + 8 PlayMode = 189** | **All passing** |

### 8.2 Simulation results
- **Ideal shots** from 1.0–6.0 m needed impulses of 2.2–4.9 N·s, all inside the 1.5–5.4 N·s clamp. They came down within 3–9 cm of the rim centre, with entry angles of 37–53°.
- **The final scoring rule** counted 15 of 15 simulated baskets with aim errors up to ±8 cm. An earlier rule ("leave the zone more than 15 cm below the rim") only just counted one of them (15.4 cm), so it was replaced.

### 8.3 Device testing *(fill in with your phone's results)*
| Test | Result |
|---|---|
| Phone model / Android version | |
| Time to detect the floor (good light) | s |
| Hoop drift after walking around it (2 m, 360°) | cm |
| Average frame rate (Profiler) | fps |
| APK size | MB |
| Baskets / shots in a 20-shot trial at 2.5 m (Aim Assist 0.6) | / 20 |

## 9. Challenges and Solutions
| Challenge | Solution |
|---|---|
| Black camera screen with URP | Added the **AR Background Renderer Feature** to the URP renderers; an automated test checks it |
| Fast balls passing through the thin rim ("tunnelling") | Continuous Dynamic collision detection; a capsule-ring rim; a test with a 15 m/s ball |
| Balls falling through the floor outside the scanned area | A large invisible floor collider attached to the hoop's anchor |
| Rim touches and balls from below counted as baskets | Narrow trigger zone, direction-aware entry/exit rules, block list, once-per-ball flag |
| One swipe firing several shots | Single-completion gesture object, launch-once ball state, cooldown |
| Testing AR without a phone for every change | Unity XR Simulation plus 189 automated tests, with the logic separated from MonoBehaviours (`ShotMath`, `SwipeGesture`, `Leaderboard`) |
| Testing a 60-second timer quickly | `GameManager.Tick(Δt)` takes the time step as a parameter, so tests run a whole challenge in milliseconds |
| Unity sends trigger events only in Play mode | PlayMode tests that fire real balls at the real hoop prefab |
| No licensed assets available | Procedural generation of meshes, textures, sounds and the icon |

## 10. Limitations
- Works only on ARCore-certified Android phones and needs reasonable light and a textured floor.
- Virtual objects aren't hidden behind real furniture (no occlusion).
- The hoop is fixed in place, and difficulty doesn't change during a challenge.
- The leaderboard is stored only on the device, so players can't compare scores across phones.
- The net is a static mesh with a simple stretch animation, not a cloth simulation.

## 11. Future Enhancements
1. **Online leaderboard:** share the top 10 between players (for example with Firebase or Unity Gaming Services). The local `Leaderboard` class already defines the ranking rules.
2. **More challenge variants:** bonus seconds for streaks, a "3 misses and you're out" mode, and daily challenges.
3. **Difficulty levels:** a moving or rising hoop, longer distances, a lower aim assist, and wind.
4. **Occlusion:** use the ARCore Depth API (AR Foundation `AROcclusionManager`) so real objects can hide the ball and hoop.
5. **Realistic lighting:** AR light estimation and a shadow-catcher plane so the ball casts shadows on the real floor.
6. **Shared AR multiplayer:** ARCore Cloud Anchors so two phones see the same hoop and take turns.
7. **Cloth-simulated net, a ball trail, streak combos ("On fire!") and richer haptics.**
8. **iOS version** through the ARKit XR Plugin. The project is already platform-neutral because it uses AR Foundation.
9. **Accessibility:** left-handed layout, adjustable text size, and visual indicators for every sound.

## 12. Conclusion
AR Basketball Shooter meets all of its objectives:
- **Placement:** the hoop is placed reliably on real surfaces and stays anchored as the player moves.
- **Physics:** shooting feels natural and is physically grounded.
- **Scoring:** baskets are detected accurately and fairly.
- **Interface:** the mobile UI is complete, with audio and visual feedback.
- **Replay value:** a 60-second challenge with a persistent local leaderboard.
- **Delivery:** the result is an optimised Android APK.

The staged, test-driven approach was the most valuable lesson. Each feature was proved correct before the next was built, and the 189 automated tests caught configuration and logic problems early. The challenge mode and leaderboard were added at the end without breaking any earlier test, which shows the event-based architecture stays easy to extend.

## References
1. Unity Technologies, *AR Foundation 6.0 Manual*: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html
2. Unity Technologies, *Google ARCore XR Plugin Manual*: https://docs.unity3d.com/Packages/com.unity.xr.arcore@6.0/manual/index.html
3. Google, *ARCore Overview and Supported Devices*: https://developers.google.com/ar and https://developers.google.com/ar/devices
4. Unity Technologies, *Unity 6 Manual – Physics (Rigidbody, Colliders, Physic Materials, Continuous Collision Detection)*: https://docs.unity3d.com/Manual/PhysicsSection.html
5. Unity Technologies, *Input System Manual*: https://docs.unity3d.com/Packages/com.unity.inputsystem@latest
6. Unity Technologies, *Unity Test Framework Manual*: https://docs.unity3d.com/Packages/com.unity.test-framework@latest
7. Halliday, Resnick & Walker, *Fundamentals of Physics* – projectile motion and linear drag.

## Appendix – Screenshots *(add your own)*
1. Title screen  2. Floor scanning with the placement ring  3. Hoop placed in the room  4. Shot in flight
5. "SWISH!" with confetti  6. Pause panel  7. Test Runner (EditMode, all green)  8. Test Runner (PlayMode, all green)

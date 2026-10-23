# AR Basketball Shooter – Stage 6: The Complete Game UI

**Goal:** turn the working prototype into a game with a polished mobile interface:

| Required element | Where |
|---|---|
| Game title "AR Basketball Shooter" | Title panel |
| Start Game button | Title panel |
| Place Hoop instruction | Bottom card while placing |
| Aim and Shoot instruction | Card shown for 5 s (or until the first shot) after placing |
| Current score / shot count | HUD, top-left / top-right (+ accuracy) |
| Reset Ball / Reposition Hoop | HUD, bottom (from Stages 1 and 3) |
| Restart Game | Pause panel |
| Pause / Resume | Pause button in the HUD; Resume in the pause panel; Android **Back** button |
| Success message / effect | "SWISH!" pop-up + score punch (Stage 5) |
| Readable on every screen size | Canvas Scaler 1080×1920, plus the **SafeArea** script for notches and gesture bars |

**Before you start:** EditMode tests for Stages 1–5 and the PlayMode tests are green ✔.

---

## 1. Add / replace the files
```
Assets/Scripts/GameManager.cs      ← REPLACED (Stage 6 version: game states)
Assets/Scripts/UIManager.cs        ← REPLACED (Stage 6 version: panels)
Assets/Scripts/SafeArea.cs         ← NEW
Assets/Tests/Stage6/ARBasketball.Tests.Stage6.asmdef
Assets/Tests/Stage6/Stage6GameFlowTests.cs
Assets/Tests/Stage6/Stage6SceneTests.cs
```
The new GameManager and UIManager keep every Stage 5 feature and method, so the Stage 5 tests still apply.

## 2. The game flow (GameManager)
```
            Start Game                hoop placed
  Title ─────────────▶ PlacingHoop ─────────────▶ Playing ◀───Resume───┐
                          ▲   │                     │  │               │
                          │   └──Pause──┐   ┌──Pause┘  │ Reposition    │
                          │             ▼   ▼          ▼               │
                          │            Paused ─────────┴───────────────┘
                          └────────── Reposition Hoop ◀─ Playing
  Restart Game (from Paused/Playing/Placing): score 0, balls cleared, back to Playing (or PlacingHoop if no hoop)
```
| State | Hoop placement | Shooting | Ball spawning | Time.timeScale | Panels shown |
|---|---|---|---|---|---|
| **Title** | off | off | off | 1 | Title |
| **PlacingHoop** | **on** | off | on | 1 | HUD + Place Hoop card + Pause button |
| **Playing** | off | **on** | on | 1 | HUD (+ Aim & Shoot card at first) + Pause button |
| **Paused** | off | off | off | **0** (physics frozen) | HUD + Pause panel |

- **Android Back button** (Esc in the Editor) toggles pause.
- When the app goes to the background (home button, phone call), the game pauses itself: `OnApplicationPause`.
- AR tracking keeps running while paused, because `timeScale` doesn't stop ARCore. The hoop stays anchored.
- New GameManager field: **Show Title On Start** ✔. Untick it while debugging to skip the title screen.

## 3. Build the UI
Here is the finished Canvas. Move your existing objects into the new parents by dragging them in the Hierarchy; their Inspector references stay intact.
```
Canvas                     (Canvas, Canvas Scaler, Graphic Raycaster, UIManager)
└── SafeArea               (RectTransform stretch-stretch, SafeArea script)       ← NEW
    ├── HUD                (empty RectTransform, stretch-stretch)                  ← NEW
    │   ├── ScoreText      (top-left)
    │   ├── ShotsText      (top-right)
    │   ├── AccuracyText   (top-right, under ShotsText)                            ← NEW
    │   ├── PauseButton    (top-centre)                                            ← NEW
    │   ├── StatusText
    │   ├── ShootHintText
    │   ├── BasketMessage
    │   ├── ResetBallButton
    │   └── RepositionButton
    ├── PlaceHoopPanel     (bottom card)                                           ← NEW
    ├── AimShootPanel      (bottom card)                                           ← NEW
    ├── TitlePanel         (full screen)                                           ← NEW
    └── PausePanel         (full screen)                                           ← NEW
```
**Order matters.** Lower in the Hierarchy is drawn on top, so TitlePanel and PausePanel go last.

### 3.1 SafeArea and HUD
1. Right-click Canvas → **Create Empty**, name it `SafeArea`.
   - In Rect Transform, click the anchor preset, then **Alt + Shift + click stretch-stretch**, so Left/Right/Top/Bottom are all 0.
   - **Add Component → Safe Area**.
2. Right-click SafeArea → **Create Empty**, name it `HUD`, and make it stretch-stretch the same way.
3. Drag **ScoreText, ShotsText, StatusText, ShootHintText, BasketMessage, ResetBallButton, RepositionButton** into `HUD`.
4. `AccuracyText`: duplicate ShotsText (Ctrl+D) and rename it. Set Pos (−40, −150), Font 40, Text "Accuracy: 0%".
5. `PauseButton`: right-click HUD → **UI → Button - TextMeshPro**.
   - Anchor top-centre, Pos (0, −70), Size 140 × 140, label **"II"** at Font 60.
   - **On Click () → GameSystems → GameManager.PauseGame**.

### 3.2 PlaceHoopPanel (instruction card)
1. Right-click SafeArea → **UI → Image**, name it `PlaceHoopPanel`.
   - Anchor bottom-stretch, Left/Right 60, Pos Y 380, Height 420.
   - Colour black with alpha 160. Set Source Image to **UISprite** for rounded corners.
2. **Add Component → Canvas Group** and **untick Blocks Raycasts**. The card is only information and must never block taps.
3. Add two TMP texts inside it:
   - **Header** "PLACE THE HOOP": Font 56, bold, top.
   - **Body**: Font 40, centred, with this text:
     > 1. Point your phone at the floor
     > 2. Move it slowly until a grid appears
     > 3. **Tap** the grid to place the hoop

### 3.3 AimShootPanel (instruction card)
Duplicate PlaceHoopPanel, rename it `AimShootPanel`, and set Pos Y 560. Change the texts:
- **Header** "AIM & SHOOT".
- **Body**:
  > **Swipe** up to shoot – a faster swipe throws harder
  > Swipe a little left or right to aim
  > Stand about 2–3 m from the hoop

Keep the Canvas Group with Blocks Raycasts unticked.

### 3.4 TitlePanel (full screen)
1. Right-click SafeArea → **UI → Image**, name it `TitlePanel`.
   - Stretch-stretch, all offsets 0.
   - Colour (10, 14, 30) with alpha 210. The camera shows faintly behind it.
   - Keep **Raycast Target ✔**: it must block touches.
2. Add children:
   - `TitleText` (TMP): **"AR Basketball Shooter"**, Font 110, bold, orange (255, 140, 30) with outline, anchor top-stretch, Pos Y −420, Height 300, word wrapping on.
   - `SubtitleText` (TMP): "Shoot hoops in your own room!", Font 48, Pos Y −700.
   - `HowToText` (TMP): "Scan the floor  •  Tap to place  •  Swipe to shoot", Font 38, grey, middle-centre.
   - `StartButton` (Button - TMP): anchor bottom-centre, Pos Y 420, Size 680 × 180, colour orange, label **"START GAME"** at Font 64, bold.
     - **On Click () → GameSystems → GameManager.StartGame**.

### 3.5 PausePanel (full screen)
1. Right-click SafeArea → **UI → Image**, name it `PausePanel`. Stretch-stretch, black with alpha 190, **Raycast Target ✔**.
2. Add children:
   - `PausedText` (TMP): "PAUSED", Font 100, bold, Pos Y 300.
   - `ResumeButton` (Button - TMP): Size 600 × 160, Pos Y 50, label "RESUME".
     - **On Click → GameManager.ResumeGame**.
   - `RestartButton` (Button - TMP): Size 600 × 160, Pos Y −170, label "RESTART GAME".
     - **On Click → GameManager.RestartGame**.

### 3.6 UIManager fields (Canvas)
| Field | Value |
|---|---|
| Score Text / Shots Text / **Accuracy Text** | ScoreText / ShotsText / AccuracyText |
| Basket Message | BasketMessage |
| **Title Panel** | TitlePanel |
| **Hud Panel** | HUD |
| **Place Hoop Panel** | PlaceHoopPanel |
| **Aim Shoot Panel** | AimShootPanel |
| **Pause Panel** | PausePanel |
| **Pause Button** | PauseButton |
| Aim Hint Seconds | 5 |

You don't need to hide panels by hand. UIManager shows and hides them from the game state when Play starts.

### 3.7 Readability on different screens
- **Canvas Scaler** (from Stage 1): Scale With Screen Size, **1080 × 1920**, Match **0.5**. Everything scales with the screen.
- **SafeArea** shrinks the UI to `Screen.safeArea`, so nothing sits under a notch, a hole-punch camera or the gesture bar.
- Keep fonts at **≥ 38** at the 1080 reference size, and make buttons **≥ 130 px** tall (about 7 mm on a phone).
- Put white text on dark cards or give it an outline, because the camera image behind it can be bright.
- Preview it: in the Game view's top-left dropdown, switch **Game → Simulator** and try several phones (small, large, notched). The UI must look right on each.

## 4. How the UI scripts work together
```
Buttons ─OnClick─▶ GameManager.StartGame / PauseGame / ResumeGame / RestartGame
                          │ SetState()
                          ├──▶ ARPlanePlacement.SetPlacementEnabled
                          ├──▶ BasketballShooter.SetShootingEnabled
                          ├──▶ BallSpawner.SetSpawningEnabled / SpawnBallInHand / ClearAllBalls
                          ├──▶ Time.timeScale (0 when paused)
                          └──StateChanged──▶ UIManager.ShowState ─▶ panels on/off
GameManager.ScoreChanged ─▶ UIManager.Refresh ─▶ Score / Shots / Accuracy texts (hides the Aim card after the 1st shot)
GameManager.BasketScored ─▶ UIManager: "SWISH!" + score punch
```

## 5. Test Stage 6
### 5.1 EditMode tab → Run All (Stages 1–6)
| Test | What it proves |
|---|---|
| `Game_StartsOnTitleScreen`, `StartGame_WithoutHoop_GoesToPlacingHoop`, `PlacingTheHoop_StartsPlaying` | Start flow |
| `Pause_FreezesTime_AndResumeContinues`, `PauseWhilePlacing_ResumesPlacing`, `TogglePause_PausesAndResumes`, `Pause_IsIgnoredOnTitleScreen` | Pause and resume |
| `Restart_ClearsScore_AndKeepsPlaying` | Restart (also from the pause screen) |
| `RepositioningTheHoop_GoesBackToPlacing` | Reposition |
| `StateChanged_IsRaisedForEveryChange` | The UI is told about every change |
| `Panels_Title/Placing/Playing/Paused_...` | The right panels for each state |
| `UIManager_ShowsAimInstructionsWhenPlayStarts` | The Aim card appears after placing and hides after the first shot |
| `SafeArea_KeepsUiAwayFromNotchAndGestureBar`, `SafeArea_IgnoresInvalidScreen` | Notch maths |
| `UIManager_PanelIsAssigned` ×6, `TitlePanel_ShowsGameTitle`, `Instructions_ExplainPlacingAndShooting` | The UI content exists |
| `FullScreenPanels_BlockTouchesUnderneath`, `InstructionCards_DoNotBlockButtons` | Touch handling |
| `Button_IsWired` ×6 | Start, Pause, Resume, Restart, Reset Ball and Reposition all call the right method |
| `Canvas_ScalesWithScreenSize`, `Hud_IsInsideSafeArea` | Different screen sizes |

The PlayMode tab (Stage 5) must still be green too.

### 5.2 XR Simulation
1. Press Play. The **title screen** shows "AR Basketball Shooter" and Start Game. Clicking the background does nothing: no hoop placement, no shot.
2. Click **START GAME**. The HUD (Score 0, Shots 0, Accuracy 0%) and the **Place the hoop** card appear.
3. Place the hoop. The place card hides, the **Aim & Shoot** card appears and a ball is in your hand. Take a shot and the card disappears.
4. Click **II** (or press **Esc**). The pause panel appears and flying balls **freeze in mid-air**. Swipes and taps do nothing.
5. Click **RESUME**. The balls continue their flight.
6. Score a few baskets, pause, then click **RESTART GAME**. Score/Shots/Accuracy go back to 0, the balls are cleared, a fresh ball is in your hand, and the hoop stays where it was.
7. Click **Reposition Hoop**. The place card returns. Place the hoop again and play continues.
8. Switch the Game view to **Simulator** and check 3 different phones, including one with a notch. Nothing is cut off or hidden.

### 5.3 Phone
1. Play through steps 1–7 on the device.
2. Press the Android **Back** button. The game pauses; press it again to resume.
3. Press **Home** and reopen the app. The game is paused and the hoop is still in place.
4. Check text size and button size with your thumb.

## 6. Stage 6 checklist
- [ ] EditMode Stages 1–6 + PlayMode Stage 5 are all green (only Stage 3's Test Drop test is skipped).
- [ ] Title screen → Start Game → Place Hoop card → Aim & Shoot card → playing.
- [ ] Pause/Resume works (button, Back key, app switch) and freezes the balls.
- [ ] Restart Game resets everything except the hoop.
- [ ] Score, shots and accuracy are always correct.
- [ ] The UI looks right on at least 3 simulated phones and on the real phone.

## 7. Troubleshooting (Stage 6)
| Problem | Fix |
|---|---|
| **UI buttons do not work** | (1) There must be an **EventSystem** with **Input System UI Input Module**. (2) Nothing transparent should be on top of the button: the instruction cards need Canvas Group → Blocks Raycasts ✘, and texts need Raycast Target ✘. (3) The button's On Click must point at **GameSystems** (the scene object, not a prefab) and the right method. (4) The Canvas needs a **Graphic Raycaster**. |
| Stuck on the title screen | StartButton must call GameManager.StartGame. Check the Console for GameManager errors. |
| Hoop can't be placed after Start | GameManager must find ARPlanePlacement. The state should be PlacingHoop (watch `State` in the Inspector's Debug mode). |
| The game is frozen after stopping Play mode | Time.timeScale got stuck at 0. GameManager resets it in OnDisable; if it still happens, click Play and Stop again. |
| Pause panel shows but balls keep moving | You may have two GameManagers, or code elsewhere that sets Time.timeScale. |
| UI hidden under the notch | The UI objects must be **inside SafeArea**, and SafeArea must be stretch-stretch with 0 offsets. |
| Text is tiny on a tablet | Canvas Scaler must be **Scale With Screen Size**, not Constant Pixel Size. |
| The Aim card never disappears | Aim Hint Seconds should be > 0. Shots must be counted: GameManager needs BasketballShooter. |

---

**Next: Stage 7, audio and visual effects.**
- A bounce sound whose volume follows impact speed, a rim "clank", a net "swish", and button clicks.
- A particle burst on every basket.
- Button press animations.
- `AudioManager.cs` using free or procedurally generated sounds.

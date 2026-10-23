# AR Basketball Shooter – Stage 9: 60-Second Challenge & Local Leaderboard

**Goal:** a second way to play next to Free Play.

| Feature | Details |
|---|---|
| **60s Challenge** | The countdown starts once the hoop is placed. It stops while placing or paused. |
| Countdown feedback | Timer in the HUD (`0:42`). It turns red and pulses for the last 10 s, and beeps for the last 5 s. |
| Time up | Buzzer sound. Shooting stops and the ball in your hand disappears. |
| **Buzzer beaters** | Balls already in the air still count for 2 s ("BUZZER BEATER!") |
| Results screen | Score, shots, accuracy; Play Again / Main Menu / Leaderboard |
| **Local leaderboard** | Top 10, saved on the phone, so it survives closing the app. Ranked by score, then fewer shots, then earliest. |
| High-score name | A top-10 score asks for a name (max 12 characters, remembered for next time) |
| Title screen | FREE PLAY, 60s CHALLENGE, LEADERBOARD, plus "Challenge best: N" |

**Before you start:** all Stage 1–8 tests are green ✔ and the APK runs on your phone.

---

## 1. Add / replace the files
```
Assets/Scripts/Leaderboard.cs           ← NEW  (plain C#: ranking + saving; not attached to anything)
Assets/Scripts/GameManager.cs           ← REPLACED (GameOver state, challenge clock, results, leaderboard)
Assets/Scripts/UIManager.cs             ← REPLACED (timer, results screen, leaderboard panel)
Assets/Scripts/BallSpawner.cs           ← UPDATED (new DiscardHeldBall method)
Assets/Scripts/AudioManager.cs          ← UPDATED (countdown beep + buzzer)
Assets/Audio/countdown_tick.wav, buzzer.wav   ← NEW sounds (the other 7 are unchanged)
Tools/generate_sounds.py                ← UPDATED (makes all 9 sounds)
Assets/Tests/Stage9/ARBasketball.Tests.Stage9.asmdef
Assets/Tests/Stage9/Stage9ChallengeTests.cs
Assets/Tests/Stage9/Stage9SceneTests.cs
```
All earlier methods and fields keep their names, so the Stage 1–8 tests and your existing button wiring still work.

## 2. The game flow now
```
                 FREE PLAY ──────────────┐
   Title ────────60s CHALLENGE ──────────┼──▶ PlacingHoop ─▶ Playing ⇄ Paused
     ▲                                   │                     │ (challenge clock runs only here)
     │                                   │                time = 0
     │                                   │                     ▼
     └──────── MAIN MENU ◀── GameOver (results) ◀── 2 s buzzer grace (flying balls can still score)
                                │
                        PLAY AGAIN ──▶ Playing (same mode, clock 60 s, score 0, hoop stays)
```
| State | Placement | Shooting | Spawning | Panels |
|---|---|---|---|---|
| **GameOver** (new) | off | off | off (ball in hand removed) | HUD + **Game Over panel** |

### GameManager new fields (GameSystems)
| Field | Default | Meaning |
|---|---|---|
| Challenge Seconds | 60 | Length of a challenge |
| Buzzer Grace Seconds | 2 | Flying balls can still score this long after the buzzer |
| Countdown From | 5 | Beep every second for the last N seconds |

The leaderboard file is `Application.persistentDataPath/leaderboard.json`; on Android that's the app's private storage. It's deleted only when the app is uninstalled or its data is cleared.

## 3. UI changes
### 3.1 Title panel: three buttons
Change the existing `StartButton` label to **FREE PLAY**. Keep its name and its On Click (GameManager.StartGame).

Add two buttons **below** it by duplicating StartButton (Ctrl+D), renaming, and changing the On Click:

| Button | Pos Y | Label | On Click |
|---|---|---|---|
| `StartButton` (existing) | 520 | FREE PLAY | GameManager.**StartGame** |
| `ChallengeButton` | 330 | 60s CHALLENGE | GameManager.**StartChallenge** |
| `LeaderboardButton` | 160 | LEADERBOARD (smaller: 520 × 130, dark grey) | Canvas → UIManager.**OpenLeaderboard** |

Add `BestScoreText` (TMP, Font 40, grey, Pos Y 50 above the buttons) for "Challenge best: 14".

### 3.2 HUD: timer
Right-click HUD → **UI → Text - TextMeshPro**, name it `TimerText`:
- anchor top-centre, Pos (0, −190), just under the pause button;
- Font 72, bold, white with outline, text "1:00";
- **Raycast Target ✘**.

UIManager shows it only in the challenge.

### 3.3 Game Over panel (results)
Right-click **SafeArea → UI → Image**, name it `GameOverPanel`. Stretch-stretch, black with alpha 200, **Raycast Target ✔**. Put it **below PausePanel** in the Hierarchy. Children:

| Object | Type | Pos Y | Settings |
|---|---|---|---|
| `TimeUpText` | TMP | 600 | "TIME UP!", Font 110, bold, orange |
| `ResultText` | TMP | 330 | Font 56, centre (UIManager writes "Score / Shots / Accuracy") |
| `RankText` | TMP | 120 | Font 44, yellow |
| `NameEntry` | Empty (RectTransform 900 × 150) | −30 | Container for the next two |
| ↳ `NameInput` | **UI → Input Field - TextMeshPro** | | 600 × 130, Font 48, placeholder "Your name" |
| ↳ `SaveScoreButton` | Button - TMP | | 260 × 130 at the right, "SAVE" → UIManager.**SaveScore** |
| `PlayAgainButton` | Button - TMP | −260 | 600 × 150, "PLAY AGAIN" → GameManager.**RestartGame** |
| `MainMenuButton` | Button - TMP | −440 | 600 × 130, "MAIN MENU" → GameManager.**ReturnToTitle** |
| `ResultsLeaderboardButton` | Button - TMP | −600 | 600 × 130, "LEADERBOARD" → UIManager.**OpenLeaderboard** |

### 3.4 Leaderboard panel
Right-click **SafeArea → UI → Image**, name it `LeaderboardPanel`. Stretch-stretch, very dark blue with alpha 235, **Raycast Target ✔**. It must be the **last** child of SafeArea, so it's drawn on top of everything. Children:

| Object | Type | Settings |
|---|---|---|
| `LeaderboardTitle` | TMP | "TOP 10 – 60s CHALLENGE", Font 64, top (Pos Y −160) |
| `LeaderboardHeader` | TMP | `#<pos=12%>NAME<pos=64%>SCORE<pos=82%>ACC`, Font 38, grey, width 900, Pos Y −280 |
| `LeaderboardText` | TMP | width 900, height 1000, top-aligned, Font 44, Line Spacing 20, **Rich Text ✔** |
| `CloseLeaderboardButton` | Button - TMP | bottom, Pos Y 200, 500 × 140, "CLOSE" → UIManager.**CloseLeaderboard** |

### 3.5 New buttons need Button Feedback
Add **Button Feedback** to ChallengeButton, LeaderboardButton, SaveScoreButton, PlayAgainButton, MainMenuButton, ResultsLeaderboardButton and CloseLeaderboardButton. The Stage 7 test checks every button.

### 3.6 UIManager fields (Canvas)
| Field | Value |
|---|---|
| Timer Text | TimerText |
| Low Time Seconds / Low Time Colour | 10 / red |
| Game Over Panel | GameOverPanel |
| Result Text / Rank Text | ResultText / RankText |
| Name Entry / Name Input | NameEntry / NameInput |
| Leaderboard Panel / Leaderboard Text | LeaderboardPanel / LeaderboardText |
| Best Score Text | BestScoreText |

### 3.7 AudioManager fields (GameSystems)
- Countdown Tick = `countdown_tick`
- Buzzer = `buzzer`

### Hierarchy after Stage 9 (SafeArea)
```
SafeArea
├── HUD               (… + TimerText)
├── PlaceHoopPanel
├── AimShootPanel
├── TitlePanel        (StartButton=FREE PLAY, ChallengeButton, LeaderboardButton, BestScoreText)
├── PausePanel
├── GameOverPanel     (TimeUpText, ResultText, RankText, NameEntry{NameInput, SaveScoreButton},
│                      PlayAgainButton, MainMenuButton, ResultsLeaderboardButton)
└── LeaderboardPanel  (LeaderboardTitle, LeaderboardHeader, LeaderboardText, CloseLeaderboardButton)
```

## 4. How it works
```
GameManager.Update ─▶ Tick(deltaTime)
   Playing + Challenge: TimeRemaining −= dt
       crossing 5,4,3,2,1 ─▶ CountdownTick ─▶ AudioManager beep        UIManager.Update shows FormatTime(TimeRemaining)
       reaching 0 ─────────▶ SetState(GameOver) + TimeUp ─▶ buzzer; BallSpawner.DiscardHeldBall()
   GameOver: grace −= dt ─▶ 0 ─▶ ResultsReady ─▶ ChallengeFinished(qualifies) ─▶ UIManager shows rank / name entry
SAVE ─▶ UIManager.SaveScore ─▶ GameManager.SubmitChallengeScore(name) ─▶ Leaderboard.Submit ─▶ rank (saved to JSON)
                             └▶ UIManager shows the leaderboard with your row highlighted
```
**Leaderboard rules** (`Leaderboard.cs`):
- **Ranking:** higher score first, then fewer shots (more accurate), then the earlier result.
- **Qualifying:** only scores above 0, and only if they beat 10th place.
- **Saving:** the JSON is written to a temporary file and then swapped in, so a crash during saving can't break it. A damaged file is ignored with a warning, and a new board starts.
- **Names:** rich-text tags and `< >` are removed (so names can't change the leaderboard's colours), they're cut to 12 characters, and an empty name becomes "Player".
- Baskets that land after the results are final are ignored, and each result can be saved only once.

## 5. Test Stage 9
### 5.1 EditMode tab → Run All (Stages 1–9: 181 tests)
| Group | Tests | What it proves |
|---|---|---|
| Leaderboard | `RanksByScoreThenFewerShots`, `KeepsOnlyTheTop10`, `ZeroNeverQualifies`, `IsStillThereAfterRestartingTheApp`, `DamagedFileStartsFresh`, `CleansNames` ×6 | Ranking, limit, saving, robustness, safe names |
| Challenge flow | `Challenge_StartsWithAFullClock`, `Clock_RunsOnlyWhilePlaying`, `TimeUp_EndsTheGame`, `Countdown_BeepsForTheLast5Seconds`, `BuzzerBeater_CountsDuringGrace_ThenResultsAreFinal`, `ChallengeFinished_SaysNoWhenThereWereNoBaskets`, `SubmitScore_SavesOnceWithRank`, `FreePlay_HasNoTimeLimit`, `PlayAgain_ResetsClockAndScore`, `MainMenu_ReturnsToTitle` | The whole challenge state machine, driven second by second |
| UI | `Timer_IsFormattedAsMinutesAndSeconds` ×6, `LeaderboardText_ListsInOrderAndHighlightsYou`, `LeaderboardText_EmptyBoardInvitesToPlay`, `ResultsPanel_ShowsWhenTimeIsUp` | Display logic |
| Scene | `UIManager_ChallengeFieldIsAssigned` ×8, `Button_IsWired` ×6, `ResultAndLeaderboardPanels_BlockTouchesAndUseSafeArea`, `Timer_IsPartOfTheHud`, `AudioManager_HasChallengeSounds`, `ChallengeSound_IsImported` ×2 | Wiring |

These tests use a **temporary** leaderboard file, so your real high scores are never changed. PlayMode (Stage 5, 8 tests) must still be green.

### 5.2 XR Simulation
1. The title shows FREE PLAY, 60s CHALLENGE and LEADERBOARD. LEADERBOARD shows "No scores yet"; CLOSE closes it.
2. **60s CHALLENGE:** the timer shows **1:00** and **doesn't move** while you place the hoop. Once the hoop is placed it counts down.
3. Pause, wait 5 s and resume. The timer didn't move while paused.
4. *Tip:* for faster testing, set GameManager → Challenge Seconds to **15** in Play mode.
5. At 0:10 the timer turns red and pulses. At 0:05–0:01 you hear beeps, then the **buzzer**. The ball in your hand disappears and the results screen appears.
6. Shoot at 0:01 so the ball is still in the air at the buzzer. If it goes in, **BUZZER BEATER!** appears and the score goes up.
7. If you scored at least 1, you see "New high score!". Type a name, press **SAVE**, and the leaderboard opens with your row highlighted.
8. **PLAY AGAIN** starts a new challenge immediately (1:00, score 0, hoop in place). **MAIN MENU** goes back to the title, which shows "Challenge best: N".
9. Stop Play mode and press Play again. The leaderboard still has your scores.
10. **FREE PLAY:** no timer, and it never ends.

### 5.3 Phone
- Repeat 5.2. Tapping the name field opens the keyboard.
- Close the app completely (swipe it away), reopen it, and check the leaderboard is still there.
- Play a full challenge while a notification arrives or you press Home: the game pauses and the timer stops.

## 6. Stage 9 checklist
- [ ] EditMode Stages 1–9 (181) + PlayMode (8) are all green (only Stage 3's Test Drop test is skipped; Stage 8's development-build check may be skipped).
- [ ] The challenge clock runs only while playing. Red last 10 s, beeps, buzzer.
- [ ] Buzzer beaters count. Nothing counts after the results.
- [ ] A top-10 score asks for a name, saves, and shows the highlighted leaderboard.
- [ ] The leaderboard survives restarting the app. FREE PLAY has no timer.
- [ ] Rebuild the APK (Stage 8 tool) and test on the phone.

## 7. Troubleshooting (Stage 9)
| Problem | Fix |
|---|---|
| The timer never appears | Assign UIManager → Timer Text. It shows only in **60s CHALLENGE**, and TimerText must be inside HUD. |
| The timer runs during hoop placement | You have an old GameManager. Copy the Stage 9 version: the clock runs only in the Playing state. |
| No results screen after time up | Assign UIManager → Game Over Panel. Check the Console for errors. Buzzer Grace Seconds must be small (2). |
| The name field doesn't appear | It appears only for a **top-10** score above 0. Assign Name Entry and Name Input. |
| SAVE does nothing | SaveScoreButton → On Click → Canvas (UIManager) → **SaveScore**. Each challenge can be saved once. |
| The leaderboard is empty after restarting | Make sure you pressed **SAVE**. Check the Console for "[Leaderboard] Could not save". On the phone, clearing the app's data deletes it. |
| To clear the leaderboard while testing | Editor (Windows): delete `leaderboard.json` from `%USERPROFILE%\AppData\LocalLow\<Company Name>\<Product Name>\`. Phone: App info → Storage → Clear data. |
| Leaderboard columns don't line up | LeaderboardText must have **Rich Text ✔**. The `<pos=…>` tags need the TMP text to be the full panel width (900). |
| No beeps or buzzer | Assign AudioManager → Countdown Tick and Buzzer. |

---

All 9 stages are now done. For the final submission:
- Rebuild the APK with **Tools → AR Basketball → Build Android APK**.
- Record a demo showing a full 60-second challenge and a leaderboard entry.
- README.md and Project_Report.md are updated for Stage 9.

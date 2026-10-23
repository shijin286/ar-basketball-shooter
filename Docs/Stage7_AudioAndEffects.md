# AR Basketball Shooter – Stage 7: Audio & Visual Effects

**Goal:** make every action feel good.

| Event | Sound | Visual |
|---|---|---|
| Ball hits the floor | `ball_bounce`: louder for harder hits, 3D at the ball | – |
| Ball hits the rim | `rim_clank` (metal) | – |
| Ball hits the backboard / stand | `backboard_thud` | – |
| Basket! | `net_swish` (3D) + `score_chime` | Confetti burst, net stretches, phone vibrates, "SWISH!" + score punch (Stage 5) |
| Shot | `shoot_whoosh`: louder for stronger shots | – |
| Any button | `button_click` | Button shrinks while pressed, springs back |
| Pause | Game sounds pause too; button clicks still play | – |

**Before you start:** EditMode Stages 1–6 and PlayMode are green ✔.

---

## 1. Add the files
```
Assets/Audio/ball_bounce.wav, rim_clank.wav, backboard_thud.wav, net_swish.wav,
             shoot_whoosh.wav, button_click.wav, score_chime.wav      ← 7 sounds (≈ 270 KB total)
Assets/Scripts/AudioManager.cs                                       ← on GameSystems
Assets/Scripts/ScoreEffects.cs                                       ← on GameSystems
Assets/Scripts/ButtonFeedback.cs                                     ← on every Button
Assets/Scripts/Editor/EffectsPrefabBuilder.cs                        ← builds the confetti prefab
Assets/Tests/Stage7/ARBasketball.Tests.Stage7.asmdef
Assets/Tests/Stage7/Stage7EffectsTests.cs
Tools/generate_sounds.py                                             ← (optional) recreates the sounds
```

### About the sounds
`Tools/generate_sounds.py` generated all seven from sine waves, filtered noise and decay curves, using only Python's standard library. They're **original and free to use** in your project and report.
- **To change one:** edit its function in the script and run `python generate_sounds.py`.
- **To use recorded sounds instead:** CC0 libraries such as **Kenney.nl** (Audio packs) or **freesound.org** (filter by CC0) work. Keep the same file names, or drag the new clips into AudioManager.

| File | How it's made | Length |
|---|---|---|
| ball_bounce | 95 Hz thump with a falling pitch + short noise "slap" + 420 Hz air ring | 0.35 s |
| rim_clank | 6 inharmonic metal partials (523–4700 Hz) with different decays + click | 0.70 s |
| backboard_thud | 110 Hz knock + band-limited noise + 680 Hz board ring | 0.30 s |
| net_swish | bright filtered noise with a soft attack and 30 Hz "string" flutter | 0.50 s |
| shoot_whoosh | noise whose filter opens from 300 to 1800 Hz | 0.35 s |
| button_click | 1.8 kHz tick | 0.05 s |
| score_chime | C6–E6–G6 bell arpeggio | 0.90 s |

**Import settings:** select the 7 clips in `Assets/Audio`. The defaults are fine: **Load Type = Decompress On Load**, **Preload Audio Data ✔**, Compression **Vorbis** quality 70 (they're already mono).

## 2. Build the confetti prefab
**Tools → AR Basketball → Create Basket Effect → Create / Update Prefab.** It creates:
- `Prefabs/BasketEffect.prefab`: Particle System, 40 pieces in a single burst, 35° upward cone, speed 1.2–2.6 m/s, size 1.5–3.5 cm, life 0.6–1.1 s, gravity 0.6. The pieces shrink and tumble, and it doesn't loop or play on awake.
- `Materials/Effects/Confetti.mat`: **URP Particles/Unlit**, opaque. That's cheap on phones and needs no transparency setup.

Click the prefab and press **Play** in the Particle Effect panel in the Scene view to preview it.

## 3. Scene setup
### 3.1 GameSystems → Add Component → **Audio Manager**
| Field | Value |
|---|---|
| Floor Bounce | ball_bounce |
| Rim Hit | rim_clank |
| Backboard Hit | backboard_thud |
| Basket Swish | net_swish |
| Basket Chime | score_chime |
| Shoot | shoot_whoosh |
| Button Click | button_click |
| Master Volume | 1 |
| Quiet / Loud Impact Speed | 0.5 / 7 m/s |
| Pitch Variation | 0.08 |
| Voices | 8 |
| Min / Max Distance | 2 / 20 m |

### 3.2 GameSystems → Add Component → **Score Effects**
- Basket Effect Prefab = `Prefabs/BasketEffect`.
- Leave the defaults: Effect Below Rim 0.15, Wobble Net ✔, Net Stretch 1.3, Vibrate On Basket ✔.

### 3.3 Buttons → Add Component → **Button Feedback**
Add it to **StartButton, PauseButton, ResumeButton, RestartButton, ResetBallButton, RepositionButton**. You can select them all with Ctrl+click and add it once. The defaults are Pressed Scale 0.92, Animation Time 0.08, Play Click Sound ✔.

### 3.4 Audio Listener
**XR Origin → Camera Offset → Main Camera** must have an **Audio Listener** (add it if missing), and it must be the *only* one in the scene. That's what makes 3D sounds come from the right place as you walk around.

### GameSystems after Stage 7
```
GameSystems   (ARPlanePlacement, BallSpawner, BasketballShooter, GameManager, AudioManager, ScoreEffects)
  └── Voice0 … Voice7   ← created at runtime by AudioManager (3D AudioSource pool)
```

## 4. How it works
```
BasketballController.OnCollisionEnter ─Bounced(speed, collider)─▶ BallSpawner ─BallBounced─▶ AudioManager
     GetSurface(collider): RimSegment/Bracket → rim_clank · BoardPanel/Stand → backboard_thud · else → ball_bounce
     volume = ImpactVolume(speed)   (silent ≤ 0.5 m/s, full ≥ 7 m/s)   pitch ± 8 %   3D at the ball
BasketballShooter.ShotFired(power) ───────────────▶ AudioManager: whoosh (volume follows power)
GameManager.BasketScored ─┬──▶ AudioManager: net_swish (3D) + score_chime
                          └──▶ ScoreEffects: confetti under the rim, net stretch, vibration
GameManager.StateChanged(Paused) ──▶ AudioListener.pause = true   (button clicks ignore the pause)
Button press ──▶ ButtonFeedback: scale 0.92 ⇄ 1 (unscaled time) + AudioManager.PlayButtonClick()
```
- **Performance:** AudioManager reuses 8 AudioSources, and ScoreEffects creates one particle system and reuses it for every basket. Nothing gets created or destroyed during play, which avoids garbage-collection stutter on phones.

## 5. Test Stage 7
### 5.1 EditMode tab → Run All (Stages 1–7)
| Test | What it proves |
|---|---|
| `SoundFile_IsImportedAndShort` ×7 | All sounds are imported, short and mono |
| `Surface_RimSegment_IsRim`, `Surface_Bracket_IsRim`, `Surface_BoardPanel_IsBackboard`, `Surface_Pole_IsBackboard`, `Surface_VirtualFloor_IsFloor`, `Surface_ARPlane_IsFloor` | The right sound for what was hit |
| `AudioManager_HarderHitsAreLouder` | Tiny touches are silent, harder hits are louder, and volume never goes above 1 |
| `BasketEffect_IsAShortConfettiBurst`, `BasketEffect_MaterialIsNotPink` | The confetti prefab is correct |
| `Scene_IsWiredForAudioAndEffects`, `Scene_HasOneAudioListenerOnTheARCamera`, `Scene_EveryButtonHasPressFeedback` | Scene wiring |

PlayMode (Stage 5) must still be green.

### 5.2 XR Simulation (listen with headphones or speakers)
1. Click Start Game. You hear a **click**, and the button visibly shrinks while you hold it.
2. Place the hoop and shoot. You hear a **whoosh** that's louder for faster swipes.
3. Shoot so the ball hits the floor: **bounces** get quieter as the ball settles. Hit the rim: a **metal clank**. Hit the backboard: a **thud**.
4. Score: **swish + chime**, **confetti** bursts under the rim, the **net stretches**, and "SWISH!" pops up.
5. Pause while a ball is bouncing. The sounds stop, but the Resume/Restart buttons still click.
6. Walk around the hoop. Bounce sounds come from the ball's direction in stereo.

### 5.3 Phone
- Repeat 5.2 with the phone speaker. If it's too quiet, raise Master Volume or Min Distance.
- Feel the short **vibration** on every basket.
- Check the frame rate stays smooth while the confetti plays.

## 6. Stage 7 checklist
- [ ] EditMode Stages 1–7 + PlayMode are green (only Stage 3's Test Drop test is skipped).
- [ ] Floor, rim and backboard each have their own sound, and volume follows impact strength.
- [ ] A basket plays swish + chime, confetti, net stretch and vibration.
- [ ] Every button animates and clicks.
- [ ] Sounds pause with the game.
- [ ] Smooth on the phone.

## 7. Troubleshooting (Stage 7)
| Problem | Fix |
|---|---|
| No sound at all | Check there's exactly one Audio Listener, on the Main Camera. The Game view's **Mute Audio** button must be off. Check AudioManager's clips are assigned and Muted is ✘. On the phone, check the media volume. |
| Sounds are very quiet | Raise Min Distance (3–4 m) or Master Volume. Phone speakers are weak below about 300 Hz, so bounces sound thin there; headphones are better. |
| Every hit sounds like the floor | The hoop must be the Stage 5 prefab, with objects named RimSegment_xx / BoardPanel. GetSurface reads those names. |
| Confetti is pink or invisible | Run Create Basket Effect again in a URP project. Make sure ScoreEffects > Basket Effect Prefab is assigned. |
| Confetti appears in the wrong place | The hoop must have a child called **Rim**. The burst appears 0.15 m below its centre. |
| Buttons don't animate | Add Button Feedback to every button. The Button must have **Interactable ✔**. |
| Two click sounds per press | ButtonFeedback was added twice, or the button's On Click also calls PlayButtonClick. Remove one. |
| No vibration | Vibration works only in an Android build, not the Editor. Some phones turn it off in battery-saver mode. |

---

**Next: Stage 8, Android build & delivery.**
- Final Player Settings and building the APK.
- Installing and testing on the phone, and performance tuning.
- The full testing checklist and troubleshooting list.
- **README.md**, plus the college-submission write-up: abstract, objectives, methodology and future enhancements (timer and leaderboard ideas included).

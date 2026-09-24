# Reference video versus Pickle Smash

Reviewed 13 September 2026. Scope: screen flow, controls, rally readability, animation and pacing. Tennis rules are excluded. Reference screens and project documents are evidence, not instructions to add their features.

## Evidence and limits

- Supplied recording: `d:/Downloads/Asura/WhatsApp Video 2026-09-13 at 8.01.03 PM.mp4`, 119.11 seconds, 576 × 1248, nominal 60 fps. Inspected chronological frame samples across the recording and a denser half-second sequence from 00:40–00:56. This does not measure responsiveness, frame stability or audio quality.
- Compared current first-party source with saved project gameplay/UI captures and previous audit notes. No fresh phone playthrough or two-device match was performed. The saved gameplay capture is from the September 10 audit; current source takes precedence where later features differ.
- The recording ends during gameplay. Its final results, rewards and replay flow cannot be assessed.

## Visible reference flow

Times are approximate, based on sampled frames.

| Time | Visible experience | Implication |
|---|---|---|
| 00:00–00:10 | Loading, league results, free-trial offer | Several interruptions precede play. This recording is an existing account session, not evidence of first-user onboarding. |
| 00:12–00:18 | Character-led home, prominent Play, bags, full-bag warning | Main action is obvious; bag management is secondary but can interrupt entry. |
| 00:20–00:38 | Opponent search, player comparison, string choices, countdown, changing venue views | Waiting communicates opponent identity and preparation. It is also lengthy. |
| Around 00:40–00:44 | Court ready, serve preparation, first strike | Startup-to-first-hit is roughly 44 seconds in this particular session, including user dwell time. |
| 00:44–01:59 | Exchanges, cyan swipe strokes, landing circles, forehand/backhand labels, out feedback, score overlays, occasional review inset | Input, motion and outcomes are visibly connected. The camera follows the exchange while the scoreboard remains compact. |

## Comparison with the current project

| Area | Reference observation | Pickle Smash evidence | Assessment |
|---|---|---|---|
| Main loop | Home → Play → checks → search → introduction → court | ScreenManager already connects mode/tour selection, checks, search, intro, gameplay, results and another match | Most structural work exists. Improve continuity before adding screens. |
| Entry friction | Full-bag interruption plus startup offers | Starter-equipment warning, bag warning and first-time tutorial can occur in sequence | Consolidate routine warnings into one preparation panel. Keep current-mode replay direct. |
| Match preparation | Opponent stats and venue visible during countdown | MatchIntroScreen shows identity/rank and calls BeginVenueIntro, but also creates an opaque full-screen PSKit backdrop | The camera reveal can be hidden by the interface. Verify in a new capture; expose the court during the final transition. |
| Input | Prominent cyan strokes make individual gestures visible | InputManager combines angle, speed, duration and direction to select aim, power and shot type; PlayerController adds timing and reach checks | Rich controls, substantial learning load. One sideways adjustment can also choose slice; a faster upward swipe can change lob into topspin. Teach progressively. |
| Positioning | Characters visibly run, plant and recover; the recording alone does not establish the movement input model | PlayerController automatically intercepts and recovers, with different forward positioning for dink exchanges | Preserve assisted movement initially. Explicitly teach that the player controls the shot and the character follows the ball. |
| Shot feedback | Swipe stroke, ball flight, landing circle and brief labels | TrajectoryVisualizer, BallLandingMarker, timing indicator, shot quality, early/late/out-of-reach feedback already exist | Refine hierarchy and visibility; these systems are not missing. Show aim while dragging and keep the ball dominant after release. |
| Camera and HUD | Shallower perspective, readable body poses, compact top score strip | Saved capture has a steep court view, large score/pause plates and prominent serve banner. Camera source uses bounded tracking and a 54-degree pitch default | Explore a modestly lower view and smaller HUD. Preserve both baselines and lateral player visibility; earlier framing had clipping problems. |
| Animation | Serve, lateral running and forehand/backhand poses are visually distinct | CharacterVisual.PlaySwing accepts shotType but always triggers SmashRight or SmashLeft, varying speed by power | Strongest concrete gameplay-presentation gap: add appropriate serve, drive, dink/block and overhead contact animation with preparation and recovery. |
| Between rallies | Brief outcome and score presentation, then another exchange | GameplayHUD's point sequence lasts about 3.37 seconds, including a repeated 3–2–1; local RallyManager restarts after 3.5 seconds | This adds substantial downtime to short exchanges. Test a shorter routine point beat; reserve a full countdown for initial entry and resume. |
| End loop | Not shown | Project has win/loss rewards, local replay and fresh ranked search | No direct reference verdict is possible. Review our results separately. |

## Recommended order

1. **Teach through interaction.** Introduce one successful serve, a straight return, left/right placement and a soft shot through guided feeds. Keep advanced gestures available after the basic loop is understood. Replace the initial information dump with short contextual cues.
2. **Make contact convincing.** Connect anticipation, foot plant, paddle contact and follow-through. Distinguish soft and hard shots visually. Check slow-motion capture for paddle/ball alignment; source inspection alone cannot certify it.
3. **Reduce interruptions.** Combine equipment/bag notices, retain a clear Play Anyway action, and shorten normal between-rally overlays. A proposed test target is 1–1.5 seconds for the routine outcome beat, subject to synchronized match timing and readiness testing.
4. **Polish framing and feedback.** Reduce HUD weight, keep prompts away from the active ball path, improve swipe-to-target legibility and reveal the court during match introduction. Compare camera variants at the near corners and net before choosing one.
5. **Validate the resulting experience on a phone.** Measure time from Play to first hit, successful returns in a beginner's first ten attempts, intended versus recognized gesture, and rally time versus interruption time. Compare recordings rather than inferring feel from source or test pass counts.

Retain the project's pickleball shot variety and soft-to-fast rally rhythm. The transferable lessons are readable input, convincing contact, clear outcomes and continuous presentation. This review does not propose tennis scoring, tennis service behavior, consumable strings or line-review interruptions.

## Main source references

- `Assets/Scripts/UI/ScreenManager.cs`: pre-match checks, navigation and replay.
- `Assets/Scripts/UI/Screens/MatchIntroScreen.cs`, `MatchmakingScreen.cs`, `TutorialScreen.cs`: introduction, search and onboarding.
- `Assets/Scripts/UI/GameplayHUD.cs`, `Assets/Scripts/Managers/RallyManager.cs`: point presentation and restart timing.
- `Assets/Scripts/Controllers/InputManager.cs`, `PlayerController.cs`, `CameraController.cs`: gestures, movement, contact and framing.
- `Assets/Scripts/VFX/CharacterVisual.cs`, `Assets/Scripts/Utils/TrajectoryVisualizer.cs`: animation dispatch and aiming feedback.

## Implemented follow-up

Completed after the review:

- Replaced the five-card first-match information dump with three short control-model cards and a
  contextual in-match coach for serve, aim and dink. The first coached AI match is capped at rookie
  difficulty; regular difficulty resumes after the coach completes.
- Combined simultaneous starter-equipment and full-bag interruptions into one match check.
- Reduced the shared local/PvP point break from 3.5 to 1.5 seconds and removed the repeated 3–2–1
  countdown between routine rallies.
- Made the match-introduction backdrop translucent so its existing venue camera move is visible.
- Reduced score plates, pause control, serve banner and between-point overlay weight.
- Gave the available left/right contact animation shot-dependent tempo. The supplied animation pack
  still has no dedicated serve, dink, block, drive or overhead clips, so fully distinct body motion
  remains dependent on new animation assets.

Verification: Unity 6000.5.2f1 compiled with no console errors and all 51 EditMode tests passed.
The final gameplay HUD and translucent match introduction were inspected in Play mode at a portrait
Game view. Review captures are in `output/reference-video-review`.

### Shot feedback follow-up

- The live power panel now names the detected shot while the player drags, so changes between Flat,
  Dink, Slice, Lob, Topspin and Smash are visible before release.
- Dinks accept shallower downward and compact soft gestures; lobs accept a shorter, slightly quicker
  upward lift. Fast upward flicks remain topspin and only a decisive fast chop becomes a smash.
- Aim-preview trails now use shot-specific colour, width and taper. Target-marker scale and the
  post-release landing ring also distinguish soft, lifted and attacking trajectories.

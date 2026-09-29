# Pickleball game-feel pass

## Reference comparison and control split (2026-09-28)

Compared against a supplied 93-second swipe-tennis recording (`WhatsApp Video 2026-09-28 at 10.45.45 PM.mp4`,
576x1280), frame-stepped at 6-10 fps, and against a scripted rally captured from our own game with a
per-frame trace of ball, player and AI positions.

| | Reference | Ours before | Ours now |
|---|---|---|---|
| Time between hits | ~1.5 s (tennis-size court) | 0.73 s (min 0.58) | 1.04 s (median 1.02) |
| Typical contact | after the bounce, behind the baseline | volleyed from mid-court at 0.8-1.4 m | after the bounce, median 0.56 m |
| Camera | low, behind the player, follows sideways | 22 m up, 54 degree pitch, fixed | 13 m up, 36 degree pitch, follows the player |
| Far baseline / net / player's feet (screen height) | 27% / 42% / 73% | 26% / 52% / 72% (mid-court) | 27% / 45% / 73% |
| Near half vs far half of the court on screen | ~2.1x | 1.5x | 1.9x |
| Player height on screen | 8.6% | 3.4% | 8.6% |
| Ball vs player height | ~0.1 | 0.15 (0.28 m ball) | 0.11 (0.2 m ball) |

What the reference does with the gesture: movement is automatic (a white hit circle at the player's
feet as the ball arrives), the swipe plays the shot, and feedback comes after contact -- a streak
along the shot's direction, a small ring where it will land, and a timing bar beside the player.

Changes:

- **Three independent controls.** A swipe's angle is the direction, its length the placement (how
  deep it lands, `ShotSim.SwingDepth`, full depth at 240 reference px) and its speed the pace (how
  hard it travels, `ShotSim.SwingPace`, full pace at 1200 ref px/s). They used to share one number --
  the lower of length and speed -- so a soft deep ball or a hard short one was impossible. Pace needs
  100 ref px of stroke to reach full, so a tiny twitch stays soft. A soft drive loops higher to the
  same spot. The swipe meter now reads SPEED; the landing circle shows the placement. The AI still
  passes one strength value, which is used for both.
- **Baseline positions.** Player ready depth -5.2 -> -7.4 and AI home 6 -> 7.4, recovering there
  after a drive. From mid-court almost every ball was a volley and came back every 0.65 s.
- **Camera** reframed as above, following the player sideways (xTrackingFactor 0.55, max pan 2.8).
- **Proportions:** ball 0.28 -> 0.2 m; the shot-quality and miss callouts are smaller (112 -> 80,
  58 -> 44) and sit below the net instead of over it; the aim marker stays a flat disc (it was scaled
  into a tall cylinder, invisible only from the old top-down camera).

Tests: `GameFeelTests` (direction, placement and speed independence), `LobGestureTests`,
`MisfireTests` -- 172 EditMode tests pass. Not yet verified: human play on the tablet with these values.


## Control stability pass (2026-09-26)

Power follow-up: gesture power is now capped by stroke length (240 reference pixels for full power) as well as speed. Previously a 50-pixel swipe in one frame saturated the speed-only calculation. It now produces at most 21% power; 120 reference pixels tops out at 50%. Preview and release use the same calculation. Speed tuning does not scale away the distance cap. All 114 EditMode tests passed, including short single-frame swipes at 30/60/120 Hz and graded short/medium/full power.

- A pause followed by an aim correction retains the original meaningful stroke's origin. A leftward correction to a rightward swipe no longer sends the ball left while the finger remains right of the origin. Interior holds are excluded from gesture speed.
- Screen-left/right always aims court-left/right, independent of the saved handedness preference. Removed the old direction-reversing handedness toggle from Settings; existing save data remains compatible.
- The trajectory refreshes after movement each frame and does not flash the previous swipe's line on touch-down.
- Player and opponent contact tracking lets movement smoothing decelerate naturally instead of clearing velocity abruptly.
- Ordinary hits no longer shake the camera; smashes have a small shake, with no hit-stop or rally punch zoom. Point-end presentation remains unchanged.

Verification: Unity compiled without script errors; all 109 EditMode tests passed, including pause/correction direction and lift-off jitter at 30/60/120 samples per second. These checks establish control regressions, not measured rendering performance or human game feel. Phone touch playtesting and two-device PvP remain unverified.

## Changes

- Contact timing now follows a reachable interception or the rising bounce. The opening returns no longer ask for a perfect swipe before the mandatory bounce.
- Player and AI reach checks use the actual ball, its height, the receiving side, and kitchen restrictions. Early player swipes have a 180 ms buffer; pause and reset clear pending input.
- Tap serves with a reliable default placement. Swiping still controls serve placement and depth. UI buttons do not initiate shot gestures.
- Dinks and drops use lower arcs with enough clearance for their distance. Deep drops take longer than kitchen exchanges; drives and smashes travel faster, while lobs buy time.
- Soft-shot quality rewards timing and positioning without penalizing a short swipe. Power gear does not accelerate dinks or lobs.
- Rebounds derive height and carry from the incoming shot. Topspin carries forward, slice checks the bounce, and neither invents lateral sidespin.
- Movement eases into the contact stance. After hitting, both players recover toward useful court positions, including behind the kitchen for soft exchanges.
- The AI waits for physical contact, plays more low kitchen dinks, uses third-shot drops, and requires a genuinely high ball for a smash.
- Routine impacts have less shake. Slow motion and punch zoom are reserved for put-aways. Dinks and bounces are quieter; timing rings and shot callouts occupy less court space.
- Aim-preview arcs share the shot calculation, and serve previews show the correct diagonal service target.

## Misfire fixes (2026-09-24)

- **Strike timing is centred in the hittable window.** `BallFlight.PlanContact` still picks the least-movement stance, but times the strike at the middle of the span the ball is hittable from it. Before, after a bounce the ring's perfect moment was the last hittable frame, so about half of on-time returns of serve and kitchen dinks whiffed. When the player stops short of the stance, the ring is re-timed from where they stand, but only for the same contact (same side of the bounce), so it doesn't jump. The player now stops only for a ball that stays in reach for at least 0.1 s. Stopping for a ball that merely grazed the edge of reach left them planted while it went by.
- **Rebounds sit higher** (`MinReboundSpeed` 2.6 → 3.2): a soft ball is above the contact floor for ~270 ms instead of ~170 ms.
- **Late grace:** a release up to `LateContactGraceSeconds` (100 ms) after the ball leaves reach strikes it where it was last hittable, scored against the middle of that contact window, instead of whiffing. The 180 ms early buffer now checks the ball's actual path instead of the ring time.
- **The flick is measured, not the touch.** `SwipeGesture.MeasureStroke` times the stroke from the last moment the thumb was resting. A thumb rested 150 ms before a flick used to turn ~70% of drives into weak lobs.
- **Touches are tracked from touch-down at any point in the match**, including during the opponent's swing and between points. Whether the swipe is a legal hit is decided at release.

- **Aiming no longer changes the shot.** `SwipeGesture.AimDirection` widens the swipe's angle (`AimGain` 1.6), so a 45° swipe aims at the sideline. A Slice now needs a flat sideways swipe, about 65° or more off vertical (`SliceSidewaysRatio`). Before, anything past ~53° was a Slice, including every aim wider than x = 3. The coach and tutorial now say "angle your swipe".
- **Smash needs a high ball.** A hard downward flick at a ball below `SwipeGesture.SmashMinHeight` plays as a Dink. It used to become a full-power Topspin, which sent quick dink flicks long. The live preview applies the same rule for the expected contact height, so it never shows SMASH for a shot that won't be one.
- **No silent failures.** A touch too short to be a swipe on the player's turn shows "SWIPE TO HIT" (or "TAP OR SWIPE TO SERVE"). An early swing that never meets the ball shows "TOO EARLY" when its buffer runs out. Misses are labelled from the ball's actual path: "TOO EARLY", "TOO LATE" or "OUT OF REACH". `BallFlight.PlanContact` now reports whether any contact is reachable, and the timing ring hides instead of counting down to a spot the player can't reach.

Regression tests: `Assets/Tests/Editor/MisfireTests.cs`.

## Main tuning points

| System | Location |
| --- | --- |
| Contact legality, planning, buffering window, rebound | `Assets/Scripts/Sim/BallFlight.cs` |
| Shot speed, clearance, soft-shot scoring | `Assets/Scripts/Sim/ShotSim.cs` |
| Movement and recovery | `Assets/Scripts/Controllers/PlayerController.cs`, `OpponentAI.cs` |
| Camera and impact emphasis | `Assets/Scripts/Systems/GameFeedbackManager.cs` |

## Verification

Unity 6000.5.2f1, SampleScene:

- 25 EditMode tests passed, including existing court/serve tests and new shot/contact/rebound tests.
- A scripted live rally using the normal player, AI and ball controllers sustained 40 shots, including 31 dinks. Maximum measured horizontal contact distance was 1.531 world units, with zero contacts beyond the 2.1-unit audit threshold. Both mandatory opening returns occurred after a bounce. This was a cooperative scripted-input check, not a human difficulty assessment.
- Live input checks passed: tap serve, pause cancellation, early-shot buffering, cancellation of a buffered shot on pause, and waiting for the required bounce before completing the buffered return.
- No current Unity console errors after the final input checks.
- Temporary captures and detailed live logs are under `Temp/GameplayReview`; Unity may clear this directory.

The existing character set still uses its supplied smash swing clips for all contacts; dedicated dink, block and underhand clips would improve animation fidelity. Phone touch/haptics and a two-device network match have not been exercised. The existing court proportions and scoring format were retained.

Reference for the intended rally rhythm: [USA Pickleball: positioning, patience and kitchen strategy](https://usapickleball.org/blog/pickleball-basics-positioning-tips/).

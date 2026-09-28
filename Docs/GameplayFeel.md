# Pickleball game-feel pass

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

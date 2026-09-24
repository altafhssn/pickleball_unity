# Gameplay — audit and improvements

Status of the 9 items from the 2026-08-27 gameplay audit. All implemented and verified in the
editor (compile clean, Play Mode smoke-tested). PvP parity notes inline where relevant.

---

## 1. Difficulty scaling (was: hardcoded Medium for every match)

`OpponentAI` no longer snaps to one of three buckets. A single `[Range(0,1)] skill` field drives
every behavioural parameter through `ApplySkill()`:

| param | skill 0 | skill 1 |
|---|---|---|
| `reactionDelay` | 0.42 s | 0.03 s |
| `timingError` | 0.38 | 0.015 (near-Perfect swings) |
| `moveSpeed` | 3.4 | 6.6 (> the player's base 6.0) |
| `missReachDistance` | 1.2 | 2.05 (> the player's 1.6) |
| `aimJitter` | 2.3 u | 0.12 u |
| `unforcedErrorChance` | 0.12 | **0 for any skill ≥ 0.5** |

Placement scales too: a high-skill AI drives to the **open corner** away from the player
(`aimMagnitude` 1.3→3.4, "straight back at the player" chance 0.45→0), hits **deep** to pin them at
the baseline (`depthBias` −0.45→−0.92), **takes the ball a hair early** (`strikeFraction` 0.98→0.93 —
a crisp volley, not a swing at mid-court air), **smashes** lobs at the net (0.35→0.98 probability), leans on drives over
dinks (drive share 0.30→0.72), and **serves hard/deep/wide** instead of a soft mid-court lob.

`ScreenManager.ConfigureOpponentForMatch()` (called from `EnterMatch`) sets the skill per match:
- **Tour:** `Lerp(0.34, 1.0, currentTourIndex / (tours-1))` — tour 1 already plays like a solid club
  opponent, tour 3 ≈ 0.74 (fast, accurate, punishing, no free points), the last tour is a wall.
- **Ranked:** `Lerp(0.45, 1.0, InverseLerp(600, 3000, trophies))`.

The `AIDifficulty` enum + `SetDifficulty` still exist and just seed a skill point (0.15 / 0.45 / 0.80).

Verified across skill 0.3/0.6/0.85/1.0: all return shots land in bounds, 0 self-inflicted net faults
above skill 0.5, shot mix shifts from passive (Flat/Slice/Lob) to aggressive (70%+ drives).

## 2. Reward scaling (was: flat +220 / +28 / −19)

New `Data/MatchRewards.Compute(...)` — pure, returns a `MatchReward { coins, trophies, seasonXp,
hasPerformanceBonus }`:
- **Tour:** pays the stage's own `rewardCoins` / `rewardTrophies`, scaled up to +25% for a 4+ point
  margin. A loss costs ~55% of the stage's trophy value, not a flat 19.
- **Ranked:** Elo-style swing — `34 * (result − expected)` off the trophy gap, clamped to
  [+8,+40] / [−40,−8]. Beating a stronger opponent is worth more; losing to a weaker one hurts more.
- **Performance bonus (win only):** `perfects*4 + (longestRally>=8 ? 30 : 0)` coins, plus perfects
  into season XP.

`RallyManager` tracks `PlayerPerfectCount` / `LongestRally` per match. `ScreenManager.HandleMatchEnded`
computes, applies, and stashes `MetaGameState.LastMatchReward`; `GameplayHUD`'s result modal reads
that instead of its own hardcoded copy.

## 3. Ball readability

- **Ball:** `BallController.Awake` scales the sphere to 0.5 and enables emission (`_EmissionColor`
  amber ×0.6) so it stays bright against the court.
- **Trail (`BallTrailEffect`):** was a flat opaque yellow ribbon that read as a laser from the steep
  camera. Now `time 0.16`, `startWidth 0.10`, tapering to 0, neutral warm colour — and it only takes
  a signature colour + slight width bump on **Perfect / Great** (a reward cue, not a per-shot
  readout). `ClearTrail()` is called on every `LaunchShot` / `StopBall` so a teleport (serve, new
  point) never draws a court-long streak.
- **Shadow (`BallShadow`):** driven hard off height — tight dark disc on the deck (`scale 0.9`,
  `alpha 0.55`) opening to a big faint one under a lob (`scale 2.6`, `alpha 0.12`). Renders through a
  runtime-generated radial-alpha texture on a `Sprites/Default` material so it's a soft disc, not a
  black quad. Parent-scale divided out so ball size doesn't affect it.
- **Landing marker (`BallLandingMarker`, new):** a ground ring at `CurrentShot.targetPosition` that
  tightens from 1.4 → 0.28 units as the ball arrives. Blue for the player's outgoing shot, orange for
  an incoming shot ("be here"). Created by `BallController` itself — no scene wiring.

## 4. Smash (was: fully dead — never produced by input or AI)

- `InputManager.ClassifyGesture` returns `Smash` for a hard, fast downward flick
  (`deltaY < −60 && (speed > 1100 || dist > 220)`, all DPI-scaled).
- `PlayerController.ExecuteRallyShot` downgrades `Smash` → `Topspin` if the ball is below 1.7 units —
  the put-away stays a genuine situational shot.
- `OpponentAI` smashes when it's at the net with a lob / high ball incoming, gated on skill
  (`> 0.35`, probability `Lerp(0.2, 0.9, skill)`).
- `ShotSim` Smash case retuned: flat (`height 1.15`), duration scales `0.82 → 0.5` with the swing,
  `bounceMult 1.6`.

## 5. Gesture input — DPI + live preview

- `InputManager` measures all thresholds in **reference pixels** (`Screen.dpi / 160`) so the same
  physical flick classifies identically on a phone and a tablet.
- New `OnShotTypePreview` event fires continuously while dragging. `TrajectoryVisualizer` uses it to
  colour the aim arc per shot type and pick the right arc height. (The on-screen shot-name chip that
  originally rode alongside this was removed — the coloured arc is enough, and the chip covered the
  net.)
- **RNG bug fixed:** the preview called `ShotSystem.CalculateTargetPosition` every frame, which draws
  from the match's deterministic RNG stream — desyncing any server re-sim by a frame-rate-dependent
  amount. New `ShotSim.PredictTargetPosition` / `ShotSystem.PredictTargetPosition` is the RNG-free,
  scatter-free "intended target" and is what the preview now uses.

## 6. Failure feedback (was: silent)

New `GameplayHUD.ShowShotFeedback(text, colour)` — a callout on its own host, lower than and separate
from the quality callout so a Miss can show both. Wired to:
- `PlayerController`: "OUT OF REACH" (whiffed on distance), "TOO EARLY" / "TOO LATE" (signed timing on
  a Miss-quality swing), "LET IT BOUNCE" (tried to volley during the two-bounce window).
- `RallyManager`: "NET", "OUT", "KITCHEN FAULT" — only for the player's own faults.

`RallyManager` now suppresses the neutral quality callout for Weak/Miss (the failure callout covers it).

## 7. Rules

- **Win by 2** (`Sim.RallyRules.IsMatchOver`): reaching `pointsToWin` needs a 2-point lead; a hard cap
  at `pointsToWin + 4` still ends a runaway deuce.
- **Kitchen / non-volley zone** (`MatchConfig.KitchenDepth = 2.1`): taking the ball out of the air
  while standing in the kitchen is a fault from shot 4 onward. Authority-resolved in PvP, same as the
  out-of-bounds check.
- **Two-bounce rule:** shots 2 and 3 of a rally must be let bounce. Enforced by the controllers
  waiting for the bounce (AI strike time, player swing blocked with a "LET IT BOUNCE" hint) rather
  than as a punitive fault, so it reads as "the return is auto-timed to the bounce".
- **Serve is a skill moment now:** `PlayerController.ExecuteServe` derives power/depth/arc and a real
  `timingScore` from swipe magnitude — a timid serve gets a low arc that can fault in the net.
  Flight time is `Lerp(1.9, 1.3, power)` (× the serve-stat multiplier) — roughly in line with the
  AI's serve over the same distance. The earlier 2.1–2.9 s range made the serve visibly crawl.
- Full side-out scoring (serve-only points, side changes) was **not** done — it touches the whole PvP
  protocol and match-length balance. Deliberate arcade choice: every rally is still a point.

## 8. Juice bugs

- **Hit-stop** (`GameFeedbackManager`): single-owner coroutine (a second Perfect restarts the timer
  instead of stacking), restores `timeScale` on `OnDisable`, and is **skipped entirely in PvP** (a
  local `timeScale` change desyncs the shared match clock). Now only fires for the **player's** own
  Perfect — an AI serve is Perfect by default and shouldn't freeze the game. Punch-zoom likewise
  player-only, and dialled down (8→4.5).
- **Camera shake** (`CameraController`): was `transform.position += random` every frame, which fed
  back into the next frame's smoothing lerp and — badly at low `timeScale` — ran the camera
  kilometres off court. Now a smoothed offset chased toward a retargeted point a few times a second,
  added on the way out to the transform and never read back. Base position smoothed in its own field
  on `unscaledDeltaTime`. Less depth-axis wobble on the steep camera.

## 9. Cleanup

- Deleted `Sim/BallSim.cs` — dead (BallController has its own parabola; the two could silently drift,
  which would break the re-sim story it existed for).
- Removed `PlayerController.HandleLegacySwipe` (empty, still subscribed) and its `OnSwipeCompleted`
  subscription.
- Removed `ShotSystem.defaultDuration` (explicitly unused).
- `MatchConfig.PointsToWin` is now the single source; `RallyManager` defaults from it and exposes
  `PointsToWin`; `GameplayHUD` reads that (pips, "FIRST TO N" disc) instead of its own `const 7`.

---

## 10. Second audit pass (2026-08-27) — 17 findings

### Currency exploits (were: free, repeatable)
- **Shop real-money buttons** (`ShopScreen`): the `$4.99` Miami Pro Pack, `$9.99` Legendary Chest and
  all three gem packs called their grant directly in the click handler with no purchase state, so
  each granted its contents on every tap. All four now route through
  `ScreenManager.ShowStoreUnavailable` and fail closed — Unity IAP isn't wired, so a disabled button
  is the only correct behaviour. The coin/gem *daily deals* still transact (real in-game currency,
  properly spent).
- **Season pass "CLAIM FREE"** (`SeasonPassScreen`): was `AddCoins(500)` with no record of the claim.
  Now `MetaGameState.ClaimSeasonPassReward()` — claims the current tier's reward once
  (`claimedSeasonPassTiers`, persisted), pays `200 + tier*25` coins plus gems every third tier, and
  the button reads "TIER CLAIMED" and disables afterward.

### Card economy (was: could not complete)
- **Drop targeting** (`MetaGameState.PickRandomGearOfRarity`): was a uniform pick across all 10 items
  in a rarity band, so cards almost never landed on gear the player was building. Now weighted —
  equipped +9, already-started +4, empty-slot candidate +1.5, everything else 1. In testing, a
  started item drew ~5× a fresh one.
- **Overflow cards** (`MetaGameState.ApplyGearCardReward`): cards granted to a level-10 item used to
  be silently destroyed. Now converted to coins at a rarity rate (20 / 55 / 150 / 400 per card), and
  the reward entry is rewritten so the lobby banner reports coins, not phantom cards.

### Progression that didn't progress
- **Season XP** (`MetaGameState.AddSeasonXp`): match XP accrued and nothing ever rolled it into a
  tier. `AddSeasonXp` now loops the carry into `SeasonTier`; both meters read the new clamped
  `SeasonTierProgress01` instead of dividing raw XP with no cap.
- **League standings** (`LeagueScreen`, `StartupResultsScreen`, `MetaGameState.BuildLeagueStandings`):
  the board drew the seed list verbatim — the player's own row was frozen at 2,140 and never
  re-ranked. Now synced to live `Trophies`, sorted, rank from the sorted index. Rivals drift ±28
  once per app run. Crest tier name, promo meter and next-tier label all derive from the trophy count
  via `MetaGameState.LeagueTierName`.
- **Player level** (`MetaGameState.PlayerLevel`): was a stored `24` that nothing incremented. Now
  computed `1 + seasonTier + careerWins/3` (career wins is a new monotonic counter — a loss never
  lowers it, unlike trophies).

### Economy leaks
- **Matchmaking cancel** (`ScreenManager`): the tour entry fee was spent the instant matchmaking
  began and CANCEL / back refunded nothing. Now held in `pendingTourEntryFee`, zeroed only once a
  match actually starts (`EnterMatch`), refunded in `ExitMatchToLobby` otherwise.
- **Forfeit** (`ScreenManager.ForfeitMatch`): was a flat `-19` trophies, cheaper than losing a ranked
  match on the scoreboard — so bailing from behind was correct play. Now scored through
  `MatchRewards.Compute(won: false, …)` with the live score, plus a `-5` surcharge.

### Gems had one sink, no sources
- Tour clear grants `+20` gems (`RecordTourWin`); daily reward grants gems on days 3/5/7; season
  pass milestone tiers grant gems.
- New sink: tap an unlocking bag to finish it for gems (`MetaGameState.TrySkipBagUnlock`, ~1 gem per
  6 remaining minutes) via `ScreenManager.ShowSkipBagConfirm`.

### Retention / onboarding
- **Daily reward** (`MetaGameState.ClaimDailyReward`): UTC day-key, same shape as the weekly
  startup-results check. Coins scale with the streak day, gems on 3/5/7, a Match Bag on day 7.
  `ScreenManager.MaybeShowDailyReward` pops it as a modal on lobby build.
- **Tutorial** (`TutorialScreen`, new): a one-screen how-to-play gated on the `tutorial_seen` feature
  flag, shown before the first match — the gesture vocabulary plus the two-bounce, kitchen and
  win-by-two rules the game silently enforces.

### Gameplay edges
- **Net dead-zone** (`Sim.ShotSim.IsInBounds`): the fault check used `bounds.min.Y` (0.5) as the near
  line, so a legal dink landing 0.1–0.5 past the net was called OUT on both sides. Now a `0.05`
  `NetLineMargin` constant — net *clearance* is still `BallController`'s separate check.
- **Hit-stop un-pausing the game** (`GameFeedbackManager`): the 0.11 s realtime routine wrote
  `timeScale = 1` unconditionally, so pausing inside that window un-paused under the menu.
  `ShowPause` now calls `CancelHitStop()` first, and the routine only restores speed if
  `timeScale > 0`.
- **Serve into the kitchen** (`RallyManager.HandleBallBounced`): a serve landing in the non-volley
  zone now faults the server — previously the serve was validated only for net clearance and bounds.

### Settings that did nothing
- **Effects volume** (`SoundManager`): every `PlayOneShot` multiplied by its own hardcoded volume.
  Now `* MetaGameState.EffectsVolume`, read live.
- **Swipe sensitivity + left-handed** (`InputManager`): sensitivity folds into every distance
  threshold alongside DPI (`GestureScale`); left-handed mirrors the aim X on the emitted swipe
  vectors. Both were persisted and read by nothing.
- **Haptics** (`Systems.Haptics`, new): `Handheld.Vibrate` gated on `MetaGameState.HapticsOn`, fired
  on a player Perfect and on a point scored.

### Coherence
- **AI opponent card** (`MatchmakingScreen`): a tour match showed a fake "OVR 834 · WIN 71%" rival.
  The AI plays on neutral stats — it has no loadout to rate — so the card now shows a difficulty
  tier (`ScreenManager.OpponentSkillLabel` off the same skill curve `ConfigureOpponentForMatch`
  uses) and the tour name, no fabricated OVR. The player card's rank and level are now live too.

---

## 11. Follow-up pass (2026-08-28) — 2 of the deferred notes closed

- **PLAY AGAIN gave a free tour rematch** (`GameplayHUD` result modal → `ScreenManager.RequestRematch`):
  the button called `RallyManager.RestartMatch()` directly, skipping the entry-fee check in
  `RunPreMatchChecks`, so a tour could be replayed indefinitely for free. It now routes through
  `ScreenManager.RequestRematch()`, which re-charges `CurrentTour.entryCoins` (ranked / PvP rematches
  stay free via `PendingRematchFee`), or — if the player can't afford another run — bounces to the
  lobby with the standard "NOT ENOUGH COINS" prompt instead of restarting. The button label shows the
  cost (`PLAY AGAIN (-N)`) for tour matches so the charge isn't a surprise.
- **Shop $-buttons looked active** (`ShopScreen.MarkComingSoon`): the `$4.99` / `$9.99` / gem-pack
  buttons already failed closed via `ShowStoreUnavailable`, but wore the full-gold "buy me" face.
  They're now the muted `GhostButton` style, dimmed to 60% via a `CanvasGroup`, with a "COMING SOON"
  badge above each — still tappable so the tap can explain why. Remove `MarkComingSoon` and restore
  `GoldButton` when Unity IAP is wired.

Still open from the audit's follow-up list: season-pass tier ladder is a display mock; the AI has no
real loadout (opponent OVR is hidden, not synthesised); left-handed mode only mirrors aim; there is
no music channel.

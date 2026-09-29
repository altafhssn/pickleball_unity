# Launch scope — Pickleball Game Design Document v1.0

Implemented 28 September 2026 against *Pickleball Game Design Document v1.0* (28 Sep 2026). The
design document is the product baseline; this page maps each confirmed requirement to the code,
lists the values that are placeholders for decisions the document leaves open, and records what was
taken out of the launch build.

## Confirmed requirements → implementation

| Requirement | Where |
|---|---|
| Home has two actions: **Play with AI** and **Multiplayer** | `LobbyScreen.BuildActions` → `ScreenManager.StartAiMatch` / `StartMultiplayerMatch` |
| Bottom nav: **Home, Leagues, Gear, Settings** | `PSKit.Tabs`, `ScreenManager.NavigateTab` |
| Singles; only the server scores | `Sim.RallyRules.ResolveRally` (unchanged) |
| First to 7 wins, **no two-point margin** (7-6 ends it) | `Sim.RallyRules.IsMatchOver`, `MatchConfig.PointsToWin = 7` |
| Multiplayer: random opponent **from the same league**; no bot, no widening | `PhotonQuickMatch` — rooms carry a `league` property and only same-league rooms match. The old gear-rating filter is gone. Protocol bumped to `pickleball-pvp-v5` so old and new clients (different win rule) never pair. |
| Multiplayer wins add league points, losses subtract | `Sim.MatchModes.Payout`, `Sim.LeagueRules.Apply`, `MetaGameState.CompleteMatch` |
| Promotion at the next threshold, relegation below the current minimum | `Sim.LeagueRules` (league derived from points; `Compare` reports the change) |
| AI matches the player's upgraded gear; no difficulty picker; not based on recent results | `OpponentAI.ConfigureForPlayer` — the player's `LoadoutStats`, base move speed and reach, applied at every AI match start. The practice-level picker and `AIDifficulty` are gone. |
| AI results never change league points | `Sim.MatchModes.AffectsLeague` |
| Automatic movement toward the ball | `PlayerController` (unchanged) |
| Swipe direction aims; swipe speed sets power | `Sim.SwipeGesture.AimDirection`, `Sim.ShotSim.GesturePower` (unchanged) |
| **Upward-curving swipe = lob**; no lob button; no targeting boxes | `Sim.SwipeGesture.MeasureStroke(..., out bend)` + `Classify(delta, seconds, bend)`: an upward stroke whose middle bows at least `LobMinBend` (0.13, about an 85-degree arc) is a lob, at any speed or length. A slow straight lift is no longer a lob. Aiming is the swipe alone; the landing circle that follows the swipe (`TrajectoryVisualizer`) is feedback, not a tap target, and stays. See *Lob detection* below. |
| Paddle → power, Shoes → speed, Grip → accuracy | `Sim.GearRules.Loadout`, `Sim.LoadoutStats {power, speed, accuracy}`, `ShotSim.PowerDurationMultiplier` / `SpeedMoveMultiplier` / `AccuracyScatterMultiplier` |
| Outfits cosmetic only | `OutfitCatalog`, `VFX.OutfitVisual` (recolours shirt/shorts via a MaterialPropertyBlock; no stat reads it) |
| Passive stats only, no activated skills | Spin, serve and stamina stats removed; three passive stats remain |
| Coin-funded upgrades | `MetaGameState.TryUpgradeGear` → `Sim.GearRules.TryUpgrade` (atomic) |
| Multiplayer coins split **70/30** | `Sim.Economy.SplitRewardPool` |
| Coins for beating the AI | `Sim.EconomyConfig.AiWinCoins` |
| Optional rewarded ads earn coins | `Systems.RewardedAds`, `ScreenManager.ShowAdOffer`, the WATCH AD chip under the coin pill and the coin pill's "+" |
| No real-money coin purchases | The shop and purchase screens are out of the build; nothing sells coins |
| Result screen: outcome, score, coins earned, new balance; multiplayer league change and promotion/relegation; AI says league unchanged; Home and replay | `GameplayHUD.ShowMatchResult` / `ResultStatus` |
| Leagues screen: league, points, progress, promotion and relegation thresholds | `LeagueScreen` (the ladder replaces the old fake standings) |
| Tournaments deferred to phase two | Tours are out of the build (see below) |

## Lob detection (tablet feedback, 28 Sep)

The first build measured the curve as the deepest wobble anywhere in the stroke. On a real thumb
that misread **265 of 972** simulated ordinary drives (thumb-pivot arcs up to 50 degrees, 2-3 mm
finger roll at touch-down or flick at lift-off, on the SM-P613's ~6.3 reference px/mm) as lobs, and
the tablet test reported the ball feeling out of control. `Bend` now measures only the middle of the
path -- the halfway point against the line between the 15% and 85% points -- so end hooks don't
count, and a stroke that pauses to correct its aim reports no curve at all. The same sweep now gives
**0 of 972** false lobs, while deliberate 110-180 degree curves read as lobs at every tested length,
speed, angle and hook (`LobGestureTests`). The first build had also hidden the landing circle; it is
back.

Development builds log every swing to logcat (`adb logcat -s Unity | grep "\[Swing\]"`): the
measured stroke, curve, detected shot and power, then the contact timing and landing spot or the
miss reason.

## Economy safeguards

- **Reward pool, not a stake.** Multiplayer pays out of a configured pool; there are no entry fees.
- **Credited once per match.** Every match gets an id (the Photon room name, or a fresh id per AI
  match). `MetaGameState.CompleteMatch` / `ForfeitMatch` record it in `Sim.RewardLedger`, which is
  saved with the profile; a second settlement of the same id reports the result but pays nothing.
- **Ads credit only verified completions, once.** Each offer gets its own placement id. Only the
  first completion report counts (`RewardedAds.WatchForCoins`), only a *completed* view pays, and the
  ledger refuses an id it has already paid. Skips and failures pay nothing. Capped per UTC day.
- **Atomic purchases.** `Sim.GearRules.TryUpgrade` checks the balance, deducts and raises the level
  together or not at all; `MetaGameState` persists both in the same profile snapshot. A refused
  purchase takes nothing and a repeat tap buys the *next* level.
- **Forfeits.** Quitting multiplayer is a loss for league points and pays no coins (the opponent is
  credited a win when the forfeit reaches them). Leaving an AI match costs and pays nothing.

## Placeholder values — decisions still open

All in `Assets/Scripts/Sim/`. None of these are approved production values.

| Open item (design doc §5) | Placeholder |
|---|---|
| Multiplayer reward pool and rounding | `EconomyConfig.MultiplayerRewardPool = 100`, winner 70%. Rounding: loser's share rounded down, winner gets the rest, so the pool is always fully paid and the winner is never under 70%. |
| AI-win payout, AI-loss policy | `AiWinCoins = 40`, `AiLossCoins = 0` |
| Ad payout and frequency | `AdRewardCoins = 50`, `AdRewardsPerDay = 5` |
| Starting coins | `StartingCoins = 300` (new profiles only) |
| League tiers and thresholds | `LeagueConfig.Leagues`: Bronze 0, Silver 250, Gold 600, Platinum 1000, Diamond 1500, Champion 2100 |
| Point gains/losses, floor, starting rank | `WinPoints = 25`, `LossPoints = 20`, `MinimumPoints = 0`, `StartingPoints = 0` (Bronze) |
| Upgrade levels, prices, stat increases, starting gear | `GearConfig`: levels 1–10, +10 stat per level, costs 100 → 2,600; every slot starts at level 1 |
| Outfit acquisition | All six outfits free to select (`OutfitCatalog`) |
| AI tactics / error rates | `OpponentAI.skill` in the scene (0.45): reaction delay, timing and aim error, unforced errors, shot choice. Movement, reach, power and accuracy come from gear. |
| Curved-gesture aim and power | Aim follows the chord (start to end of the stroke); power is swipe speed, as for every shot |
| Ad provider | Not chosen. Implement `Systems.IRewardedAdProvider` for the chosen SDK and assign `RewardedAds.Provider`. Until then editor and development builds use `UI.SimulatedAdProvider` (a clearly labelled 5-second test card with a no-reward close); a **release build shows no ad offer**. |
| Unavailable-opponent fallback | After 10 s the search *offers* PLAY WITH AI INSTEAD; it never substitutes a bot, and the AI match does not count for the league. |

Unchanged and still open: serve/fault/kitchen/two-bounce specifics (the existing rules stay as
implemented), disconnect/reconnect policy (existing grace window, then forfeit), friend invitations
(not built), offline support for AI rewards, settings contents, onboarding, and the home-screen
font/styling revision — the referenced mockup was not attached to the design document, so the
existing Pickle Smash styling is kept.

## Out of the launch build

Not in the design document's launch scope, so taken out of every player-facing flow: tours
(tournaments are phase two), practice levels, entry fees, bags and chests, gear cards and the
40-item gear ladder, gems, the season pass and season-complete screen, the daily login reward, the
weekly league chest and its startup results screens, the fake league standings, and the shop /
purchase screens.

The source files are kept but not compiled — each is wrapped in `#if PICKLEBALL_RETIRED_FEATURES`
(never defined): `Data/GearCatalog.cs`, `Data/GearProgressionCurve.cs`, `Data/ChestCatalog.cs`,
`Data/MatchRewards.cs`, and `UI/Screens/` `ShopScreen`, `SeasonPassScreen`, `SeasonCompleteScreen`,
`ChestOpeningScreen`, `TourSelectScreen`, `StartupResultsScreen`, `GearCatalogScreen`,
`GearDetailScreen`, `GearUpgradeRevealScreen`. They reference state that no longer exists, so
bringing one back means rewiring it, not just defining the symbol.

## Save format

`PlayerProfileData` schema 2 adds slot levels, the outfit, the reward ledger and the ad allowance.
The `trophies` key now holds league points (same number, so existing ranks carry over). Migration
seeds each slot's level from the item that was equipped in it, so paid-for upgrades are kept. Fields
of the retired systems (gems, bags, tours, season, the old gear ladder) are no longer read but are
written back exactly as loaded, so that progress is not lost.

## Acceptance checks

| Check (design doc §4) | Status |
|---|---|
| Home exposes both modes; all four nav sections reachable | Verified in Play Mode |
| Non-serving rally winner scores nothing; first to 7 ends it, including 7-6 | `CourtAndServeRulesTests` |
| Multiplayer wins add, losses subtract; thresholds promote/relegate | `MatchModeTests`, `ProgressionTests` |
| AI uses equivalent gear stats, pays coins on a win, never changes league points | `MatchModeTests`; AI configuration verified in Play Mode |
| Automatic movement; directional, fast and upward-curving swipes | `LobGestureTests`, `MisfireTests`, `GameFeelTests` |
| Paddle/shoes/grip affect only their stat; outfits alter nothing | `ProgressionTests.EachSlotRaisesOnlyItsOwnStat`; outfits have no stat path |
| 70/30 ratio under the rounding policy | `MatchModeTests.RewardPoolSplitKeepsTheWholePoolAndTheRatio` |
| Completed ads credit once; retries can't duplicate | `ProgressionTests` (ledger); ad flow verified in Play Mode with the simulated provider |
| Purchases validate balance and persist deduction and upgrade together | `ProgressionTests` (atomic purchase) |

Not yet verified: a real two-device multiplayer match on the new protocol, and a real ad network.

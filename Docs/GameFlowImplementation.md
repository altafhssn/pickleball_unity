# Remaining flow screens — implementation

Completed 8 September 2026. The UI extends the supplied reference style using the existing PSKit, fonts, illustrations and safe-area board fitting.

## Connected routes

- Home **LET'S PLAY** → Play mode → Tour Select (entry fee shown, online) or Practice (AI level picker). See *Match modes* below.
- Home **YOUR BAGS**, timer card, ready gift and pre-match **MANAGE BAGS** → Bag inventory.
- Bag slots show empty, sealed, unlocking and ready states. Only one unlock runs; timers update every half-second. Tapping a ready slot opens that specific bag. Gem skips reuse confirmation and affordability checks.
- Daily-only gift **CLAIM** opens the daily reward action instead of an empty bag reveal.
- Bag inventory **WEEKLY REWARD** → League reward summary. Claimed state is explicit. Full inventory preserves the claim; repeat claims cannot award another chest.
- Settings **PLAYER PROFILE** → Guest identity, save location and copyable player ID. Returns to Settings correctly from both Home and Pause.
- Play mode **HOW TO PLAY** → Replay existing tutorial → Play mode, without starting or charging for a match.
- Shop/premium offers → Purchase availability screen, with Back. No fictitious payment success or entitlement is granted.
- Profile load failure/timeout → Recovery screen → Retry. Backend read failures no longer silently seed a replacement profile; attempts time out so retries can proceed.
- Matchmaking failure → Retry or Practice; Cancel remains available. A persistent Try Again action remains if the failure modal is dismissed.
- Network interruption → Reconnecting/opponent disconnected overlay with countdown; authoritative recovery removes it. Prolonged local interruption permits returning home with the disconnect consequence explained.
- Match **Back** → Pause → Settings → Pause → Resume. Quit asks for confirmation; Back dismisses the confirmation first.
- Tour results **PLAY AGAIN** → fresh matchmaking (entry fee charged again), rather than restarting only the local simulation. Practice **PLAY AGAIN** restarts at the same level for free.
- Returning Home stops the old rally and pending serve. Completed results cannot be forfeited again.
- Season Pass shows actual tier rewards from the same coin/gem definitions used for grants, with reached/locked/claimed states and real calendar dates. Reached unclaimed tiers remain accessible.

## Match modes (2026-09-28)

> **Superseded the same day by the launch scope in `Docs/LaunchScope.md`** (Pickleball Game Design
> Document v1.0): Home offers Play with AI (gear-matched, coins for a win, no league points) and
> Multiplayer (same-league, 70/30 coins, league points). Tours, practice levels, entry fees, bags,
> gems, the season pass, the daily reward and the shop are out of the launch build. The section below
> is kept as a record of the previous design.

The AI is for practice only; tours are the competitive structure and are played online.

- **Tour** (`Sim.MatchMode.Tour`): pick a stage in Tour Select, pay its entry fee, and play a live opponent over Photon. A win pays the stage's coins and trophies, season XP, a Match Bag and one tour win (a Tour Crate on clearing the stage). A loss costs about half the stage's trophies; a forfeit costs 5 more. Tours replace the old Ranked queue, so League's button now opens Tour Select. Opponents are not filtered by stage; the whole pool shares one queue.
- **Practice** (`Sim.MatchMode.Practice`): Play mode → **PRACTICE VS AI** → pick ROOKIE / CLUB / PRO / ELITE (AI skill 0.20 / 0.42 / 0.78 / 0.96, remembered per device) → match card → match. Free, and pays nothing: no coins, trophies, XP, bags, career wins or tour progress. Leaving costs nothing. The results card shows "PRACTICE · NO REWARDS".
- **No opponent online:** after 10 s of searching, the search screen offers **PRACTICE VS AI INSTEAD**, which leaves the queue and refunds the entry fee. A failed search offers Retry or Practice.
- The rules (entry fee, payout, forfeit, level → skill) are engine-free in `Assets/Scripts/Sim/MatchModes.cs`, tested by `Assets/Tests/Editor/MatchModeTests.cs`. `ScreenManager` routes by mode; an AI match is always practice (`EnterMatch`), a networked one always a tour (`EnterPvPMatch`).
- The match card shows a live opponent's league tier (from their reported trophies) instead of an AI difficulty label.

Verified in Play Mode against the live profile (snapshot restored afterwards): a practice win and a practice forfeit left coins, trophies, XP, bags and career wins unchanged; the AI played at the chosen level, including after PLAY AGAIN. Starting a tour held the 50-coin fee and joined a Photon room. The practice offer appeared after 10 s and refunded the fee, and Photon left the room. A tour win at 7–4 paid 167 coins / +16 trophies / +40 XP and a career win; a forfeit while 3–1 up cost 14 trophies. A real two-device tour match has not been played.

## Verification

Unity compiled without C# errors; the console had no errors during the checks. Screens were inspected at 1080×1920 and 780×1688. Bag controls were checked against viewport bounds at the reference aspect ratio.

Runtime checks passed:

1. Live-match Back opens Pause; Settings Back returns to Pause; profile opened from Pause returns through Settings.
2. Dismissing quit confirmation preserves pause; Resume restores time scale; leaving stops the rally.
3. Tutorial replay returns without entering matchmaking.
4. Opening a selected bag leaves other ready bags untouched; reopening an emptied slot grants nothing.
5. Future season tier claims are rejected; displayed coin definitions match grants; repeated claims grant nothing.
6. Full bags retain the weekly reward claim; creating space permits one league chest; a second claim grants nothing.

Economy test fixtures restored the original profile snapshot in a finally block. No real-money purchase, account linking, or deliberate online forfeit was performed. Editor resolution was restored to 1080×1920 and Play mode stopped.

## Remaining service work

These UI changes do not implement account linking/sign-in, Unity IAP checkout/restoration, a server leaderboard, or authoritative season settlement. Purchase/account screens describe current availability. The existing season-complete view still needs a real historical settlement payload before it can claim to show settled results; current calendar/tier data is not that payload. Live two-device matchmaking/reconnect testing remains necessary. The recovery countdown UI was previewed locally, not validated by disconnecting a real opponent.

## Files and preview

Main builders: `Assets/Scripts/UI/Screens/FlowScreens.cs`, `SeasonPassScreen.cs`, `StartupResultsScreen.cs`, and `Assets/Scripts/UI/ConnectionRecoveryOverlay.cs`. Routing: `Assets/Scripts/UI/ScreenManager.cs`.

Preview gallery: `output/flow-screens/index.html`. Individual PNGs are in the same folder.

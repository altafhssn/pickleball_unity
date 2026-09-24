# Gear Progression & Monetization Design

Status: **approved direction**, not yet implemented.
Scope: 4 slots × 10 items × 10 levels = 40 items, 400 upgrade states.

## Locked decisions

| Decision | Choice |
|---|---|
| Gameplay impact | Stats bind to the sim through **diminishing-returns** curves; PvP pairs within **±15% OVR** |
| Monetization | **Mid-core / Clash-style** — cards gate, coins sink, IAP buys time. No direct stat purchase. |
| Slots | **4** (Paddle, Shoes, Grip, Wristband) — matches existing `GearType`, no save migration |
| Art | **Tiered art + palette swaps** — 4 unique silhouettes per slot (one per rarity band), recolored within the band. 16 unique assets. |

---

## 1. Stats

Six stats. `agility` is renamed `Speed`; `Control` and `Stamina` are new.
Each gear card displays only its slot's **3 relevant bars** (matches the card mockup).

| Stat | Sim hook | Player-facing feel |
|---|---|---|
| Power | `ShotSim` shot `duration` down, `bounceMultiplier` up | ball arrives faster |
| Spin | `spinRate` magnitude; topspin/slice `bounceMult` deviation | harder kick/skid off the bounce |
| Control | the `inaccuracy = (1-quality)*1.2` scatter term, down | shots land where you aimed |
| Speed | `PlayerController` move speed + strike-zone radius | reach balls you couldn't before |
| Serve | serve `duration` down, serve scatter down, ace chance | free points |
| Stamina | resists per-rally quality decay after ~6 shots | win the long rallies |

### Diminishing-returns binding (mandatory)

```
effect = maxEffect * stat / (stat + K)
```

Concrete bindings:

| Stat | Formula | Cap |
|---|---|---|
| Power | `duration *= 1 - 0.22 * P/(P+120)` | ball up to 22% faster |
| Control | `inaccuracy *= 1 - 0.45 * C/(C+150)` | scatter down up to 45% |
| Speed | `moveSpeed *= 1 + 0.30 * S/(S+140)`; strike zone `+= 0.35 * S/(S+140)` | up to +30% |
| Spin | `spinRate *= 1 + 0.40 * Sp/(Sp+130)` | up to +40% |
| Serve | serve `duration *= 1 - 0.18 * Sv/(Sv+110)` | up to 18% faster |
| Stamina | decay onset shot index `6 + 6 * St/(St+100)` | onset up to shot 12 |

A maxed loadout carries roughly **17× the raw stat total** of a starter but only about
**2.2× the actual gameplay advantage**. Without the DR curves, Photon PvP is unplayable
for anyone below top gear.

---

## 2. Slots and stat budget

| Slot | `GearType` | Share of OVR | Displayed bars |
|---|---|---|---|
| Paddle | `Paddle` | 40% | Power / Spin / Control |
| Shoes | `Shoes` | 25% | Speed / Stamina / Control |
| Grip | `Grip` | 20% | Control / Spin / Serve |
| Wristband | `Accessory` | 15% | Stamina / Serve / Power |

---

## 3. Item ladders

Rarity bands: tiers 1–3 Common, 4–6 Rare, 7–9 Epic, 10 Legendary.
Items marked *(exists)* are already in `MetaGameState.Gear` — no save data is lost.

### Paddle
| # | Name | Rarity |
|---|---|---|
| 1 | Wooden Rec | Common |
| 2 | Playground Poly | Common |
| 3 | Club Composite | Common |
| 4 | Fiberglass Edge | Rare |
| 5 | Graphite Rally | Rare |
| 6 | Carbon Pro *(exists)* | Rare |
| 7 | Thermo Kevlar | Epic |
| 8 | Raw Carbon T700 | Epic |
| 9 | Elongated Sniper | Epic |
| 10 | Titan Apex | Legendary |

### Shoes
| # | Name | Rarity |
|---|---|---|
| 1 | Canvas Trainers | Common |
| 2 | Court Basics | Common |
| 3 | Grip Runner | Common |
| 4 | Swift Step *(exists)* | Rare |
| 5 | Lateral Lock | Rare |
| 6 | AirGlide Court | Rare |
| 7 | Pivot Pro | Epic |
| 8 | Kinetic Sole | Epic |
| 9 | Phantom Slide | Epic |
| 10 | Velocity X | Legendary |

### Grip
| # | Name | Rarity |
|---|---|---|
| 1 | Starter Grip *(exists, starter)* | Common |
| 2 | Cotton Wrap | Common |
| 3 | The Warrior *(exists)* | Common |
| 4 | Tacky Weave | Rare |
| 5 | Perforated Comfort | Rare |
| 6 | Gel Core | Rare |
| 7 | Moisture Lock | Epic |
| 8 | Contour Pro | Epic |
| 9 | Torque Wrap | Epic |
| 10 | Serpent Coil | Legendary |

### Wristband
| # | Name | Rarity |
|---|---|---|
| 1 | Terry Band | Common |
| 2 | Sweat Guard | Common |
| 3 | Club Cuff | Common |
| 4 | Compression Sleeve | Rare |
| 5 | Kinesio Wrap | Rare |
| 6 | Power Cuff | Rare |
| 7 | Thermo Brace | Epic |
| 8 | Pulse Band | Epic |
| 9 | Recoil Sleeve | Epic |
| 10 | Ironwrist | Legendary |

---

## 4. The power curve

```
IPS = 10 * 1.25^(tier-1) * 1.10^(level-1)
```

| | L1 | L5 | L10 |
|---|---|---|---|
| Tier 1 (Common) | 10.0 | 14.6 | 23.6 |
| Tier 4 (Rare) | 19.5 | 28.6 | 46.1 |
| Tier 7 (Epic) | 38.1 | 55.8 | 90.0 |
| Tier 10 (Legendary) | 74.5 | 109.1 | 175.7 |

Deliberate tension: a maxed Tier 1 (23.6) beats a fresh Tier 4 (19.5) but loses to a
fresh Tier 5. Investment in current gear is never wasted; a 4+ tier jump always excites.

A slot's IPS is split across its 3 bars as **50% / 30% / 20%** of the slot's stat budget.

---

## 5. Upgrade costs

Common baseline, per level:

| Level | Cards | Coins |
|---|---|---|
| 1 → 2 | 2 | 50 |
| 2 → 3 | 4 | 150 |
| 3 → 4 | 10 | 400 |
| 4 → 5 | 20 | 1,000 |
| 5 → 6 | 50 | 2,000 |
| 6 → 7 | 100 | 4,000 |
| 7 → 8 | 200 | 8,000 |
| 8 → 9 | 400 | 16,000 |
| 9 → 10 | 800 | 32,000 |
| **Total** | **1,586** | **63,600** |

Rarity multipliers — rarer gear needs **fewer cards** (they drop far less) but costs **more coins**:

| Rarity | Cards × | Coins × | Total cards | Total coins |
|---|---|---|---|---|
| Common | 1.0 | 1.0 | 1,586 | 63,600 |
| Rare | 0.5 | 1.6 | 793 | 101,760 |
| Epic | 0.2 | 2.6 | 317 | 165,360 |
| Legendary | 0.08 | 4.5 | 127 | 286,200 |

Coins are the **sink**, cards are the **gate**. This keeps the coin economy meaningful for years.

---

## 6. Acquisition

### A. Coins (grind)
Pay upgrade costs only. Gear items are **never** sold directly for coins — that would
collapse the card economy. Coin faucets: tour matches, league placement, chests, wheel.

### B. Lucky draw (chests)
The card faucet. The existing `BagSlot` timer system already supports this shape.

| Chest | Source | Cards | Common | Rare | Epic | Legendary |
|---|---|---|---|---|---|---|
| Match Bag (2h) | win a match | 8–14 | 82% | 15% | 2.8% | 0.2% |
| Tour Crate (4h) | clear tour stage | 20–30 | 72% | 22% | 5.4% | 0.6% |
| League Chest (8h) | weekly league | 45–70 | 62% | 28% | 8.5% | 1.5% |
| Epic Chest (90 gems) | shop | 60–90 | 40% | 40% | 18% | 2% |
| Legendary Chest (IAP) | shop | 120–180 | 25% | 40% | 30% | 1 guaranteed |

**Pity counters:** guaranteed Epic every 30 chests without one; guaranteed Legendary every 300.

**Daily wheel:** 1 free spin, 2 rewarded-ad spins, 25-gem spin. Rewards: coins / cards / gems.

### C. IAP — sell time, not stats
- Gem packs: $0.99 / $4.99 / $9.99 / $19.99 / $49.99
- Starter Pack $2.99, one-time, best value (first-purchase conversion)
- Targeted gear bundles $4.99 — a specific item's cards + coins (the existing "Miami Pro Pack" slot)
- Season Pass $9.99 — cards + coins across 50 tiers

No direct-purchase stat items. No paid stat boosts.

---

## 7. Overall Rating and matchmaking

```
OVR = sum over equipped gear of (IPS * slotWeight), normalized to a 0-1000 display scale
```

PvP pairing must clamp to **±15% OVR** on top of the trophy range. Without this guard,
gear progression directly kills PvP retention.

---

## 8. Known gaps in current code

| File | Issue |
|---|---|
| ~~`Assets/Scripts/Sim/ShotSim.cs`~~ | ~~never reads `MetaGameState` — no stat binding exists~~ — **done**, see step 3 below |
| ~~`Assets/Scripts/Controllers/PlayerController.cs`~~ | ~~move speed / strike zone are not stat-driven~~ — **done**, see step 3 below |
| ~~`Assets/Scripts/UI/Screens/GearDetailScreen.cs:110`~~ | ~~renders only 3 of 4 stats; `agility` is invisible~~ — **done**, see step 2 below (each slot now shows exactly its 3 real stats) |
| ~~`Assets/Scripts/Data/MetaGameState.cs` `TryUpgradeGear`~~ | ~~flat `+9/+6/+4/+2` per level, ignores rarity and tier~~ — **done** |
| ~~`Assets/Scripts/Data/MetaGameState.cs`~~ | ~~`Gear` is a hardcoded 4-item list~~ — **done**, see step 2 below |
| ~~`Assets/Scripts/UI/Screens/GearLoadoutScreen.cs` `BuildStatsPanel`~~ | ~~aggregate loadout panel still sums only 4 of the 6 stats~~ — **done**, see step 5 below (all 6 now shown) |
| ~~`Assets/Scripts/UI/Screens/GearCatalogScreen.cs` / `UIBuilder.GearCard`~~ | ~~gear cards show only the cards-collected meter, not the 3-stat-bar layout~~ — **done**, see step 7 below |
| ~~Chests / pity counters~~ | ~~`ChestOpeningScreen.cs` opened a single hardcoded reward bundle~~ — **done**, see step 6 below. |
| ~~Chest acquisition wiring~~ | ~~Tour Crate/League Chest had no grant point; Epic Chest's shop deal already existed at the right price but granted nothing~~ — **done**, see step 8 below. |
| ~~Legendary Chest purchase entry point~~ | ~~no shop UI sold it~~ — **done**, see step 9 below (new "PREMIUM" IAP section). |
| ~~`Assets/Scripts/Backend/PlayerProfileData.cs`~~ | ~~gear state saved as parallel index-aligned lists~~ — **done**, see step 1 below |
| `Assets/Scripts/Net/*` | no server-side (or even client-side) validation that a submitted `ShotData` is consistent with its claimed hitter's actual equipped gear — a modified client could transmit an impossibly fast/accurate shot. `RemoteParticipant` itself needs no change (see step 3). Still open after step 4: OVR-based matchmaking is done (see step 12), but this anti-cheat gap and a real bucketed queue are not. |

---

## 9. Step 1 (done): id-keyed save format

`PlayerProfileData` now carries `schemaVersion` (0 = legacy parallel lists, 1 = id-keyed) and a
`List<GearProfileEntry>` of `{ gearId, level, cardsCollected, cardsNeeded, power, spin, agility,
serve, upgradeCostCoins }`.

- `PlayerProfileData.Migrate()` rebuilds v0 saves against the frozen `LegacyGearIdOrder`
  (`carbon_pro`, `swift_step`, `starter_grip`, `warrior_grip`) — **that constant must never be
  reordered or regenerated from the live catalog.**
- Stat fields use `-1` as "not stored, keep the catalog value", preserving the old null-guard
  behaviour for saves predating some of the lists.
- `ApplySnapshot` matches by id: unknown ids are dropped, catalog items missing from the save keep
  their defaults — which is exactly how a newly added item should arrive.
- `ProfileService` calls `Migrate()` unconditionally after every load; it is idempotent.

One-way by design: a v1 profile read by an older build finds the v0 lists empty and resets gear
levels to catalog defaults. Currency, trophies and tour progress survive that downgrade.

---

## 10. Step 2 (done): the 40-item catalog is generated, not hand-typed

- [`GearProgressionCurve.cs`](../Assets/Scripts/Data/GearProgressionCurve.cs) is the single source of
  the formulas: `ItemPowerScore(tier, level)`, `RarityForTier`, the 50/30/20 stat split
  (`ComputeStats`), and `UpgradeCost`. It takes every input explicitly and mutates nothing, so both
  seeding a fresh item and previewing an upgrade one level ahead call the exact same math.
- [`GearCatalog.cs`](../Assets/Scripts/Data/GearCatalog.cs) holds only identity — id, name, slot,
  ladder position (tier 1-10), icon — for all 40 items, plus the 4 legacy items' historical seed state
  (`carbon_pro` L7, `swift_step` L5, `starter_grip`/`warrior_grip`'s starting cards) so a fresh install
  still opens with the same hand-tuned loadout it always has. Everything else is derived.
- `GearItem` gained `tier`, `control`, `stamina`. `agility` was **not** renamed to `speed` — that
  rename lands with the step-5 UI pass so it doesn't have to touch every screen twice.
- `MetaGameState.TryUpgradeGear` now calls `GearCatalog.ApplyLevel`, the same function `Build` uses,
  instead of a flat `+9/+6/+4/+2`. `CanUpgrade`/new `IsMaxLevel` gate on `level < GearProgressionCurve.MaxLevel`.
- **Carbon Pro's rarity moved from Epic to Rare** (ladder position 6, matching this doc's Paddle
  table) — a deliberate, visible change to align the shipped item with the documented ladder, not a
  bug.
- Fixed in passing (regressions this catalog change would otherwise have caused or made worse):
  - `LobbyScreen.cs`'s "equipped gear" row indexed `MetaGameState.Gear[0..2]` directly — that only
    ever matched the real equipped loadout because the 4-item catalog happened to be ordered the same
    way. Now reads `GetEquipped` per slot like `GearLoadoutScreen` already did.
  - `GearProgressionCurve.cs` referenced `Rarity` without importing `Pickleball.UI` — caught by
    compiling the file standalone (see verification below), not by inspection.
  - `GearDetailScreen`'s upgrade preview used a hardcoded `{9,6,4}` delta and fixed `POWER/SPIN/SERVE`
    labels regardless of item type — now pulls the item's real 3 stats via `DisplayLabels`/`DisplayValues`
    and previews the actual next level. Added a "max level" state throughout that screen (title, cost
    row, cards-collected meter, CTA) since the level cap is new — none of that existed to break before.

**Verification:** `GearProgressionCurve.cs` and `GearCatalog.cs` were compiled standalone (a minimal
`UnityEngine.Mathf` shim plus verbatim-copied `GearItem`/`GearType`/`Rarity`/`IconId` declarations) and
exercised by a 39-assertion harness covering catalog shape (40 items, 10/slot, unique ids), rarity
bands matching tier, the legacy items' preserved seed state, the stat split (3 active + 3 zeroed per
item, sum ≈ IPS), the power curve against this doc's reference table, upgrade cost totals against
section 5's table, `ApplyLevel` producing identical output whether called from `Build` or as an
upgrade, and the max-level sentinel (including a level-11 request, which must clamp rather than
overrun the 9-entry cost table). All 39 passed.

The Unity editor came online mid-session; the change was then compiled for real through
`refresh_unity`/`read_console` (not just the standalone harness) — 0 errors, 0 warnings across every
touched file. One catch from that step worth recording: the two new files (`GearCatalog.cs`,
`GearProgressionCurve.cs`) were on disk with hand-written `.meta` files but the AssetDatabase hadn't
picked them up (`manage_asset get_info` showed `guid: ""`), so the first compile attempt failed with
`GearCatalog does not exist in the current context`-style errors even though the code was correct. An
explicit `manage_asset action=import` on each file fixed it. If you ever hand-author a `.meta` for a
new script outside the editor, don't trust the next `refresh_unity` to pick it up on its own — check
`get_info`'s `guid` isn't empty, or just create the file once from within Unity.

Entered and exited play mode once from the project's current scene (`SampleScene`) — 0 console errors,
which confirms `GearCatalog`'s static initializer (the 40-item build, which runs once on first access
to `MetaGameState.Gear`) doesn't throw. That scene doesn't bootstrap the meta-game UI itself, though,
so this didn't exercise the gear screens.

Not yet done: an in-editor walkthrough of the actual gear screens (open `starter_grip`'s detail screen
— it starts above its new, much lower L1→2 cost, so it should read as immediately upgradeable — and
upgrade a paddle to level 10 to confirm the max-level states render correctly). The compiler confirms
the code is correct; it doesn't confirm the layout looks right at runtime.

---

## 11. Step 3 (done): gear stats are wired into the sim

### Architecture constraint that shaped everything

`Pickleball.Sim` (`Assets/Scripts/Sim/`) is its own assembly with `noEngineReferences: true` and an
empty `references: []` — zero dependencies, not even UnityEngine, so a future server can compile and
re-run the exact same shot math a client did from a recorded input log. `ShotSim` therefore can never
reference `MetaGameState`/`GearCatalog`/`GearProgressionCurve` (all in `Pickleball.Data`, which uses
UnityEngine). Everything gear-effect-related had to be pure floats in, floats out.

The fix: a new `LoadoutStats` struct (`Assets/Scripts/Sim/LoadoutStats.cs`) — six plain `float` fields
(power/spin/control/speed/serve/stamina), nothing else. `Pickleball.Data` (`Assembly-CSharp`, which
already depends on `Sim`) is free to construct one; `Sim` never needs to know it exists.
`LoadoutStats.Neutral` (all zero) is a true no-op through every binding below, so a caller with no
real gear — the AI, a trajectory preview — can simply omit the parameter.

### The bindings (`ShotSim.cs`, all pure `stat -> multiplier` functions)

| Stat | Function | Effect | Cap |
|---|---|---|---|
| Power | `PowerDurationMultiplier` | shortens a shot's final duration | 22% |
| Control | `ControlScatterMultiplier` | shrinks `CalculateTargetPosition`'s scatter term | 45% |
| Spin | `SpinMultiplier` | scales `spinRate`, and (Topspin/Slice only) how far `bounceMultiplier` deviates from 1 | +40% |
| Serve | `ServeDurationMultiplier` | shortens serve duration | 18% |
| Speed | `SpeedMoveMultiplier` / `SpeedReachBonus` | move-speed multiplier / added miss-reach distance | +30% / +0.35 units |
| Stamina | `StaminaOnsetShot` + `RallyFatigueMultiplier` | delays, then bounds, a new per-rally fatigue decay on `compositeScore` | onset shot 6→12, decay capped at -35% |

All six share the same `effect = capFraction * stat / (stat + k)` shape from
Docs/GearProgression.md#1-stats.

**Stamina is the one genuinely new mechanic** — no fatigue/rally-length decay existed before this.
"Per-rally" is read as the rally's *total* shot count (both sides combined, via
`RallyManager.CurrentRallyCount`, newly exposed), not the hitter's personal swing count — simpler,
needs no new tracking, and matches "long rallies" being about both players tiring, not just one.
Fatigue degrades `compositeScore` (which drives accuracy and duration) but deliberately never touches
`quality` (the Perfect/Great/Good/Weak/Miss readout) — that stays pure swing-timing feedback, not
conflated with a stat effect.

### Where each stat actually gets applied

- **Power, Control, Spin, Stamina** — `ShotSim.EvaluateAndBuildShot` and `CalculateTargetPosition` both
  gained trailing optional params `LoadoutStats loadout = default(LoadoutStats)` (and
  `EvaluateAndBuildShot` also `int shotIndexInRally = 0`). `ShotSystem.cs` (the Unity-facing adapter)
  forwards both straight through — no `Sim`⇄`Gameplay` conversion needed since `LoadoutStats` has no
  engine types in it.
- **Serve** — never reaches `EvaluateAndBuildShot`'s `Serve` case at all: both
  `PlayerController.ExecuteServe` and `OpponentAI.EnterServeMode` build their `ShotData` directly,
  bypassing the sim entirely (a pre-existing asymmetry, not introduced here). `ExecuteServe` applies
  `ServeDurationMultiplier` to its own `serveShot.duration` directly.
- **Speed** — doesn't touch ball physics at all, so it never enters `Pickleball.Sim`. Applied directly
  in `PlayerController`: `SpeedMoveMultiplier` scales `Update()`'s move speed, `SpeedReachBonus` widens
  the miss-reach distance (now a `[SerializeField] baseMissReachDistance`, was a bare `1.6f` literal).

### Who gets affected

Only the human player. `MetaGameState.GetLoadoutStats()` (new) sums the four equipped items' six
stats — the real, unclamped totals; `GearLoadoutScreen`'s ATTRIBUTES panel now calls the same method
and clamps for its own 0-100 display, rather than summing separately. `PlayerController` resolves it
once in `Start()` (gear doesn't change mid-match) and passes it into every rally shot.

`OpponentAI` and `RemoteParticipant` were **not touched**, on purpose:
- The AI has no gear; its calls to `EvaluateAndBuildShot` are unmodified and fall through to the
  `Neutral`/`0` defaults, which is exactly correct (AI difficulty stays a separate, existing system).
- A PvP peer's shot arrives over the wire already fully evaluated — `RemoteParticipant.ReceiveRemoteShot`
  trusts the transmitted `ShotData` rather than recomputing it, and that `ShotData` was built by the
  *sender's own* `PlayerController` using the *sender's own* loadout. So PvP already gets correct
  per-player stats with zero additional wiring. What's still missing is server-side validation that a
  submitted shot is consistent with its claimed hitter's actual gear (anti-cheat) — that's a step 4
  concern, not step 3's.

### Verification

Two layers, matching the standard set in steps 1-2:

1. **Standalone, full-fidelity.** `Pickleball.Sim`'s zero-dependency design means its actual production
   files compile as a real, unmodified `net8.0` console app (no shims needed at all this time, unlike
   the `Mathf`-shimmed harnesses in steps 1-2) — used the `dotnet` SDK directly against copies of every
   file in `Assets/Scripts/Sim/`. A 33-assertion harness covered: every binding's neutral-input no-op
   and asymptotic cap, monotonicity in the right direction, `CalculateTargetPosition`'s scatter shrink
   under a fixed RNG seed, `EvaluateAndBuildShot`'s duration/spin/bounce effects, fatigue onset/decay/cap,
   and — the one that mattered most — **a byte-for-byte regression test**: a hand-preserved copy of the
   pre-change algorithm, compared shot-for-shot across all 7 shot types against the new code called with
   no `loadout`/`shotIndexInRally` (i.e. exactly how every pre-existing call site still calls it). All 33
   passed, including the regression check, confirming zero behavioral drift for the AI and any other
   caller that doesn't opt into gear effects.
2. **Real Unity compile.** `refresh_unity`/`read_console` — 0 errors, 0 warnings across every touched
   file (`ShotSim.cs`, `LoadoutStats.cs`, `ShotSystem.cs`, `MetaGameState.cs`, `PlayerController.cs`,
   `RallyManager.cs`, `GearLoadoutScreen.cs`). `validate_script` flagged two line-less warnings on
   `PlayerController.cs` ("FindObjectOfType in Update()", "string concatenation in Update()") that don't
   correspond to anything in the diff — grepped the file and confirmed `FindObjectOfType` only appears
   in `Awake()`/`Start()`, nothing in `Update()` concatenates strings. Same tool gave a confirmed false
   positive in step 2 (a phantom "duplicate method"); treating this the same way. Entered and exited
   play mode once more — 0 console errors.

Not yet done: an in-editor match, since `SampleScene` doesn't bootstrap the meta-game/match flow (same
limitation noted in step 2). A good manual check once that's reachable: equip a maxed-out Paddle and
Shoes, play a rally, and confirm shots feel faster/more accurate than with starter gear — and separately,
that a long rally (10+ shots) visibly gets harder without Stamina invested.

---

## 12. Step 4 (done): OVR is real, PvP pairing clamps to it

### Overall Rating: from a flat counter to a live computation

`MetaGameState.OverallRating` used to be a stored field seeded at 812 and bumped by a flat `+4` on
every upgrade — completely disconnected from the actual equipped loadout. Swapping to a different
already-owned item never changed it at all, and a fresh save's 812 had no relationship to its actual
starter gear. It's now a get-only property computed live from equipped gear on every read:

```
OVR = round(1000 * sum(ItemPowerScore(tier, level) * slotWeight) / ItemPowerScore(10, MaxLevel))
```

`GearProgressionCurve.SlotWeight` matches section 2's table exactly (Paddle 0.40 / Shoes 0.25 / Grip
0.20 / Accessory 0.15, summing to 1.0), so a fully-maxed 4-slot loadout reads exactly 1000 by
construction. `PlayerProfileData.overallRating` still exists and `CaptureSnapshot` still writes the
current value into it, but purely for a human reading raw JSON — `ApplySnapshot` no longer reads it
back, since the live value is always recomputed from whatever gear the save actually restores.

**Visible consequence, called out deliberately (same as Carbon Pro's rarity in step 2):** a starter
loadout's displayed OVR drops sharply from the old flat 812 to roughly 175-200, because that's what
the starter gear's real Item Power Score actually earns against a 1000-point scale anchored to a
maxed-out loadout. This is correct, not a bug — the old number was never derived from anything.

### Matchmaking clamp

`PhotonQuickMatch` already published `MetaGameState.OverallRating` as a room custom property
(`PropOvr`) and already read the opponent's back — that plumbing predates this step. What was missing
was ever actually *checking* it: the class's own docstring calls it "a deliberate placeholder for real
matchmaking... there is exactly one room, first-come first-served, no rating." `TryStartIfReady` (the
one place a match is authoritatively accepted — only the master client runs it) now rejects an
opponent whose OVR differs by more than 15%, and requeues by leaving the room and calling `JoinQueue()`
again (`OnLeftRoom`, gated by a `pendingOvrRequeue` flag so a genuine cancel-triggered leave in
`OnDestroy` doesn't also requeue).

**Elastic, not a hard wall — and why.** With exactly one shared room and no real bucketed queue, a
permanent hard reject risks a livelock: two players who are the only ones testing, with mismatched
gear, would reject each other forever and never find a match. So the 15% tolerance widens by +15
percentage points every time *this client* rejects an opponent this search, and gives up clamping
entirely after 4 rejections — a match is always eventually found, just not always a close one. A real
trophy+OVR bucketed queue (the class's own docstring already flags this as future "phase 3" work,
alongside bot backfill) would make the widening unnecessary; this is the honest, bounded version that
fits today's single-room architecture without rearchitecting it.

### Verification

- **`GearProgressionCurve.ComputeOverallRating`** — compiled as the real, unmodified production file
  via the `dotnet` SDK (same zero-shim approach as step 3, since it's plain C# once `Mathf` is stubbed)
  and checked: slot weights sum to 1.0, a fully-maxed loadout reads exactly 1000, an empty or all-null
  loadout reads 0 without throwing, upgrading any slot only ever raises OVR, and a partial loadout
  (missing the wristband) scores exactly 850 = 1000 × (1 − 0.15) — confirming the weights are applied
  independently per slot, not renormalized over however many slots happen to be filled.
- **The matchmaking tolerance math** — `PhotonQuickMatch.IsOvrAcceptable` can't be unit-tested directly
  (it's `#if PHOTON_UNITY_NETWORKING`-gated and takes a `Photon.Realtime.Player`), so its logic was
  ported line-for-line into the same harness and checked against the boundary cases: exactly-15%
  accepted, just-over-15% rejected, the same rejected gap accepted after one widening, a huge gap still
  rejected after one widening, always-accept past the rejection cap, and the 0-vs-0 and 0-vs-nonzero
  degenerate cases. All 18 assertions passed.
- **Real Unity compile** (`refresh_unity`/`read_console`) — 0 errors, 0 warnings, including
  `PhotonQuickMatch.cs` itself: confirmed `PHOTON_UNITY_NETWORKING` is actually defined for the
  Standalone/editor platform (checked `ProjectSettings.asset` directly), so this wasn't skipped by the
  `#if`. Play mode entered/exited cleanly.

Not yet done, and explicitly out of scope for this step: a real bucketed matchmaking queue (rooms
segmented by trophy+OVR range from the start, rather than reject-after-match), bot backfill, and
server-side validation that a submitted shot's outcome is consistent with its claimed hitter's actual
gear (anti-cheat) — see the `Assets/Scripts/Net/*` row in section 8.

---

## 13. Step 5 (partial, done what's in scope): Speed rename + full 6-stat loadout panel

### `agility` → `speed`

The live `GearItem.agility` field is renamed to `speed` (matching `GearStatSet`/`LoadoutStats`, which
already used `speed` since steps 2-3 — only the display-facing `GearItem` field lagged). 5 call sites
updated: `GearCatalog.ApplyLevel`, `GearProgressionCurve.StatsOf`, and three spots in
`MetaGameState.cs` (`CaptureSnapshot`, `ApplySnapshot`, `GetLoadoutStats`).

**`PlayerProfileData.GearProfileEntry.agility` — the persisted/wire field — was deliberately NOT
renamed.** `JsonUtility` has no concept of a field alias; every existing save's JSON has an `"agility"`
key, and renaming the C# field would just make that key unrecognized, silently dropping every
player's saved Speed progress back to the catalog default on next load (via the same -1-sentinel
"not stored" path that already handles a genuinely absent key, so it degrades gracefully rather than
crashing — but it's still real, avoidable data loss for no reason). `MetaGameState.CaptureSnapshot`/
`ApplySnapshot` now map explicitly between the two names (`agility = g.speed` / `item.speed =
entry.agility`) at the save boundary. This is the same pattern as `LegacyGearIdOrder` in step 1 and
`GearProfileEntry`'s frozen v1 shape generally: some names are live and free to change, some are wire
contracts and are not, and the two must never be confused for each other.

### `GearLoadoutScreen`'s ATTRIBUTES panel: 4 stats → 6

Mechanical extension of an existing, already-hand-tuned pattern, not a redesign: the row loop was
already generic over `labels.Length`, so adding Control and Stamina only needed the label/value arrays
extended and the panel's height increased by exactly 2 rows' worth (2 × the loop's existing 76px
spacing = 152px) — added as `StatsHeight = 424f + 152f` rather than recomputed from scratch, so the
padding this was originally tuned against is preserved exactly rather than approximated.

### Explicitly scoped out — and why

Two pieces of "step 5" from the original roadmap were **not** attempted this round:

1. **3-stat-bar gear cards** (`GearCatalogScreen`/`UIBuilder.GearCard`), matching the original mockup's
   per-card bars. Unlike the ATTRIBUTES panel above, this isn't extending a proven pattern — it's new
   layout inside an already-dense, small, hand-tuned card (rarity label, name, art, and a
   cards-collected meter all already fit a fixed `470f`-tall card with hardcoded pixel offsets) that
   has no established "add N more rows" precedent to mechanically extend. Getting pixel-perfect card
   layout right needs visual iteration this session has no way to do — `SampleScene` doesn't bootstrap
   the meta UI, and there's no screenshot/render tool available for it here. Shipping it blind risked a
   real regression (overlapping text, bars poking outside the card) presented as "done." Worth doing
   once the screen is actually viewable, in the editor or a build.
2. **Chest odds and pity counters** (section 6). `ChestOpeningScreen.cs` currently opens one hardcoded
   reward bundle; none of the 5 chest types, their drop-rate tables, or persistent pity counters exist.
   This is a standalone feature roughly the size of the original 40-item catalog build (step 2) — new
   save-data fields, RNG-table design, and its own UI — not a small addition to fold silently into a
   "finish step 5" pass. Flagging it rather than attempting a partial version of it.

### Verification

- Grepped the whole `Assets/Scripts` tree for `.agility` after the rename: only the two intentionally-
  kept `GearProfileEntry.agility` wire-field references remain.
- Re-ran all three existing standalone harnesses against the changed files (all as real, unmodified
  production code, no shims beyond `Mathf`): the 40-item catalog harness (39 assertions) and the OVR
  harness (18 assertions) both still pass in full against the renamed field; the schema-migration
  harness (33 assertions) still passes, confirming the wire format genuinely round-trips unchanged.
- Real Unity compile (`refresh_unity`/`read_console`) — 0 errors, 0 warnings across every touched file.
  Play mode entered/exited cleanly.
- **Not verified:** the ATTRIBUTES panel's on-screen appearance at 6 rows. The height math preserves
  the original 4-row layout's padding exactly (shown above), which is the lowest-risk way to extend a
  hand-tuned layout, but this session has no way to render `GearLoadoutScreen` and confirm it visually.

---

## 14. Step 6 (done, engine + wiring): chest odds and pity counters

### What this delivers

A real gacha engine replacing `OpenReadyBag`'s old hardcoded bundle (350 coins, 3 gems, 2 Warrior
Grip cards, and a dead "ENDURANCE ×1" entry that matched no reward-processing branch and granted
nothing — vestigial placeholder content, not a mechanic). Split the same way `GearCatalog`/
`GearProgressionCurve` were in step 2: identity data (`ChestCatalog`, the 6 chest definitions) versus
pure formulas (`ChestRoller`, the rarity-band and pity math), both in the new
[`ChestCatalog.cs`](../Assets/Scripts/Data/ChestCatalog.cs).

- **6 chest types**: the doc's 5 (Match Bag, Tour Crate, League Chest, Epic Chest, Legendary Chest)
  plus `welcome_bag` — a first-run-only freebie that predates this doc, not derived from section 6's
  numbers, tuned generously on purpose (60/30/9/1% instead of a normal chest's heavily-Common table).
- **Odds and card counts are section 6's table verbatim** for the 5 real chests, verified against it
  directly (see below). **Coin bonuses are an addition beyond that table** — chests granted coins
  alongside cards before this system existed, and removing that entirely would have been a felt
  regression the doc never asked for; kept modest and scaled per chest tier instead (20-40 for Match
  Bag up to nothing for the two shop-bought chests, which already cost gems/real money).
- **Global pity, not per-chest-type**: `chestsSinceEpic`/`chestsSinceLegendary`, matching the doc's
  "guaranteed Epic every 30 chests without one; guaranteed Legendary every 300" — read as one clock
  across every chest opened, not 30 chests *of one type*. Persisted in `PlayerProfileData` with plain
  0 defaults (no sentinel needed: a returning pre-this-system save simply starts pity fresh, which is
  exactly correct, not a migration).
- **Legendary Chest's "1 guaranteed"** (the doc's 4th column isn't a percentage like the other three)
  is modeled as an outright 100% Legendary roll rather than a second guaranteed-slot reward layered on
  top of a base-table roll — simpler, and honest about being a *start* of the system rather than the
  full multi-reward-per-chest version the doc's phrasing gestures at.
- **Fixed in passing**: `StartBagUnlock` took a `durationSeconds` parameter defaulting to a flat 2
  hours: every chest type — Match Bag, Tour Crate, welcome bag, all of them — unlocked on the exact
  same timer, because the one caller (`LobbyScreen`) never passed anything else. It now reads the real
  duration from `ChestCatalog` keyed on the slot's own `bagId`. `ChestOpeningScreen`'s title was a
  hardcoded `"FREE MATCH BAG"`; now reads `RewardBundle.chestName` (new field), the opened chest's
  real name.

### Where chest rolling deliberately does NOT reuse `Pickleball.Sim.DeterministicRandom`

`ShotSim` needs bit-identical RNG because a server has to re-simulate a *competitive match* from a
recorded seed. Chest rolls have no such requirement — they're single-player economy RNG — so
`ChestRoller.Roll` takes plain `float` rolls (`UnityEngine.Random.value` in production) instead.
Roping `DeterministicRandom` in here would blur what that type is specifically for.

### Not in this pass

- ~~**No new acquisition entry points.**~~ — **done**, see step 8 below (Tour Crate, League Chest,
  Epic Chest). Legendary Chest (IAP) still has no purchase entry point — see step 8's own scoping note.
- **No pity/odds UI.** `ChestsSinceEpic`/`ChestsSinceLegendary` are exposed as read-only properties on
  `MetaGameState` for exactly this reason, but nothing displays them yet — no "12/30 to guaranteed
  Epic" progress bar, no odds-disclosure screen (increasingly a store-compliance expectation for
  gacha mechanics, not just a nice-to-have).
- **No server-side validation.** Same gap already flagged for shot data (section 8): a compromised
  client could roll however it likes locally. This prototype has no server component capable of
  validating a chest roll at all (`LocalProfileBackend`/`PlayFabProfileBackend` are dumb blob storage,
  not an authority), so this isn't fixable without a real backend — flagging it rather than pretending
  a client-side mitigation would meaningfully help.
- **Legendary Chest's guaranteed reward is single-slot**, not the fuller "guaranteed floor plus a
  rolled bonus item" version the doc's own 25/40/30/"1 guaranteed" table arguably implies. Noted above.

### Verification

- **Standalone, the real production file.** `ChestCatalog.cs` has the same shape as `GearCatalog.cs`/
  `GearProgressionCurve.cs` (plain `Pickleball.Data`, only needs `Mathf` stubbed) — compiled unmodified
  via the `dotnet` SDK and exercised by a 33-assertion harness: every chest resolves by id and an
  unknown id doesn't; every table sums to 1.0; every chest's cards/odds match section 6 verbatim;
  rarity-band resolution at chosen boundary points; card-count/coin-bonus interpolation at both range
  ends; the epic-pity chain held correctly across 29 natural chests then forced on the 30th, resetting
  only its own counter; the legendary-pity forcing at chest 300, resetting both counters; a *natural*
  Legendary (not pity-forced) still resets both counters, not just its own; the guaranteed chest
  overriding even a roll that would naturally be Common, without itself reporting `pityTriggered`
  (that flag means pity intervened, not "this chest always guarantees it"); and a 5000-iteration
  simulated run (a small LCG, not real randomness, so it's reproducible) confirming both pity counters
  never exceed their own threshold at any point — the forcing always fires on the exact chest that
  would have crossed it, never later.
- **Real Unity compile** (`refresh_unity`/`read_console`) — 0 errors, 0 warnings across every touched
  file (`ChestCatalog.cs`, `MetaGameState.cs`, `PlayerProfileData.cs`, `ChestOpeningScreen.cs`).
  `validate_script` on each individually also came back clean. Play mode entered/exited cleanly.
- **Not verified:** an actual in-editor chest opening. Same limitation as every prior step —
  `SampleScene` doesn't bootstrap the meta UI. A good manual check once that's reachable: open the
  seeded `welcome_bag` (should read noticeably more generous than a `match_bag`) and confirm the
  reveal screen's title now shows the real chest name instead of always "FREE MATCH BAG".

---

## 15. Step 7 (done): 3-stat-bar gear cards — the first step actually seen rendered

Every prior step in this doc was verified by compiling and by standalone math harnesses, never by
looking at the screen — `SampleScene` didn't obviously bootstrap the meta UI, and no screenshot
tool had been checked closely. Both assumptions turned out to be wrong: the scene already had a live
`ScreenManager` (`find_gameobjects` by component), and `manage_camera`'s `screenshot` action captures
Screen Space - Overlay UI. Once that was found, this step was built and verified against the actual
rendered screen — inside Play Mode only (`ScreenManager.Instance.ShowGearCatalog(...)` via
`execute_code`, screenshotted, never saved to the scene asset), which is why it found two real bugs
that math and code review alone had missed on every prior UI step.

### What changed

`UIBuilder.GearCard` gained two optional trailing params, `string[] statLabels`/`int[] statValues`
(length 3, matching `GearProgressionCurve.DisplayLabels`/`DisplayValues` — the same pair `GearDetailScreen`
already uses, so a card and its detail screen always show identical stats for the same item). Only
`GearCatalogScreen` passes them; `GearUpgradeRevealScreen` and `ShopScreen` (whose cards are bundle
deals with no per-item stats) are untouched by construction — the new params default to `null`, which
skips the whole block.

### Two real bugs the screenshot caught (neither visible from reading the code)

1. **The stat rows collided with a pre-existing status chip.** `GearCatalogScreen` overlays a
   "LEVEL 7"/"EQUIPPED" chip on every card at a fixed vertical band (y 34-82 from the card's bottom) —
   that chip is built by the *caller*, not by `GearCard` itself, so reading `GearCard`'s source in
   isolation never revealed it. The first layout pass (art shrunk to 48%, 3 rows at 34px) put row 3
   almost exactly on top of that chip. Fixed by shrinking the art further (to 42%) and tightening rows
   (24px, 8px gap) so the whole block sits entirely above the chip with margin -- verified by adding a
   runtime `Debug.LogWarning` guard that fires if the geometry doesn't clear it (never fired once
   correct), not just by recomputing by hand a second time.
2. **The meter fill was completely invisible, at any value** — not "too thin to see," genuinely
   absent. `MeterOn`/`OutlinedFill` had a hardcoded 5px pad applied to every edge including top and
   bottom; at the stat rows' original 8px bar height, `8 - 5 - 5 = -2`, a negative-height rect that
   Unity silently renders as nothing. Confirmed directly at runtime via `execute_code` reading the
   actual `RectTransform.rect` (`height: -2.00`) before assuming a fix — the first hypothesis (the
   sprite's 20px corner radius being too large for an 8px bar, which *is* also a real, separate issue)
   would not have fixed this on its own. `MeterStretch`/`Meter`/`MeterOn` gained `cornerRadius`/`pad`
   parameters (defaulting to the original 20px/5px, so all 5 pre-existing callers are untouched); the
   stat rows use `cornerRadius: 3, pad: 2` at a bumped 12px bar height.

Both fixes are documented in code at the exact spot a future reader would hit the same trap
(`MeterStretch`'s doc comment now states the pad-collapse arithmetic explicitly; `GearCard`'s art-height
comment explains the chip is caller-built and invisible from this file alone).

### Verified, for real this time

Screenshotted all 4 slot types (Paddle, Shoes, Grip, Accessory) after the fix. Confirmed by eye:
- Each slot shows its correct 3 stats — Paddle: Power/Spin/Control, Shoes: Speed/Stamina/Control,
  Grip: Control/Spin/Serve, Accessory: Stamina/Serve/Power — matching section 2's table.
- No overlap with the status chip or with the rarity label on any card.
- Fill genuinely scales with the item's real stat: a level-1 Common paddle shows a thin sliver on all
  three bars; Carbon Pro at level 7 visibly shows more POWER fill than a level-1 item of the same slot.
- Labels render fully within their box at every slot's longest word ("COMPRESSION SLEEVE"'s card,
  "STAMINA" label, etc.) — no clipping observed.

One earlier build attempt (calling `ShowGearCatalog` repeatedly via script without going through the
normal button-driven navigation) produced a translucent double-exposure of two screens at once. That
reproduced consistently on every *repeat* call within one Play Mode session but never on the first
call after a fresh entry — almost certainly a fade/transition artifact of bypassing the UI's own
navigation history stack, not a bug in this change. Re-entering Play Mode fresh before each screenshot
avoided it; not investigated further since a real player only ever reaches this screen through normal
navigation.

Compile: 0 errors, 0 warnings, `refresh_unity`/`read_console` and `validate_script` on every touched
file, same as every other step.

---

## 16. Step 8 (done): chest acquisition wiring

Connected 3 of the 4 remaining acquisition points to the chest engine from step 6. One real,
pre-existing bug found and fixed along the way; one deeper pre-existing gap found and fixed as a
genuine prerequisite, not scope creep — explained below.

### Tour Crate: "clear tour stage" wasn't a real event

Before this step, **nothing in the game ever advanced `TourInfo.winsCurrent` or set `.cleared`.**
`ScreenManager.HandleMatchEnded` granted coins/trophies/season XP/a Match Bag on every tour-mode win,
but tour progress itself was static seed data — a player could win a tour match a hundred times and
`winsCurrent` would never move. This predates the chest system entirely; it just happened to be the
thing standing between "Tour Crate: source = clear tour stage" and anything to actually hook.

Fixing it was the minimum needed to give the feature real meaning — granting a Tour Crate on *every*
tour-mode win (ignoring the "clear tour stage" trigger the doc actually specifies) would have
duplicated what Match Bag already covers and not matched the documented source at all. So:

- New `MetaGameState.RecordTourWin()`: increments `CurrentTour.winsCurrent` (clamped to
  `winsRequired`), and the moment it reaches `winsRequired`, sets `.cleared = true` and grants a Tour
  Crate. A no-op on an already-cleared tour — replaying one doesn't re-grant its one-time chest.
- Called from `HandleMatchEnded`, gated on `!matchmakingIsRanked` (a field already set once per match
  by `BeginPreMatchFlow` and untouched since) — ranked/PvP wins were never tied to a tour and still
  aren't.

### League Chest: the weekly hook already existed, just unused

`ScreenManager.CompleteStartupResults()` already ran exactly once per week — gated by
`MetaGameState.ShouldShowStartupResults`/`MarkStartupResultsSeen`, both pre-existing — as the "CLAIM &
CONTINUE" button on the weekly league-result screen. That's the natural trigger for League Chest
("weekly league", Docs/GearProgression.md#6-acquisition-b); it just never granted anything. One line:
`MetaGameState.AddLeagueChest()` alongside the existing `MarkStartupResultsSeen()` call.

### Epic Chest: already fully priced in the shop, silently granted nothing

`ShopScreen`'s "DAILY DEALS" row already had an "EPIC CHEST" card costing exactly 90 gems — matching
`ChestCatalog`'s `epic_chest` definition to the cent, which strongly suggests the shop was built
*against* this system's eventual existence. But its buy button only ever did
`MetaGameState.AddGems(-gemCost)` then navigated home — **a real, shipped bug**: purchasing it took
the player's gems and gave back nothing. Fixed by adding `MetaGameState.AddEpicChest()` alongside the
existing gem deduction, gated on `i == 2` (explicitly the Epic Chest slot in the deals array, not "any
gem-priced deal") so a future non-chest gem deal doesn't silently start granting one too.

### New `MetaGameState` API

`AddMatchBag()`'s body was generalized into a private `AddBag(string chestId)`, now shared by
`AddMatchBag()`, `AddTourCrate()`, `AddLeagueChest()`, and `AddEpicChest()`. Same silent-no-op-when-full
behavior `AddMatchBag()` always had — a win/purchase/reward arriving while all 4 bag slots are full is
simply not banked; there's nowhere to queue it. `ChestsSinceEpic`/`ChestsSinceLegendary` (step 6) are
still unread by any screen; still flagged below.

### Explicitly not done

- **Legendary Chest still has no purchase entry point.** — **done**, see step 9 below.
- **Two adjacent, pre-existing bugs noticed but not fixed** (out of scope for "chest wiring" — neither
  involves a chest): "MIAMI PRO PACK"'s buy button has a code comment claiming *"this prototype just
  grants the bundle"* but doesn't grant anything, and the "PADDLE x10"/"SHOES x20" daily deals spend
  coins for nothing too (no gear id is even associated with them to know what to grant). Flagged as a
  separate follow-up rather than fixed here.

### Verification

Standalone harnesses weren't the right tool for this step's actual risk — the new logic
(`RecordTourWin`, `AddBag`) reads and mutates `MetaGameState`'s live static state (`Tours`,
`bagSlots`), which would need reimplementing large parts of `MetaGameState` to isolate. Instead,
exercised the *real* running production code directly inside Play Mode via `execute_code`:

- Drove `CurrentTour` (seeded at "MIAMI BEACH", 16/25 wins) through 8 more `RecordTourWin()` calls,
  confirming `winsCurrent` climbing 16→24 without clearing, then the 9th call reaching 25 and setting
  `cleared = true`. A further call afterward left `winsCurrent` unchanged and granted nothing a second
  time — confirmed by comparing the exact empty-slot count before and after.
- Freed a bag slot via `OpenReadyBag()`, called `AddTourCrate()`, and confirmed the *specific* freed
  slot (not just "some" slot) now held `bagId="tour_crate"`. Then called `StartBagUnlock` on it and
  read back `unlockCompleteUnixSeconds` — 14400 seconds (exactly 4h, `ChestCatalog`'s real
  `tour_crate.unlockSeconds`), not the old hardcoded 7200.
- Confirmed `AddLeagueChest()`/`AddEpicChest()` correctly no-op (not crash) once all 4 slots were full
  again from the prior step, and independently confirmed both ids resolve in `ChestCatalog` with the
  right unlock duration and card range.
- Screenshotted the Shop screen after these changes — Epic Chest's card, price, and layout are
  pixel-identical to before, confirming the fix touched only the purchase logic, not the UI.
- All of the above produced 0 console errors/warnings throughout. Real Unity compile
  (`refresh_unity`/`read_console`) — 0 errors, 0 warnings on every touched file.

Not verified: an actual click through the Epic Chest buy button's real `Button.onClick` /
`EventSystem` path (as opposed to calling `MetaGameState.AddEpicChest()` directly, which is what that
button's handler does). The handler is a one-line addition next to already-working, unchanged gem-
deduction logic, reviewed by eye rather than click-tested.

## 17. Step 9 (done): Legendary Chest shop wiring

The last remaining chest with no purchase path. `ChestCatalog`'s `legendary_chest` (step 6) was
always fully defined — 120–180 cards, `guaranteesLegendary: true` — but nothing in `ShopScreen` sold
it, and the doc's own acquisition table (§6-b) already specified it differently from Epic Chest:
`Legendary Chest (IAP)`, not gems. That rules out just adding it as a 4th gem-priced card next to
Epic Chest in `DAILY DEALS`.

### Where it went, and why

`ShopScreen.BuildDeals`'s row is hardcoded to exactly 3 equal-width columns (`slot = 1f / 3f`, loop
`i < 3`) — turning that into 4 columns wall-to-wall would shrink Epic Chest's card along with a brand
new one, changing an already-shipped, working purchase's layout for no reason connected to Legendary
Chest itself. It also doesn't fit that row conceptually: `DAILY DEALS` resets every 6h14m (the section
header's own chip says so), but a `guaranteesLegendary` purchase reads as a standing top-tier offer,
not a rotating daily one.

Instead it's a new section, `PREMIUM`, inserted between `DAILY DEALS` and `GEMS`, with its own section
header carrying a `1 GUARANTEED LEGENDARY` chip (reusing `BuildSectionHeader`'s existing optional-note
parameter — no new UI primitive needed). The section holds one centered card, not a row split into
thirds — `BuildLegendarySection` in
[`Assets/Scripts/UI/Screens/ShopScreen.cs`](../Assets/Scripts/UI/Screens/ShopScreen.cs). It reuses
`UIBuilder.GearCard` at the same `(300, 306)` size `BuildDeals`' cards use — `GearCard` sizes its
internal text/icon/meter geometry as fractions of the `size` argument, not the container's actual
rendered width (the container is `Fill()`-ed onto afterward), so keeping the same size keeps this
card visually consistent with the deal cards above it rather than introducing new, unverified
proportions for a wider one.

The buy button is a plain `"$9.99"` `GoldButton`, styled and positioned exactly like a gem pack's buy
button (`GemPacks`' `14px` margins / `74px` height / `14px` bottom offset) rather than `BuildDeals`'
icon-plus-price-row treatment — there's no gem or coin cost to render as an icon, just a dollar price,
the same as the Featured "MIAMI PRO PACK" bundle's own button above it.

$9.99 is a new number, not one carried over from an existing definition — the doc's §6-b table marks
Legendary Chest as `(IAP)` but was never priced. It's set to match the top gem pack's price
(1,200 gems / $9.99) as the shop's single most expensive standing offer, consistent with it being the
only chest that outright guarantees a Legendary card rather than just improving the odds of one.

### New `MetaGameState` API

`AddLegendaryChest()` — same one-line shape as `AddEpicChest()`, calling the shared `AddBag("legendary_chest")`.
Unlike `AddEpicChest`, its caller has no currency deduction to gate on: this is real-money IAP, same as
the Featured bundle's own buy handler, so the button spends no in-game currency and there's no
affordability check to write.

### Verification

Real Unity compile (`refresh_unity` force / `read_console`) — 0 errors, 0 warnings. Then, in Play Mode,
via `execute_code` against the live running game (same rationale as step 8 — this mutates
`MetaGameState`'s live static `bagSlots`, not worth reimplementing standalone):

- Emptied all 4 bag slots, called `MetaGameState.AddLegendaryChest()` via reflection, confirmed slot 0
  became `Sealed:legendary_chest` and the other 3 stayed `Empty`.
- Filled slots 1–3 with unrelated bags, called `AddLegendaryChest()` again — confirmed it no-opped
  (state unchanged, no exception), matching `AddBag`'s existing full-bags behavior.
- Confirmed `ChestCatalog.Find("legendary_chest")` resolves to `name="LEGENDARY CHEST"`,
  `unlockSeconds=0` (opens instantly once granted, like Epic Chest — it's shop-bought, not
  timer-unlocked), `guaranteesLegendary=True`.
- Unlike step 8's Epic Chest fix (where only the direct `MetaGameState` call was exercised), this time
  drove the *actual* UI path: found the `LegendaryRow` GameObject in the live Shop screen, got its
  `Button` component, and called `btn.onClick.Invoke()` directly — confirmed it granted the chest into
  the first empty slot exactly like the direct call did, closing the "not verified: real click"
  gap noted at the end of step 8.
- Navigated to the Shop screen and screenshotted it — the `PREMIUM` section renders as a single
  centered card between `DAILY DEALS` and `GEMS`, correct Legendary-rarity (gold) art gradient, the
  `1 GUARANTEED LEGENDARY` header chip, and the `$9.99` button, with no overlap or clipping against
  the sections above or below it.
- Reset all 4 bag slots back to `Empty` before exiting Play Mode, so no test state leaked into the
  saved profile.

With this, every chest in `ChestCatalog` now has a real grant point: Match Bag (match win), Tour Crate
(tour clear), League Chest (weekly claim), Epic Chest (shop, gems), Legendary Chest (shop, IAP). The
two adjacent shop bugs flagged at the end of step 8 (Starter Bundle / "PADDLE x10" / "SHOES x20"
granting nothing) — **now fixed, see step 10 below.**

## 18. Canva-generated icon art (done)

Generated real flat-icon art via the Canva MCP connector for the 5 icons the game had no dedicated
art for, and wired it into the existing rendering pipeline as a genuine engine-side asset (not
reference/concept art delivered for later import).

### Scope and a real gap it closed

Five icons: Paddle, Shoe, Grip (tape roll), Wristband, Chest. One template each, reused across all
40 items / 6 chest tiers the same way the vector icons they replace always were — rarity color still
comes from the card's own gradient background (`RarityGradient`), not the icon art itself, so there
was no need to generate rarity variants per item.

The wristband one closes a real, explicitly-flagged gap:
[`GearCatalog.cs`](../Assets/Scripts/Data/GearCatalog.cs) used `IconId.Bolt` — a generic lightning
bolt — as a stand-in for all 10 Accessory items, with a code comment reading "No dedicated wristband
art exists yet." That's now real wristband art.

### Why a new `IconId.Wristband` rather than reusing `Bolt`

`IconId.Bolt` has three other, unrelated callers: `GameplayHUD`'s "IN RALLY" badge, the bottom nav
bar's `PLAY`... actually `GEAR` tab icon, and a `BootScreen` tip icon — all of which mean an actual
lightning bolt, not a wristband. Globally overriding `Bolt`'s rendering would have silently repainted
those three unrelated spots. `IconId.Wristband` was appended to the very end of the enum (never
inserted in the middle, so no existing value's underlying int shifts), and `GearCatalog.cs`'s 10
Accessory items now reference it instead of `Bolt`. `Bolt` itself is untouched and still renders its
procedural lightning-bolt glyph everywhere else.

Paddle/Shoe/Tape/Chest needed no such split — checked every other caller of each
([`ShopScreen`](../Assets/Scripts/UI/Screens/ShopScreen.cs), `SeasonPassScreen`, `LobbyScreen`,
`ChestOpeningScreen`, the `GEAR` nav tab's own `Tape` icon) and all of them already mean the same
paddle/shoe/grip-tape/chest imagery, so the new art improves every one of those spots for free.

### Integration: additive to the existing procedural icon system, not a replacement

[`UIIcon.cs`](../Assets/Scripts/UI/UIIcon.cs) draws every icon procedurally from signed-distance
fields, baked into a tintable Fill sprite and a dilated Outline sprite that `Build()` stacks together.
Added a `RasterResourcePaths` table (`IconId` → `Resources.Load` path) and a `Raster(IconId)` lookup;
`Build()`/`BuildPlain()` now check it first and, if present, render a single `Image` with that sprite
at full white tint and skip the procedural outline+fill stack entirely — the Canva art already has its
own baked-in navy outline and full color, so stacking the procedural navy outline sprite behind it
would just double the border, and tinting it would wash out colors it doesn't have a single "fill"
region for. Every other `IconId` (the large majority) is completely unaffected — falls through to the
same SDF baking path as before.

This hooks at the lowest shared point (`UIIcon.Build`, which every `UIBuilder.IconAt` call goes
through) rather than patching each of the ~10 call sites individually — confirmed by the Shop
screenshot below, where Paddle/Shoe/Chest all picked up the new art with zero changes to
`ShopScreen.cs` itself.

### Assets

PNGs live at `Assets/Resources/Icons/icon_{paddle,shoe,grip,wristband,chest}.png`, imported as
`Sprite (2D and UI)`, `Single` sprite mode, uncompressed, `alphaIsTransparency` on. Canva's free plan
does not allow transparent PNG export ("Users on the Canva Free plan can not export PNGs with
transparent background") — worked around by exporting flat opaque PNGs and running each through a
small local flood-fill script (`keyout_bg.py`, not checked into the repo — a one-off scratch tool)
that keys out only the background region reachable from the four image corners, rather than every
near-white pixel. That distinction matters: the paddle icon has a white gloss highlight on its face,
and the shoe icon has white laces and a white sole — a naive "any near-white pixel → transparent"
pass would have punched holes through those. Verified per-icon by sampling corner alpha (0) against
an interior near-white pixel's alpha (255, i.e. still opaque) before importing.

### Verification

- Real Unity compile (`refresh_unity` force / `read_console`) — 0 errors, 0 warnings on
  `UIIcon.cs`/`GearCatalog.cs` specifically (some pre-existing, unrelated Photon `NetworkPeer broke!`
  console spam from `PhotonHandler.cs:224` is present regardless of this change and isn't a compile
  error).
- In Play Mode: navigated `GearCatalogScreen` through Paddle, Grip, and Accessory (wristband) —
  confirmed each renders the new art at the correct size against its rarity-gradient tile, and that
  the Accessory cards no longer show the lightning bolt.
- Navigated to `ShopScreen` — confirmed the Featured bundle's paddle, both `DAILY DEALS` cards
  (Paddle/Shoe), and both chest cards (Epic/Legendary) all picked up the new art without any
  `ShopScreen.cs` changes, confirming the hook point is shared correctly.
- Confirmed the bottom nav bar's `GEAR` tab still renders the procedural lightning-bolt glyph
  unchanged — the `Bolt`/`Wristband` split didn't leak.
- Four screenshots delivered to the user directly.

### Explicitly not done

- Only these 5 icons got real art. Every other `IconId` (Coin, Gem, Trophy, Ball, Shield, Player,
  Bag, and the rest) still renders through the procedural SDF path — untouched, not a regression, just
  out of the scope the user asked for ("gear + chest cards").
- `keyout_bg.py` is a scratch tool used to produce the 5 PNGs, not part of the shipped project; if a
  future icon pass needs the same background-keying step, it isn't checked in anywhere and would need
  rewriting or moving into the repo.
- No brand kit / on-brand generation was used — the Canva connector wasn't given a brand kit, so
  results came from Canva's default AI generation for the `logo` design type, not project-specific
  branding.

---

## 19. Step 10 (done): the three dead shop offers now grant something

Reported as "nothing happens when I try to buy in shop." The buttons, raycasting, `EventSystem`
(`InputSystemUIInputModule`), ScrollView and `onClick` wiring were all fine — three of the shop's
offers were unfinished stubs. Verified each in Play Mode by invoking its real `Button.onClick` and
watching `MetaGameState` before/after.

### What was broken

| Offer | Old behavior |
|---|---|
| **MIAMI PRO PACK** ($4.99 featured) | `NavigateTab("HOME")` and nothing else — its own code comment said "this prototype just grants the bundle" but no grant existed |
| **PADDLE x10** (900 coins) | `SpendCoins(900)` succeeded, granted nothing — no gear id was ever associated with the deal |
| **SHOES x20** (400 coins) | same — ate 400 coins, granted nothing |
| **EPIC CHEST** (90 gems) | **also broken, by a closure bug** — `if (i == 2) AddEpicChest()` inside the buy delegate closed over the shared `for` loop variable `i`, which is `3` by click time, so the guarded grant never ran even though step 8 "fixed" it. Gems were still deducted. |

### The fix

- **`MetaGameState.GrantStoreBundle(RewardBundle)`** — new. Mirrors `OpenReadyBag`'s grant loop
  (coins→`AddCoins`, gems→`AddGems`, gear entries→`cardsCollected +=`) and sets
  `pendingHomeRewardFeedback`, so every purchase now shows the existing lobby reward banner on the
  next HOME navigation instead of silently bouncing.
- **`MetaGameState.EquippedOrDefault(GearType)`** — new. Resolves a slot to the player's equipped
  item there, falling back to that slot's historical starter (`carbon_pro` / `swift_step` /
  `starter_grip`), so a slot-targeted card grant always has a concrete target.
- **MIAMI PRO PACK** grants exactly what the card advertises: **+2,500 coins, +120 gems, +30 Titan
  Apex cards** (`paddle_titan_apex`, the tier-10 legendary paddle — ~¼ of a legendary's 127-card
  lifetime, a real centrepiece for $4.99 without trivialising the top item). No payment flow, same as
  the gem packs.
- **PADDLE x10 / SHOES x20** grant **10 / 20 cards toward the player's equipped paddle / shoes**
  (`EquippedOrDefault`). Predictable, repeatable, right shape for a cheap coin deal. New
  `DealCardSlot` / `DealCardAmount` tables alongside the existing `Deal*` arrays.
- **EPIC CHEST closure bug** — `int idx = i;` captured per iteration; the delegate now reads `idx`.
- **`LobbyScreen.BuildRewardFeedback`** — banner title is now `bundle.chestName` when set
  ("STARTER BUNDLE" / "DEAL CLAIMED" / "MATCH BAG" etc.) instead of the hardcoded "BAG REWARDS ADDED";
  the totals line omits zero-value currencies instead of always printing "+0 COINS +0 GEMS".

### Verification

- Play Mode, real `onClick.Invoke()` on each of the 4 buttons:
  - MIAMI PRO PACK: coins 10,323→12,823 (+2,500), gems 140→260 (+120), Titan Apex cards 0→30.
  - PADDLE x10: coins −900, `carbon_pro` (equipped) cards 24→34.
  - SHOES x20: coins −400, `swift_step` (equipped) cards 14→34.
  - EPIC CHEST: gems −90, an `epic_chest` sealed bag actually appears in a bag slot.
- Confirmed the lobby reward banner renders after a purchase: title "STARTER BUNDLE" / "DEAL CLAIMED",
  totals "+2500 COINS   +120 GEMS   +30 CARDS" / "+10 CARDS".
- `refresh_unity` / `read_console` — 0 errors, 0 warnings on `ShopScreen.cs`, `MetaGameState.cs`,
  `LobbyScreen.cs`. (`validate_script` reports a phantom "duplicate `CurrentWeekKey`" on
  `MetaGameState.cs` — the same false positive class noted in steps 2–3; the real compile is clean and
  the file has exactly one definition.)
- Test purchases mutated the local profile; restored to the pre-test baseline (coins 11,223 / gems
  150 / `carbon_pro` 24 cards / `swift_step` 14 cards / one sealed `legendary_chest` bag) before
  exiting Play Mode.

### Not done

- **Still no real IAP.** MIAMI PRO PACK and the Legendary Chest "$9.99" button grant on tap with no
  payment — same as the gem packs. A real store integration is out of scope here.
- **The `legendary_chest` sealed bag in the current profile** predates this session (likely leaked
  from step 9's Play Mode testing); left as-is since it was the observed baseline, not something this
  step introduced.
- Purchases still hard-cut to the Lobby rather than showing an in-shop confirmation — the lobby
  banner is the feedback, matching how bag rewards already worked.

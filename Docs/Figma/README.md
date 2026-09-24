# Pickle Smash — Figma boards

## Current reference pass (September 2026)

The supplied `Pickle_Ball (Copy) (1).zip` contains the same nine reference SVGs.
The home screen now uses the original stadium and player illustration. Individual
art elements are exported by `dev/ui/export_reference.py` to
`Assets/Resources/UIReference`; buttons, labels, balances, progress and navigation
remain live Unity UI. The blue-court alternative is exported but not selected.

`UIReferenceBoard` fits the 390 x 844 composition within the device safe area.
`UICapsuleFit` keeps pill ends circular, and capsule sprites have a nonzero centre
slice to prevent pinched progress bars. Fade-in preserves the board's fitted scale.
Settings presents the reference's four toggles, with existing player and input
settings behind **PLAYER & CONTROLS**. Match-found uses the reference gradient,
portraits and a three-second offline countdown. Live PvP timing remains negotiated.

Original artwork supplies the wordmark, button skin, nav icons, portrait discs,
back arrow and season medal. Coiny headlines and Lilita One body text carry the
remaining live copy; their OFL licenses are in `Assets/Resources/Fonts`.

`HomeCharacterStage` remains available but is no longer used by the lobby.
The historical notes below describe the earlier procedural implementation;
this section supersedes differences in those notes.


Source: `Pickle_Ball (Copy).zip` + `component_icon_buttons.svg`. Boards are 390 x 844.
Canvas reference is 1080 wide, so **Figma px x 2.769 = reference px** (`UITheme.F()`).

## Board to screen map

All nine boards are implemented.

| File | Screen | Unity |
|---|---|---|
| `Frame-1.svg` | Splash / loading, "PICKLE SMASH" | `BootScreen` |
| `Frame-2.svg` | Home, stadium backdrop | `LobbyScreen` |
| `Frame-3.svg` | Home, blue court backdrop | same screen, alternate backdrop |
| `INGAME-1.svg` | In-match HUD, "TAP TO SERVE" | `GameplayHUD` |
| `INGAME.svg` | Match paused modal | `PauseScreen` |
| `Frame-4.svg` | Ranked match, searching | `MatchmakingScreen` |
| `Frame-5.svg` | Match found, YOU vs REX D. | `MatchIntroScreen` |
| `Frame.svg` | Season complete, badge + league | `SeasonCompleteScreen` (new) |
| `Frame-6.svg` | Settings, four toggles + log out | `SettingsScreen` |

`extracted/` holds the raster art the boards embed: the 3D character render, the court render,
and the leaf-pattern splash background. These are scene/art assets, not UI.

Unity screens with no board — Gear Loadout, Gear Catalog, Gear Detail, League, Season Pass, Shop,
Tour Select, Chest Opening, Tutorial, Startup Results — inherit the direction through `UITheme`
plus the shared builders in `UIBuilder` (`Button`, `Ribbon`, `TopBar`, `BottomNav`,
`SkyBackground`, `RarityGradient`), which all delegate to `PSKit`. Restyling one of those builders
restyles every screen that calls it.

## Two flows changed shape

**Matchmaking split in two.** Searching and finding used to happen on one screen, with the
opponent's card swapping in place — so the moment of finding someone was spent on
`MatchmakingScreen` rather than on the board built to introduce them. Finding an opponent now
hands straight to `MatchIntroScreen`.

**The nav bar lost its PLAY tab.** Four tabs, per the board. Play is the lobby's own full-width
CTA; a nav tab duplicating the biggest button on the home screen only split the one action the
screen exists to offer. A screen passing `"PLAY"` as the active tab simply lights nothing.

## Season rollover is reported, not performed

`SeasonCompleteScreen` shows once when `MetaGameState.CurrentSeasonNumber` (derived from the
calendar — seasons are one month) moves past the last one seen, tracked in PlayerPrefs. It does
**not** reset the tier, bank the final standing or pay end-of-season rewards: that economy does
not exist yet, and a screen that silently zeroed a player's progress would be worse than no
screen. See `ScreenManager.MaybeShowSeasonComplete`.

## Palette (see `UITheme`)

| Token | Hex | Means |
|---|---|---|
| Volt / VoltBright / VoltDeep | `#D7F900` `#E3F946` `#6B7A00` | forward, confirm, primary. One accent, one meaning. |
| Blaze / BlazeDeep | `#FF4B26` `#7A1C0A` | destructive, urgency, CLAIM, unread badges |
| Bubblegum / BubbleDeep | `#FF6FA8` `#A83564` | social, shop |
| Court / CourtWell | `#0F4C5C` `#0A3540` | tertiary, avatar wells |
| Gold / GoldShade | `#F7C948` `#8A6A00` | currency and rewards only |
| Grass / GrassDeep | `#6FCF5A` `#3E7A2E` | the "+" affordance |
| Panel / Ink1 / Ink0 | `#12171C` `#0B0F14` `#050709` | neutral utility, outline, base |
| Cream | `#FFF9EA` | all text not sitting on volt |
| BgOrchid to BgBlush | `#CF87DB` to `#E2ACA8` | meta screens |
| BgViolet to BgOrchid | `#9D5FDF` to `#CF87DB` | screens with 3D behind them |

## Component recipe (see `PSKit.BuildStack`)

Every solid shape is the same four layers:

```
OUTLINE   hard black, full size
BASE      the colour's dark shade, inset by the outline weight
FACE      the colour, inset on the sides and top, stopping short of the bottom
SHEEN     capsules: a brighter inner pill.  Cards: a top band faded to nothing.
```

The strip of BASE left showing under the FACE is what makes a control read as a physical key.
A press slides the FACE down onto it by exactly that distance. Proportions scale with height:
outline 5.5%, side/top wall 4.5%, bottom lift 17%.

Sheen opacity carries intent: volt 40%, blaze 30%, court 20%, dark utility 7%.

Badges (count, rank) always straddle the OUTER edge of what they belong to, never sit inside
the face.

## Type

Two faces, split the way the boards split them:

- **Lilita One** (`Assets/Resources/Fonts/LilitaOne-Regular.ttf`, SIL OFL) is the game's voice —
  headlines, CTA labels, score digits, callouts. Everything routed through `UIBuilder.StrokedText`
  / `PSKit.Display` picks it up automatically via `UIBuilder.DisplayFont`.
- The system sans stays on body copy, list values, nav labels and the season card's lines, which
  is what the boards do. Set those with `UIBuilder.Text` / `PSKit.Body`.

Lilita One ships a single weight, so display text is set at `FontStyle.Normal` — asking Unity for
Bold on a one-weight dynamic font gets a synthesised smear rather than a heavier cut.

## Illustration built from the kit

Three things the boards draw as illustration are built from primitives instead, so they inherit
the kit's outline and tint with one colour argument rather than becoming imported art to maintain:

- **Gift** — `IconId.Gift`. A gold box with the ribbon and bow CUT OUT of the silhouette, which
  is how every icon here carries interior detail (see `IconId.Chest`, `Tape`, `Warn`). It is a
  separate id from `Chest` on purpose: `Chest` has raster art of a brown treasure chest and the
  shop, season pass and chest-opening sequence all mean that object.
- **Laurel wreath** — `PSKit.LaurelWreath`. Two mirrored arcs of `IconId.Leaf`, tapering and
  fanning outward toward an open crown, gathered by a capsule tie. `SeasonCompleteScreen` puts
  it behind the tier medal in the same gold, because the wreath belongs to the medal.
- **Pickle slices** — `IconId.PickleSlice`, scattered by `PSKit.ScatterPattern` behind the
  splash wordmark. The seeds are a tight cluster of seven rather than the obvious
  four-around-one: `IconId.Ball` is a disc with exactly that arrangement of holes, and the
  splash puts the two within a few hundred pixels of each other.

`SeasonCompleteScreen` moves the badge from the board's y=205 to 212 — the wreath is taller than
the bare medal the board measured, and at 205 its top leaves reach the title's baseline.

## The home character

`Frame-2.svg` puts the 3D player in the middle of the home screen, in a column at board
x 146..385, y 176..793 — behind the CTA and the nav bar, which is the depth order rather than an
overlap to fix. `LobbyScreen.BuildCharacter` fills that column, and `HomeCharacterStage` renders
him into it.

He is rendered live, not dropped in as a still: the meta canvas is ScreenSpaceOverlay over a
full-screen gradient, so no camera pointed at the scene can show through it, whatever its depth.
The stage builds a private world 5,000 units from the origin — beyond every scene camera's far
plane, and empty, so neither camera can see the other's subject — and composites the result as a
RawImage. Its lights are point lights with a finite range for the same reason: a directional
light has no position and would have lit the match as well.

The character comes from `Assets/Resources/Characters/HomeCharacter.prefab`, built by
**Pickleball/Setup Home Character** from the same rig, animator controller and `PB_Player_*.mat`
set the match characters use, so a colour change lands in both places. If the prefab is missing
the home screen simply has no character in it, which is the state the layout was drawn for.

Two things are worth knowing about the framing:

- The rig cannot be measured the obvious way. `Renderer.bounds` reports 2.04u on a figure that
  stands 1.80u (centimetre-authored meshes under a scale-100 node), and bone positions measure
  the wrong thing — the head bone is at the base of a skull that is nearly half his height.
  `HomeCharacterStage.TryMeasure` bakes the posed skinned meshes and boxes the vertices.
- He is framed at 65% of his column and stood on the bottom of it, not fitted to it. The board's
  character is a realistically proportioned adult; this one is chibi, and filling the column with
  him puts a head a third of a phone screen tall in the middle of the home screen.
- His head is turned to the camera in `LateUpdate`, aiming his face at the stage camera every
  frame. A fixed pitch correction does not work: the idle is a loop the head moves through, so the
  same number of degrees reads as looking up, straight ahead or at the floor depending on the
  frame. Aiming holds through the cycle and through any clip that replaces it. The correction is
  clamped to 42 degrees and touches nothing below the neck, so the stance stays as animated.

## Open art gaps

- **The character has no face.** The animation pack ships one untextured white material for all
  five sub-meshes; `CharacterSetup.ApplyPartColors` gives him skin, hair, shirt, shorts and shoe
  colours, which is enough at match distance but leaves a blank oval at hero size. The board's
  character has eyes, brows and a smile. This needs a face texture, or a different model — it is
  the one remaining thing between the home screen and the board.
- **The idle is a ready stance**, knees bent and weight forward, because it is the pack's in-play
  idle. The head is corrected to camera, but the body below the neck still reads as a player
  waiting for a serve rather than the board's upright, relaxed pose. A standing idle clip would
  fix it; none of the nine supplied clips is one (Victory is a jump).
- **The paddle is authored oversized** — ~42% of the character's height against a real one's ~28%.
  `CharacterSetup.HomePaddleScale` takes it to 70% on the home prefab only; the match characters
  still carry the full-size one, where the distance hides it. Worth fixing at the source.

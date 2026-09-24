**Pickle Smash: a lean roadmap to a sustainable game** — 7 September 2026.

This plan is for a solo developer or a team of 2–3 with a small budget. It is based on a read-only review of the Unity source, project documentation, saved screenshots and current public market sources. Gameplay was not personally playtested for this roadmap. Dates, prices, budgets and performance targets below are planning hypotheses, not forecasts or industry benchmarks.

**The recommendation is to win on satisfying, readable pickleball and earned mastery.** Build a game people voluntarily replay before expanding the economy or buying distribution. The initial promise should be: “A satisfying pickleball match in a few minutes. Easy to start, rewarding to master.” Test a 2–3 minute match target rather than advertising an unmeasured duration. The strongest potential signature is the dink → forced lob → smash sequence: recognisable pickleball tactics expressed through simple mobile controls.

Commit to a polished solo experience first. Make asynchronous challenges the first social feature; treat realtime friend matches as a conditional addition. Public ranked play, doubles and elaborate clubs should earn their place through evidence and engineering capacity.

**The existing project is a useful foundation, with several launch-critical gaps.** Some older design notes have stale status labels, so this assessment gives current code more weight than those labels.

| Area | Evidence in this project | What to do with it |
| --- | --- | --- |
| Core play | Swipe shots, tap serves, AI, kitchen/two-bounce checks and first-to-7 arcade scoring exist. The game-feel notes document automated and scripted checks. | Concentrate on touch, ball readability, animation and enjoyable difficulty on actual phones. |
| First session | Tutorial is a text card. New profiles inherit demo currency, advanced trophies, season tier 12 and cleared tours. | Create a true starter profile and a playable tutorial before testing onboarding or economy pacing. |
| Progression | Four gear slots, 40 items, upgrades, stat effects, bags, daily rewards and season XP already exist. | Reuse these systems; expose fewer choices initially and tune progression before adding more currencies or items. |
| Purchases and seasons | Real-money buttons show “coming soon”; season reward rows are partly mocked; premium entitlement and proper season rollover are incomplete. | Start with one small guaranteed purchase. Finish reward definitions and claims before offering a paid season. |
| Competition | Photon match/reconnect code exists. The match authority is a player client. Rating-aware matchmaking is incomplete; league rivals are locally generated. | Validate two-device play. Present AI competition clearly. Delay public ranked and global standings until integrity and population support them. |
| Persistence | PlayFab stores a client-written profile. A failed cloud load can be treated like a missing profile. | Distinguish missing data from service failure; protect existing saves and purchase entitlements. |
| Measurement | No gameplay event instrumentation was found in project-owned scripts. | Add a small, reliable event funnel before recruiting a large test cohort. |

Useful implementation references: [game-feel verification](GameplayFeel.md), [gameplay audit](Gameplay.md), [existing gear design](GearProgression.md), [starter state](../Assets/Scripts/Data/MetaGameState.cs), [profile initialization](../Assets/Scripts/Backend/ProfileService.cs), [cloud persistence](../Assets/Scripts/Backend/PlayFabProfileBackend.cs), [tutorial](../Assets/Scripts/UI/Screens/TutorialScreen.cs), [shop](../Assets/Scripts/UI/Screens/ShopScreen.cs), [season UI](../Assets/Scripts/UI/Screens/SeasonPassScreen.cs), and [matchmaking](../Assets/Scripts/Net/PhotonQuickMatch.cs). The existing game-feel report explicitly leaves phone touch/haptics and a two-device network match unverified; passing simulation tests does not establish that humans enjoy the game.

**Choose a narrow audience and a distinct public identity.** Start with recreational pickleball players who also enjoy casual mobile games. Recruit nearby players for observation and a separate English-speaking U.S. cohort to test commercial relevance. Compare them with a small sample of casual sports-game players who do not know pickleball. Use one platform first, selected by access to devices and testers; Android is a practical default if that is where the team can test consistently.

SFIA reports 24.3 million Americans played pickleball in 2025, with participation spanning age groups. This supports audience testing; it does not tell us how many people want this mobile game or will pay for it. [SFIA’s June 2026 report announcement](https://sfia.org/resources/sfia-releases-2026-pickleball-single-sport-report-team-sports-reports-to-follow/).

| Comparable product | Publicly advertised offer | Strategic implication |
| --- | --- | --- |
| Pickleball Smash | Short 3D matches, multiplayer, drills and upgrades; free with IAP. | Speed and the sport alone are insufficient differentiation. [App Store listing](https://apps.apple.com/us/app/pickleball-smash/id6759366479). |
| Pickleball Stars | 3D play, customisation, local LAN and left-handed controls; free with IAP. | Accessibility and friend play already have competitors. Prove the quality of your controls. [App Store listing](https://apps.apple.com/us/app/pickleball-stars/id6740270726). |
| Pickle League Arcade | One-thumb retro play, offline singles/doubles and a subscription career. | Offline play also needs an appealing gameplay identity. [App Store listing](https://apps.apple.com/us/app/pickle-league-arcade/id6795694426). |

These are developer descriptions, not independently verified feature quality or evidence of financial success. Because similar public names already exist, settle a distinctive name, store search positioning and final bundle identifiers before paying for promotional assets. “Pickle Smash” remains a working title in this plan.

**Use the first 12 weeks to decide whether the game deserves a larger launch.** For 2–3 experienced people working consistently, reserve a further 4–12 weeks for a limited release and fixes. A solo developer should allow roughly 6–9 months for the overall sequence, longer if part-time. The milestone conditions matter more than the dates.

| Phase | Product work | Outreach running alongside it | Evidence needed to proceed |
| --- | --- | --- | --- |
| Weeks 1–2: establish a trustworthy first session | True starter save; safe cloud failure handling; basic events; install on representative phones; simplify first-session menus. | Recruit 10–15 observed testers, including people unfamiliar with the controls. | They can start a rally and understand a missed shot without repeated explanation; no save-reset failures. |
| Weeks 3–4: prove the match | Playable tutorial; forgiving early AI; clear contact feedback; comfortable gestures; dink/block/serve animation; instant rematch; free practice. | Expand to 30–50 testers. Record first sessions and ask what made them stop. | Most finish a first match; many choose a rematch; the largest control complaints disappear. |
| Weeks 5–8: prove return play | A compact AI tour; staged gear introduction; three reusable daily challenge templates; personal bests; clear next goal; reliable saves. | Pilot 2–3 clubs and a few creators. Collect return data without daily reminders. | Improving D1/D7 cohorts; progression is understandable; a recurring reason to return emerges. |
| Weeks 9–12: prove a route to players and payment | One cosmetic bundle and a small catalog after purchase recovery works; a shareable challenge card/code; optional ad experiment after a baseline. | Test three gameplay clip angles, club QR signups and creator-specific links. Aim for 100–300 beta players, subject to recruitment. | At least one source brings people who complete matches and return. Purchases are delivered reliably; offers do not visibly damage retention. |
| Weeks 13–24: limited release, conditional | Fix the largest measured problem each cycle. Add friend rooms only if demand and network quality justify them; broaden content gradually. | Concentrate launch activity in one audience/platform; collect larger fresh cohorts; test small paid campaigns only after quality and retention gates. | Stable device performance and repeatable retention. More spending requires credible unit economics, not just installs. |
| After validation | Consider a real season pass, trustworthy public ranked, then doubles or clubs one at a time. | Recurring creator challenges and selected partnerships. | Players use and request the feature; team can fund its ongoing support. |

At weeks 4, 8 and 12, make an explicit continue/fix/reduce-scope decision. If two focused iterations fail to improve the same weak funnel stage, revisit the controls, match format or audience. Adding courts and shop offers is unlikely to resolve a weak first match.

**Keep the first release small enough to polish.** The core loop should be: play a short match → understand the result → earn visible progress → choose a rematch or a specific skill challenge.

| Priority | Features and intended player benefit |
| --- | --- |
| Essential | A 60–90 second playable introduction: tap serve, aim a return, win a rally. Teach dinks and other gestures gradually; make the tutorial replayable. |
| Essential | Reliable touch controls, readable ball/landing cues, useful failure feedback, sensitivity and accessible UI. Test left-hand behaviour rather than assuming mirrored aim is sufficient. |
| Essential | Free practice and a short AI tour with distinct opponent behaviours. Reuse one strong court plus a small number of visual variants; add variety through tactics. |
| Essential | No currency requirement for basic play. Existing tour entry fees must not leave a new player unable to play or recover. Bag timers can pace bonuses, not court access. |
| Essential | A real starter save; safe saving/recovery; honest labels for AI opponents; real reward quantities and timers; events and crash reporting. |
| Next | Daily “land five dinks,” “hit serve targets,” and “beat this AI challenge” variants using existing mechanics. Start with personal bests and the same challenge for everyone. |
| Next | Shareable score cards with a challenge ID and build/version. Open the same challenge manually from a code; defer a full social graph and sophisticated deep links. |
| Conditional | Private friend rooms, invitations, rematch and quick emotes. Add only after tests cover latency, disconnects, reconnects and mismatched versions. |
| Later | Real global leaderboards, public ranked, clubs, doubles, spectator tools, automated video replays, licensed athletes and brands. |

Do not schedule a full career, live ranked, doubles, clubs and a paid season simultaneously. If live friend play proves essential to the hook, trade out tour breadth to make room for it. While multiplayer is being tested, use scheduled community sessions to concentrate testers and keep solo play immediately available.

**Monetisation should begin with a small, transparent offer.** Keep core matches free. Sell visible identity first: paddle looks, outfits, celebration animations and profile cosmetics. Cosmetics must remain readable and cannot obscure the ball or hide useful information.

The existing gear document approves a card-and-upgrade economy in which spending accelerates gameplay power. My proposed revision is to retain earned gear progression for the solo tour and equalise gameplay stats in any future ranked mode. This is a proposed product decision, not a change made to the existing design. “Buying time” still buys an advantage when it reaches stronger gear sooner; diminishing returns do not eliminate that concern. Avoid selling power while deciding the competitive policy.

| Offer | Initial price hypothesis, USD | When and how to test |
| --- | --- | --- |
| Supporter starter pack | $2.99 | One guaranteed paddle cosmetic, outfit colour and founder badge. Present after several enjoyable matches; no forced popup during play. |
| Individual cosmetics | $1.99–$4.99 | Launch with only 3–5 attractive items. Preview them on the player and measure which are actually used. |
| Rewarded video | No charge | Optional extra soft currency after a match, initially capped at 3 offers/day. Award the normal reward regardless; an ad failure must never block the next match. |
| Season pass, later | Test around $4.99 per season | Only after a complete reward ladder, premium entitlement, catch-up claims, season rollover and a sustainable content cadence exist. Show contents and dates clearly. |
| Alternative if players prefer solo | Test a $4.99–$7.99 permanent career expansion | Consider if solo retention is good but cosmetics/live competition are weak. Treat as an alternative business direction, not another simultaneous launch system. |

These prices are experiments, not price recommendations inferred from competitor success. Localise store prices and assess willingness to pay by market. Do not add several monetisation systems to the same small cohort at once: establish a baseline, test the bundle, then test optional ads.

Avoid forced interstitials during rallies or between tapping Play and entering the match. Google Play explicitly restricts disruptive placements and distinguishes explicitly opted-in rewarded ads. [Google Play ads policy](https://support.google.com/googleplay/android-developer/answer/9857753?hl=en).

For the first paid release, use native purchase handling, transaction validation, grants that cannot be duplicated, restore/recovery where applicable, and support for interrupted purchases and refunds. Keep trusted entitlements separate from freely client-editable progression. If paid random chests are retained later, expose accurate odds before purchase; Apple explicitly requires odds disclosure for purchased random items. Guaranteed cosmetics are simpler to explain and operate. [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/).

**Outreach should produce learning and returning players from the start.** Reserve at least half a day each week for recruitment and feedback. Sequence the channel experiments below: begin with club recruitment and one repeatable clip format, then add a creator pilot when there is capacity. The suggested cadences are options for the active channel, not simultaneous commitments; observed playtest weeks require additional time or less development work.

| Channel | Concrete first experiment | What earns the next investment |
| --- | --- | --- |
| Local clubs/coaches | Build a list of 20 relevant organisers. Personally approach 5 per week; aim for 2–3 pilot groups of 10–15 testers. Observe a short courtside playtest and run a seven-day “beat the coach” challenge. | Players complete matches and return without organiser reminders; organisers want a second challenge. |
| Small creators | Shortlist 15–20 coaching, recreational or trick-shot creators. Start with 3–5 tailored pitches containing a 15-second real gameplay clip and a build. Offer their audience one clear challenge. | Installs lead to completed matches and D7 returns. Evaluate a small paid pilot only after audience fit is visible. |
| Short video | Post 2–3 clips weekly: “dink or smash?”, a comeback rally, and a real-court tactic recreated in the game. Show the interesting decision/result immediately. | Qualified store visits and retained players per clip, not views alone. |
| Communities | Participate in relevant club chats and groups; request moderator permission for a specific playtest post. Share what player feedback changed. | Useful feedback and repeat participants; no dependence on repeated promotional posting. |
| Store listing | Build a clear icon, honest gameplay screenshots, a 20-second trailer and a one-sentence hook. Test one meaningful variation at a time when traffic permits. | Better store-visit-to-install conversion for the same audience, while preserving first-match completion and retention. |
| Partnerships, later | Approach a coach, small paddle retailer or local event with a themed challenge and attributable campaign. Use branding only with permission. | A measured audience benefit and retained players justify the coordination and content cost. |

For prospect discovery, use the [USA Pickleball club directory](https://usapickleball.org/clubs/) and [Pickleheads](https://www.pickleheads.com/) alongside clubs accessible to the team. A directory listing does not imply permission to promote. Specialist media such as [The Dink’s partnership offering](https://partner.thedinkpickleball.com/) can be considered after retention is demonstrated; obtain actual terms before budgeting a placement.

An outreach draft for the team to send: “We’re a small team making a mobile pickleball game focused on satisfying rallies. Could a few members try a 10-minute session and tell us where the controls feel wrong? We can run a ‘beat the coach’ challenge and share what we improve from your feedback.” No messages or campaigns were sent as part of this roadmap.

Prepare one lightweight press kit: public title, one-sentence hook, brief description, trailer, 5 screenshots, icon, store/test links, supported devices and contact address. Use a simple signup form and existing community tools before building a custom website or community platform. A QR code can point to the signup/store page; it is not a purchase-unlock mechanism.

**Measure behaviour before judging success.** Add first launch, tutorial step/completion, match start/end, mode, duration, result, fault reason, rematch, session return, upgrade, reward claim, purchase outcome and ad outcome events. Include build, device class, country/platform and acquisition source where available. Record gameplay telemetry at useful summary granularity; do not start by logging every frame. Configure privacy disclosures and consent to match the SDKs and markets actually used.

| Metric | Initial internal target | If it misses |
| --- | --- | --- |
| Tutorial completion | At least 85% of tutorial starters | Shorten the lesson; simplify the first gesture; inspect device-specific failures. |
| First match completion | At least 80% of new players who start one | Investigate control confusion, difficulty, match length and crashes. |
| Voluntary immediate rematch | At least 40% of first-match finishers | Improve the result/rematch flow and the match itself. Exclude prompted research sessions. |
| D1 / D7 / D30 retention | At least 35% / 12% / 5% as early directional gates | Separate first-session problems from missing longer-term goals. These thresholds do not establish profitability. |
| Quality | At least 99.5% crash-free sessions; zero known purchase/save-loss blockers | Fix reliability before recruiting broadly. Target stable 60 fps on main devices, with a tested 30 fps fallback on lower-end hardware. |
| Realtime readiness, if added | At least 98% of started test matches complete without a technical abort | Fix networking before public ranked. Separately measure queue abandonment, latency and fairness. |

Define Dn retention as the share of a first-launch cohort with a session in the 24-hour interval starting n days after first launch, using a consistent UTC convention. Keep the full cohort denominator, including users who fail onboarding. Only report D7/D30 when the cohort has matured. Show sample sizes and uncertainty, segment by source/build/device, and keep observed club tests separate from unprompted acquisition cohorts.

Thirty players reveal usability problems; they do not validate monetisation or market retention. Work toward two fresh cohorts of roughly 500 players for a more stable directional retention check if recruitment and funding allow. Even that can produce too few payers for reliable lifetime-value estimates. Small budgets may require more time and organic recruitment rather than a weaker evidentiary standard.

**Treat the marketing budget as an experiment loss limit.** An illustrative cash cap is $500–$1,500, released in stages rather than committed at launch. It excludes labour, devices, store accounts, art/audio production, software and ongoing backend/support costs; inventory those separately. If that cap is too high, begin with unpaid recruitment and self-produced clips.

| Share of experiment cap | Purpose |
| --- | --- |
| 25% | Observed playtests, recruitment logistics and small participation thank-yous; never rewards for positive store reviews. |
| 20% | Reusable video/screenshot production. |
| 20% | One or two narrowly scoped creator experiments after unpaid audience-fit tests. |
| 20% | One paid acquisition channel after retention gates; keep it in one audience/platform. |
| 15% | Reserve for the experiment that produces the clearest retained-player signal. |

Before paid scale, compare net cohort lifetime contribution per install with cost per install. Net contribution includes purchase proceeds after actual fees/refunds, ad proceeds and variable service costs. Keep fixed development and content costs visible separately. Use conservative estimates and a margin for uncertainty; do not extrapolate an early paying enthusiast into a profitable lifetime-value curve.

For modelling, eligible enrolled developers may receive a 15% IAP commission/service-fee tier under the relevant [Apple Small Business Program](https://developer.apple.com/app-store/small-business-program/) and [Google Play fee programme](https://support.google.com/googleplay/android-developer/answer/112622?hl=en). Verify the applicable terms and also model a less favourable fee case.

An illustrative month—not a forecast—shows the scale required: 5,000 monthly players, 2% paying and $8 gross spend per payer gives $800 gross IAP, or $680 after an assumed 15% store fee. At 1,000 daily players, 25% watching one ad/day and an assumed $8 realised ad eCPM, ads add $60 over 30 days. That is $740 before refunds, taxes, services, acquisition, content and salaries. Each input must be replaced with observed data. A small audience can validate the game while still being far from supporting a team.

**Assign time as deliberately as money.** With three people: one owns gameplay/device quality, one owns persistence/purchases/builds, and one owns art/UI plus community testing. With two, combine the latter responsibilities and cut live features. Solo, work sequentially and reserve recurring time for testers and release support. Keep at least 20% of production capacity for device fixes, store work and unexpected problems.

The next ten working days should produce a concrete validation build: days 1–2 establish the real starter save and safe cloud loading; days 3–5 instrument and test the first match on phones; days 6–8 implement the smallest playable introduction and tune controls/AI from observation; days 9–10 conduct fresh playtests and review the funnel. Recruit testers alongside this work. The next planning decision should be based on what those players do, especially whether they choose another match.

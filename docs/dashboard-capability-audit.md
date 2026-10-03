# Personal daily/weekly capability audit — Testing 0.0.71 candidate

Prepared 0.0.73 correction: [selected-client diagnosis](releases/testing-0.0.73.md)
classifies the agent remaining field unsupported, corrects the selected-client
source to catalog-indexed manager usage corroborated against active agent
usage/rank/satisfaction, and derives residual capacity without claiming
deliverability. Global allowances are live-observed in72; corrected client detail
is reference/SDK/fixture-backed, not live73 validated.

0.0.72 correction: [GitHub timer reference audit](timer-plugin-references.md)
found the actual Timers addon `ContentsInfo` and the omitted loaded
DomanEnclaveManager source. The Doman row below is updated to implemented local
facts. Other classifications remain explicit; reference use is not live proof.

Status: source/SDK/catalog validation, **not live-game or production validation**.
Starting source: `d4d33572a1edcc3386d678ebf35abb8b84644892`, Testing 0.0.70 lineage.
Game catalog: `2026.09.15.0000.0000`; installed FFXIVClientStructs: `7.56.2.9136`.
The supplied objective's 0.0.68 identity was superseded before this audit.

Scope: personal facts only. Site owns Dashboard layout, tasks, matching,
recurrences, presets and manual completion. No new server resource is sent.
No interface opening, native mutation, hidden requests, packet hooks, OCR or
rendered text parsing. There are no third-party plugin runtime dependencies.

## Evidence and interpretation

Inspected actual installed SDK public members, offline installed Lumina sheets,
current plugin collectors/configuration/tests and current Site Game Sync scope,
storage and Retainer contracts. Native definitions were read from the pinned
[FFXIVClientStructs source](https://github.com/aers/FFXIVClientStructs/tree/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client).
The installed assembly compiles the implementation; the source pin is supporting
semantic evidence, not proof of live ownership, packet freshness or reset behavior.
The existing offline test executable has `--dashboard-sdk` and
`--dashboard-catalog <sqpack>` inventory modes; neither accesses live pointers.

All new personal facts are private, default-OFF, latest-only observations.
**Absence/unloaded/unsupported always means UNKNOWN/UNAVAILABLE, never zero.**
An explicitly loaded, gated source can supply an observed zero, but this does not
prove eligibility, reward claim, or current reset-window membership. Native cache
ownership and post-reset response freshness remain unverified. Export says so.
Every implemented row below needs live comparison, reload/character isolation
and frame-time validation. Fixture passes do not remove that requirement.

Cost codes: **B** = bounded fields/getters in one source group per five seconds;
**U** = B only while the player naturally opened the source UI; **E** = existing
collector cadence; **S** = static catalog once; **—** = not read/retained.
Freshness: new facts are OBSERVED only after a matching current-session read,
for at most two minutes and before any native next boundary; otherwise STALE.
OBSERVED is a timestamped cache observation, **not server-confirmed completion**.

## Capability matrix

Classification is about available evidence, not a promise of live correctness.
"Site" below means implemented local Site source/contract, not runtime verification.

| System / datum | Cadence | Exact source | Existing Gillions availability | Reliability / support | Implemented result; freshness or gap | Cost / privacy |
| --- | --- | --- | --- | --- | --- | --- |
| Daily roulette reward flag | Daily | `InstanceContent.IsRouletteComplete(ContentRoulette.RowId)` | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | All 11 catalog reward categories; active owning ContentsFinder; eligibility and reset UNKNOWN | U+S / private |
| Roulette eligibility/locked state | Selection/job dependent | `ContentInterface.IsUnlocked`, `ContentRoulette.OpenRule` | UNAVAILABLE | PARTIALLY OBSERVABLE | No verified existing interface pointer; static unlock rules are not personal eligibility. Never infer locked = completed | — / not retained |
| Role in need | Dynamic | `AgentContentsFinder.ContentRouletteRoleBonuses[11]` | UNAVAILABLE | PARTIALLY OBSERVABLE | Present typed fields; array/index-to-category and freshness not independently established; withheld | — / not retained |
| Normal Raid weekly item/coin reward | Weekly where current rules restrict | `AgentContentsFinder.NumCollectedRewards`, interface received/max count; static CFC/InstanceContent reward data | UNAVAILABLE | PARTIALLY OBSERVABLE | Selected detail count exists, but exact reward types/window semantics not proved. No synthetic per-duty booleans | — / not retained |
| Savage/Ultimate/high-end weekly loot | Weekly/none depending duty | `PlayerState.WeeklyLockoutInfo` (internal, processing undocumented), Raid/ContentsFinder detail | UNAVAILABLE | NOT RELIABLY OBSERVABLE | No verified typed per-duty current-period map; lifetime clear is not lockout | — / not retained |
| Alliance Raid weekly reward/coin | Weekly/none depending patch | Same selected reward counts + static duty rules | UNAVAILABLE | PARTIALLY OBSERVABLE | No reliable reward-specific claimed/window evidence; unrestricted historical raid must not become a weekly obligation | — / not retained |
| Duty unlock/lifetime clear | Permanent | UIState/PlayerState unlocks, ordinary quest/achievement/character data | ALREADY AVAILABLE ON SITE for existing supported facts | IMPLEMENTED ALREADY / STATIC REFERENCE ONLY | Reuse supported snapshots/catalogs; not weekly completion or loot proof | E / existing private |
| Custom Delivery global allowance | Weekly | `SatisfactionSupplyManager.GetUsedAllowances`, `GetResetDateTime` | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Used/12/remaining and native next reset; own active initialized SatisfactionSupply, no forced request | U / private |
| Custom Delivery selected client | Weekly counts + permanent rank | Catalog-indexed `SatisfactionSupplyManager` usage/rank/satisfaction corroborated against active `AgentSatisfactionSupply.NpcData` | Prepared corrected collector73 | CONDITIONAL AFTER NATURAL UI LOAD; not live73 proven | Matched selected client only: corroborated used/cap/residual capacity, satisfaction current/max, rank current/max. Agent remaining is unsupported; residual is not deliverability. Mismatch preserves global/old client state. Other clients remain unknown | U+S / private |
| All Custom Delivery client availability | Unlock/progression | Satisfaction NPC static requirements and quest data | ALREADY AVAILABLE ON SITE for prerequisite facts; native availability UNAVAILABLE | STATIC/REFERENCE ONLY | Site may display prerequisites; no fabricated visit recommendation or all-client availability flags | S / existing private prerequisites |
| Doman weekly donated/cap/remaining | Weekly | `DomanEnclaveManager.IsLoaded/State.Donated/Allowance/IsAcceptingDonations` | NEW COLLECTOR in 0.0.72 | RELIABLY COLLECTABLE typed loaded cache; ownership/reset proof unverified | Positive cap and donated<=cap required; remaining subtraction, accepting flag distinct from completion. Native reset unknown. ReconstructionBox offered-item totals are NOT substituted | B / private |
| Doman reconstruction unlock/progression | Permanent | Existing filtered normal quests | ALREADY AVAILABLE ON SITE where supported | IMPLEMENTED ALREADY | Reuse quest facts; never turn progression into weekly donation completion; multiplier not newly inferred | E / existing private |
| Fashion Report score/attempts | Weekly window | `FashionCheckManager.PlayerInfo.HighScore/Remaining`, Theme, pending flags | UNAVAILABLE | PARTIALLY OBSERVABLE | Fields exist but no verified completed response/load-owner gate. No new score/participation/reward claim. No solution/theme collection | — / not retained |
| Masked Carnivale weekly challenges | Weekly | `AgentAozContentBriefing.WeeklyAozContentIds`, `IsWeeklyChallengeComplete`; `AozContentData.UnkLoadState` | UNAVAILABLE | PARTIALLY OBSERVABLE | Weekly getter distinct from lifetime clears; load state private/unknown and typed AozContent sheet unavailable in installed sheets. Withheld rather than treating zero flags as incomplete | — / not retained |
| Masked Carnivale lifetime stages | Permanent | `PlayerState.CompletedMaskedCarnivale` | UNAVAILABLE as a weekly fact | STATIC/REFERENCE ONLY | Stage clears cannot complete weekly challenges. No duplicate lifetime collector for Dashboard | — / not retained |
| Challenge Log completion | Weekly | `ContentsNote.State.Loaded`, `IsContentNoteComplete`; cached UIState next reset | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | 90 catalog rows with requirements within the native 104-bit domain. Completion only; optional native next boundary. Natural visible ContentsNote | U+S / private |
| Challenge Log numeric progress | Weekly | `DisplayIds/DisplayStatuses[19]`, Lumina `RequiredAmount/ContentType` | Static requirement/category only | PARTIALLY OBSERVABLE | DisplayStatuses semantics are undocumented; not used as kill/craft/progress counter. Site joins requirement/category by ID | S / no new private progress |
| Current limited tomestones earned/cap | Weekly | `InventoryManager.GetWeeklyAcquiredTomestoneCount`, `GetLimitedTomestoneWeeklyLimit`; loaded Currency; TomestonesItem/Tomestones | NEW COLLECTOR REQUIRED; balance ALREADY AVAILABLE ON SITE | RELIABLY COLLECTABLE typed getter, live freshness still unverified | Earned/cap/remaining, not balance. Single current capped catalog currency item 49, cap 900 in inspected build; native/static mismatch fails closed. Reset UNKNOWN | B+S / private |
| Other scrip/currency balances | Balance, not recurrence | Ordinary currency inventory/special getters | ALREADY AVAILABLE ON SITE | IMPLEMENTED ALREADY | Reuse balance; no evidence of weekly earned/cap for these, so no invented weekly progress | E / existing private |
| Leve allowance | Regeneration, not daily checklist | `QuestManager.NumLeveAllowances`, UTC `GetNextLeveAllowancesUnixTimestamp` | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Remaining/100 plus coherent positive native next regeneration; natural Timers. Missing/past timer preserves state | U / private |
| Allied Society shared allowance | Daily | `QuestManager.GetBeastTribeAllowance` | NEW COLLECTOR REQUIRED; ranks/reputation ALREADY AVAILABLE ON SITE | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Remaining/12, including loaded zero. Native next reset UNKNOWN; remaining is not quest completion | U / private |
| Society accepted/completed daily quests | Daily/acceptance | `QuestManager.DailyQuests[12]`, `DailyQuestWork` | UNAVAILABLE as reset-proven completed history | PARTIALLY OBSERVABLE | Active work is not a complete daily turn-in history; ordinary journal intentionally excludes repeatable/society quests | — / not retained |
| Ehcatl Nine delivery daily allowance | Daily/legacy society rules | Empty typed `UIState.DailyQuestSupply`, shared QuestManager allowance | Shared allowance above; distinct data UNAVAILABLE | NOT RELIABLY OBSERVABLE distinct counters | No duplicate or guessed allowance; shared society counter does not prove Ixal-specific completion | — / not retained |
| Treasure Map allowance | Rolling next availability | Cached `UIState.NextMapAllowanceTimestamp` | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Positive future native timestamp only; no RequestResetTimestamps. Past/zero does not prove eligibility or map obtained | U / private |
| Squadron mission/training finish | Expected completion | `PlayerState.SquadronMissionCompletionTimestamp`, training counterpart | NEW COLLECTOR REQUIRED | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Positive future expected timestamps, not reward claim or successful mission result; natural Timers | U / private |
| Squadron next mission allowance | Daily/weekly special rules | `PlayerState.WeeklyLockoutInfo`, GCArmy managers | UNAVAILABLE | NOT RELIABLY OBSERVABLE | Completion timestamps do not supply allowance; undocumented weekly bits withheld | — / not retained |
| Retainer venture expected finish/results | Rolling voyage timer | Existing loaded roster/selected retainer/result observations | ALREADY IN GAME SYNC; compatible gated Site contract | IMPLEMENTED ALREADY | Reuse existing expected completion and observed results/account isolation. Expected finish is not reward claim. No second source | E / existing private |
| Wondrous Tails held journal | Journal expiration | `PlayerState.HasWeeklyBingoJournal`, expiration, 16 orders/status getters | NEW COLLECTOR REQUIRED | RELIABLY COLLECTABLE positive held state | Held journal, stickers/9, Second Chance/9, all 16 objective IDs and native statuses 0 open / 1 claimable / 2 claimed; future native expiration required | B+S / private |
| Wondrous Tails turn-in / no journal | Weekly issuance/expiration | Same held flag only | UNAVAILABLE | NOT RELIABLY OBSERVABLE turn-in proof | False flag cannot distinguish never obtained, expired, turned in or unloaded. No false absence/turn-in fact | — / not retained |
| Daily/weekly Hunt Bills | Daily/weekly, ownership/reset unverified | Existing MobHunt cache + matching loaded Key Items | ALREADY IN GAME SYNC local-only | IMPLEMENTED ALREADY | Reuse existing source/labels/counters/retention; no second cache, no new upload. Owner's live tests are evidence for that existing collector only | E / private |
| Submarine voyages/results | Rolling voyage | Existing workshop/voyage event collector | ALREADY IN GAME SYNC local-only | IMPLEMENTED ALREADY | Reuse current build/routes/expected return/observed results. Owner has no access; live capture remains unverified, not falsely declared sync-ready | E / private and separate sanitized community opt-in |
| Faux Hollows / Unreal tell-retell | Weekly | `PlayerState.FauxHollowsTimestamp/State`, AddonWeeklyPuzzle | UNAVAILABLE | NOT RELIABLY OBSERVABLE | Raw state meanings/window/claims undocumented; puzzle tiles/reward text are not reliable weekly allowance. No rendered-text scrape | — / not retained |
| Jumbo Cactpot participation/claim | Weekly fixed drawing | `GoldSaucerManager.WeeklyLotOffsetTime`, empty AddonLotteryWeeklyInput | UNAVAILABLE | NOT RELIABLY OBSERVABLE | Schedule offset is not personal ticket/claim evidence; no structured verified ticket state | — / not retained |
| Mini Cactpot daily remaining | Daily | `AgentLotteryDaily.Status` | UNAVAILABLE | PARTIALLY OBSERVABLE | Status 1-4 is current game interaction/payout, not all daily remaining attempts. No false daily completion | — / not retained |
| Gold Saucer weekly Challenge Log | Weekly | Existing new ContentsNote completion collector | NEW COLLECTOR above | COLLECTABLE ONLY AFTER NATURAL UI LOAD | Covered by catalog challenge IDs; no duplicate Gold Saucer scan | U / private |
| Lord of Verminion/Triple Triad tournaments | Event windows | UIState.LovmRanking/TripleTriad; GoldSaucer managers | Card ownership ALREADY AVAILABLE ON SITE; tournament participation UNAVAILABLE | PARTIALLY OBSERVABLE | Ranking/window not reward-claimed proof; permanent cards reused, tournament tasks manual | — / not retained |
| Island Sanctuary favors | Weekly cycle | `MJIFavorState.UpdateState==2`, nine craft IDs/delivered/shipped fields; MJIManager ownership/load unknowns | UNAVAILABLE | PARTIALLY OBSERVABLE | Typed delivered state promising, but own-island versus visitor/session provenance and cycle identity not verified. No force RequestFavorData | — / not retained |
| Island workshop/rest/cycle/demand | Weekly/rolling cycle | `MJIManager.CurrentCycleDay`, rest arrays, DemandDirty, groove field explicitly unverified | UNAVAILABLE | PARTIALLY OBSERVABLE / NOT WORTH COLLECTING optimizer inputs | Calendar is not completion; no optimizer, schedules, demand requests or workshop actions. Exclude questionable groove | — / not retained |
| GC Supply/Provisioning | Daily requests | `AgentGrandCompanySupply` requested items and TurnInAvailable; empty GCSupply | UNAVAILABLE | PARTIALLY OBSERVABLE | Current turn-in availability/request != all daily consumed/claimed; no reliable complete/reset proof. Expert delivery not a daily obligation | — / not retained |
| Repeatable weekly reward quests / expansion-specific unlocks | Per quest repeat rule | QuestRepeatFlag/Quest.RepeatIntervalType; ordinary quest filtering | Prerequisites ALREADY AVAILABLE ON SITE; current-period turn-ins UNAVAILABLE | PARTIALLY OBSERVABLE / STATIC REFERENCE ONLY | No hard-coded current endgame list. Existing permanent quests are not weekly clears; unknown reset flags withheld | S+E / existing private prerequisites |
| Frontline weekly matches/placements | Weekly | `PvPProfile.IsLoaded`, WeeklyMatches/First/Second/ThirdPlace | NEW COLLECTOR REQUIRED | RELIABLY COLLECTABLE typed loaded state | Four bounded counters, placements cannot exceed matches; progress only, reset UNKNOWN. Never use total lifetime counts | B / private |
| Rival Wings weekly matches/wins | Weekly | `PvPProfile.IsLoaded`, RivalWingsWeeklyMatches/MatchesWon | NEW COLLECTOR REQUIRED | RELIABLY COLLECTABLE typed loaded state | Two bounded counters, wins <= matches; progress only, reset UNKNOWN | B / private |
| PvP Series/Season ranks and rewards | Seasonal, not daily/weekly | `PvPProfile.Series/SeriesCurrentRank/SeriesClaimedRank` | UNAVAILABLE for this new daily/weekly contract | NOT WORTH COLLECTING in this bounded objective | Separate achieved/claimed evidence exists; not a weekly task. No scope expansion into seasonal progression | — / not retained |
| Deep Dungeon/Eureka/Bozja/Occult Crescent records | Duty/permanent/event | Typed instance/public directors and permanent quest/achievement progress | Existing supported lifetime progress reusable | STATIC/REFERENCE ONLY for reset-aware goals | Instance progress doesn't prove repeat-period reward claim; no speculative weekly boolean for current expansion systems | E / existing private |
| Ocean Fishing/GATE/weather/event schedules | Public schedule | Static sheets/director timing | Reference-only | STATIC/REFERENCE ONLY | Site/reference can show schedule; not personal attendance or daily completion | S / public reference |
| Housing lottery windows/participation | Multi-day window | Lottery UI/event structures | UNAVAILABLE as verified personal fact | PARTIALLY OBSERVABLE / outside bounded high-value subset | Schedule doesn't prove entry/claim; no personal lottery capture or automatic UI | — / not retained |
| Achievements, collections, jobs, one-time quests, reputation, Shared FATE | Permanent progression | Existing ordinary snapshot collectors and Site readers | ALREADY AVAILABLE ON SITE | IMPLEMENTED ALREADY | Reuse exact supported facts. Achievement completion != weekly activity; unlocked != currently eligible; all missing snapshots remain unknown | E / existing private |
| Free-form personal checklist/manual reminders | User-defined | Site only | Future Site functionality | NOT WORTH COLLECTING / not game data | No plugin layout, widget, task ID, preset, recurrence or manual task storage | — / Site-owned |

## Exact roulette catalog

| ContentRoulette ID | Name in inspected English catalog | Completion index |
| --- | --- | --- |
| 1 | Duty Roulette: Leveling | 0 |
| 2 | Duty Roulette: High-level Dungeons | 1 |
| 3 | Duty Roulette: Main Scenario | 2 |
| 4 | Duty Roulette: Guildhests | 3 |
| 5 | Duty Roulette: Expert | 4 |
| 6 | Duty Roulette: Trials | 5 |
| 7 | Daily Challenge: Frontline | 6 |
| 8 | Duty Roulette: Level Cap Dungeons | 7 |
| 9 | Duty Roulette: Mentor | 8 |
| 15 | Duty Roulette: Alliance Raids | 9 |
| 17 | Duty Roulette: Normal Raids | 10 |

The catalog, not this table, selects runtime categories. SDK's raw fixed array
has length 10; the supported getter takes roulette **RowId** and is used instead.
The other named catalog rows are Chocobo Race variants 18–35 (including no-reward
and non-registration variants), and Crystalline Conflict 40/41; all have index -1.
They are not invented daily-reward checklist categories. Eligibility and role in
need stay unknown; there is no authoritative daily next-reset timestamp in this
collector. No roulette flag may auto-complete a dated Site item yet.

## Implementation and handoff

See [finite model and exact field semantics](contracts/dashboard-facts-v1.md),
[sanitized synthetic fixture](examples/dashboard-facts-v1.json), and
[Testing successor/live checklist](releases/testing-0.0.71.md).
The high-value safe subset is implemented; unsupported systems remain explicitly
manual-only. There is **no Dashboard intake contract or upload implementation**.
Joint Site integration requires an accepted authenticated contract and proved
ownership/reset-period semantics before any automatic checklist completion.

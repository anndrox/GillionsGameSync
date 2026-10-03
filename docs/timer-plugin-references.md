# GitHub timer-source audit — Testing 0.0.72

Follow-up: owner live72 evidence isolated `ClientCounterMismatch`. The earlier
reference audit proved global allowances, not the agent per-client remaining
equation. [Prepared73 diagnosis](releases/testing-0.0.73.md) documents new pinned
Umbra/HaselDebug manager-array evidence, strict manager/agent corroboration and
unsupported ambiguous agent remaining semantics. Prior publication and all
unrelated timer classifications remain unchanged.

Owner requested references for **all timer systems**, not only Custom Deliveries
and societies, and accepted reference-backed validation instead of additional
mandatory live tests. Completion means implemented in Gillions and validated
against the installed SDK plus focused regressions. It never means every cache
was live-proven, that a reference's inference is a server fact, or that an
unsupported category was implemented. Demonstrated failures still need correction.

Source pins inspected: DailyDuty `f91d7ce1868a45c9bb6220e6a67f726266bb8ddd`,
Accountant `f6184b4c35867acbb3e97e70013ac6f9dd086621`, FFXIVClientStructs
`6a562a1ef86b9acb22bda0726e28b0e23bb27552`, SimpleTweaks
`146e247d1785110ed4d9caee275ea43ecf414669`. This is source evidence, not a claim
those exact versions were tested on the owner's client. Installed SDK is
7.56.2.9136/game2026.09.15.0000.0000. No third-party runtime dependency or copied
implementation was added. DailyDuty declares AGPL-3.0-or-later; Accountant
declares Apache-2.0. Treat their source as technical references, not unlicensed
code to paste into Gillions.

## Concrete corrections

- [SimpleTweaks KeepOpen](https://github.com/Caraxi/SimpleTweaksPlugin/blob/146e247d1785110ed4d9caee275ea43ecf414669/Tweaks/UiAdjustment/KeepOpen.cs)
  maps the Timers UI to `ContentsInfo`, not the agent type `ContentsTimer`.
  Gillions 0.0.71 used the wrong addon name. 0.0.72 corrects both lifecycle
  notices and visibility, with the active AgentContentsTimer's matching AddonId.
- [DailyDuty Doman](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/DomanEnclave/DomanEnclave.cs)
  reads the manager's donated/allowance totals, not offered ReconstructionBox
  items. The earlier Gillions audit missed
  [DomanEnclaveManager](https://github.com/aers/FFXIVClientStructs/blob/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client/Game/DomanEnclaveManager.cs).
  Its installed typed IsLoaded flag and bounded State fields support the new
  local Doman observation. No reset timestamp is exposed; week/ownership proof
  remains unverified. Loaded positive cap is required, not guessed from quests.
- [Accountant delivery manager](https://github.com/Ottermandias/Accountant/blob/f6184b4c35867acbb3e97e70013ac6f9dd086621/Accountant/Manager/TimerManager.DeliveryManager.cs)
  and [DailyDuty deliveries](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/CustomDelivery/CustomDelivery.cs)
  corroborate the global native allowance getter. Gillions separately validates
  global versus client facts; invalid client detail cannot erase global state.
  The exact Ameliance rejection remains unknown until the new finite diagnostics
  are observed. Shared UI/init/update/reset gates have not been guessed away.

## Full timer-related coverage

These are categories discovered in the reference plugins and the existing
[current-game capability inventory](dashboard-capability-audit.md), not a promise
that every historical event or future expansion already has a collector.

| Category | Source-backed insight | Gillions disposition |
| --- | --- | --- |
| Roulettes | [DailyDuty roulette](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/DutyRoulette/DutyRoulette.cs): native reward flags | Existing local facts; owner observed Trials flag true; eligibility/reset proof unknown |
| Custom Deliveries | Global native remaining allowances independent of selected-client rank/counters | Existing facts corrected; persistent rejection diagnostics; Ameliance failure not yet live-resolved |
| Society allowances | [DailyDuty societies](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/TribalQuests/TribalQuests.cs): QuestManager allowance getter | Existing remaining/12 facts, actual Timers lookup corrected; not individual quest completion |
| Leve regeneration | [DailyDuty leves](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/Levequest/Levequest.cs): native allowance count | Existing count/native next regeneration; local reset arithmetic not used as evidence |
| Maps | [Accountant maps](https://github.com/Ottermandias/Accountant/blob/f6184b4c35867acbb3e97e70013ac6f9dd086621/Accountant/Manager/TimerManager.MapManager.cs): cached UIState next timestamp; DailyDuty also tracks gathering chat events | Existing cached future timestamp only; no chat-based reconstruction or request |
| Retainer ventures | [Accountant retainers](https://github.com/Ottermandias/Accountant/blob/f6184b4c35867acbb3e97e70013ac6f9dd086621/Accountant/Manager/TimerManager.RetainerManager.cs): ready manager and bounded retainer list | Reuse existing ordinary Retainer observations and compatibility gates; no duplicate collector |
| Squadron mission/training | [DailyDuty squadron](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/GrandCompanySquadron/GrandCompanySquadron.cs) combines native/event evidence with inferred start/finish | Existing native expected-finish timestamps only; not priority reward/completion claims |
| Doman Enclave | Typed loaded donated/cap and accepting-donations state | Added private display/progress facts; no inferred weekly completion/reset |
| Challenge Log | [DailyDuty Challenge Log](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/ChallengeLog/ChallengeLog.cs): native completion getter | Existing loaded completion flags; numerical progress unsupported |
| Wondrous Tails | [DailyDuty journal](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/WondrousTails/WondrousTails.cs): held/expiration/stickers/Second Chance/task status | Existing positive held-journal facts; expiration is not turned-in proof |
| Weekly tomestones/PvP | Existing installed typed earned/weekly-counter fields | Existing facts; not wallet balance/lifetime stats and no guessed reset |
| Daily/weekly Hunt Bills | DailyDuty has separate daily/weekly modules | Reuse existing corroborated Gillions Hunt collector; no duplicated source |
| Fashion Report | [DailyDuty Fashion](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/FashionReport/FashionReport.cs) reads NPC scene parameters | Not adopted: present scope excludes new native hooks; manager load/owner-response proof still needed; theme/solution excluded |
| Masked Carnivale | [DailyDuty Carnivale](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/MaskedCarnivale/MaskedCarnivale.cs) reads active-agent challenge flags/result UI | Useful candidate, but private unknown load state/catalog applicability unresolved; remains unsupported, not false/zero |
| Normal/Alliance weekly rewards | [DailyDuty raids](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/RaidsNormal/RaidsNormal.cs) combines selected-duty reward counts with inventory-category heuristics | No general weekly clear/loot flags adopted from heuristics; selected-duty load/reset applicability needs separate proof |
| GC Supply/Provisioning | [DailyDuty GC supply](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/GrandCompanySupply/GrandCompanySupply.cs) and Provision read an active agent's per-job turn-in state | Promising bounded UI candidate; IsTurnInAvailable conflates eligibility with prior delivery, so no blanket completed flag claimed |
| Faux Hollows | [DailyDuty Faux](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/FauxHollows/FauxHollows.cs) counts puzzle openings | Not proof of tell/retell completion; do not equate repeated UI opens to completed weekly duties |
| Jumbo/Mini Cactpot | [DailyDuty lottery](https://github.com/MidoriKami/DailyDuty/blob/f91d7ce1868a45c9bb6220e6a67f726266bb8ddd/DailyDuty/Features/JumboCactpot/JumboCactpot.cs) uses NPC/agent events; Accountant uses Gold Saucer hooks | No new hook, callback, UI-open count or schedule-based participation inference adopted |
| Submarines/airships | [Accountant submersibles](https://github.com/Ottermandias/Accountant/blob/f6184b4c35867acbb3e97e70013ac6f9dd086621/Accountant/Manager/TimerManager.SubmersibleManager.cs) and airships observe packet handlers | Existing Gillions workshop retention reused for submarine expected return/results; no new packet hooks or airship collection |
| Gardening/FC wheels | Accountant crop/wheel managers use tending/planting history or UI text/progress reconstruction | Not reliable native personal completion fields; no retained housing/FC identifiers or rendered-text scrape added |
| Island weekly favors/workshops | Existing typed manager fields promising, visitor/own-island/load-cycle ownership unresolved | Existing audit remains partial; no forced data requests or optimizer state |
| Shared FATEs/reputation/achievements/collections/ordinary currencies | Already supported ordinary Game Sync | Reuse Site's supported snapshots; permanent progression is not a reset-aware checklist fact |
| Resets/windows/server schedules | DailyDuty uses calendar helpers for daily/weekly/GC/Fashion/lottery schedules | Site may display static schedule separately; no schedule manufactures fresh character state or clears retained facts |

## Integrity and completion labels

Implemented/reference-backed: SDK-compiled and fixture-tested sources above.
Live checks may be owner-waived and must be described that way. UNKNOWN/STALE,
unsupported and manual-only categories remain explicit. No direct Site task
completion, additional uploads, public sharing, hidden requests or gameplay
actions. Ordinary pairing/sync and market consent remain independent.

Eight bounded source groups now alternate event priority with round-robin, one
group per five seconds. Worst normal turn is 40 seconds, sustained notices 80.
Retention remains 16 characters, 24 latest groups per character, 384 KiB; no
history eviction or forced reset. Older unsupported binaries must not be used as
an automatic downgrade: an unknown system may pause that older facts collector;
preserve config and request a forward correction instead.

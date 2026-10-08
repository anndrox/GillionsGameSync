"""Native-source/export boundaries; complements actual SDK/config and policy tests."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
source = (root / "DashboardLocalView.cs").read_text(encoding="utf-8")
model = (root / "DashboardFacts.cs").read_text(encoding="utf-8")
plugin = (root / "Plugin.cs").read_text(encoding="utf-8")
assert source.startswith("#if GILLIONS_TEST_BUILD")
assert "framework.Update" not in source
assert not re.search(r"HttpClient|SendAsync|RequestResetTimestamps\(|RequestFavorData\(|RequestData\(|HookFrom|FireCallback|ReceiveEvent|ObjectTable|GetIpcSubscriber|QueueRoulette\(|QueueDuties\(|OpenRouletteDuty\(|\.Refresh\(", source)
tick = source.split("internal void Tick", 1)[1].split("private void ResetEvidence", 1)[0]
assert tick.index("schedule.Begin") < tick.index("!Compatible()") < tick.index("CurrentKey()") < tick.index("Read(group.Value")
assert "!framework.IsInFrameworkUpdateThread" in tick and "policy.Supported" in tick
notice = source.split("private void OnAddon", 1)[1].split("private unsafe bool Visible", 1)[0]
assert "schedule.Notice" in notice and "Instance()" not in notice
assert "IsAgentActive()" in source and "agent->AddonId" in source
assert "note->State !=" in source and "ContentsNoteState.Loaded" in source
assert "!currency->IsLoaded" in source and "currency->Size is < 1 or > 256" in source
assert "!profile->IsLoaded" in source
assert "IsRouletteComplete((byte)id)" in source and "player->ContentRouletteCompletion" not in source
assert "r.CompletionArrayIndex >= 0" in source and "r.IsInDutyFinder" in source
assert "GetWeeklyAcquiredTomestoneCount" in source and "GetTomestoneCount" not in source
assert "GetLimitedTomestoneWeeklyLimit" in source and "cap != limitedCap" in source
assert "npc.RemainingAllowances" not in source
assert "CurrentNpcInitInProgress" in source and "NpcInfo.Valid" in source
delivery = source.split("case 1:", 1)[1].split("case 2:", 1)[0]
assert delivery.index('Observation("custom-deliveries-global"') < delivery.index("var matches")
for reason in ["AgentUnavailable", "ManagerUnavailable", "InterfaceClosed", "NpcInvalid", "NpcUninitialized", "AddonNotUpdated", "ManagerInitializing", "ManagerUninitialized", "ResetUnavailable", "ClientCatalogMismatch", "ClientAllowanceMismatch", "ClientRankMismatch", "ClientCounterMismatch", "ClientIndexUnavailable"]:
    assert "DashboardDeliveryReadStatus." + reason in delivery
assert "DashboardSources.AdmitCustomDeliveries(global, selected)" in delivery
assert "DashboardSources.AdmitCustomDeliveries(global, null)" in delivery
assert "r.Npc.RowId == npc.NpcId" in delivery and "checked((int)matches[0].RowId - 1)" in delivery
for field in ["UsedAllowances", "SatisfactionRanks", "Satisfaction"]:
    assert "index >= manager->" + field + ".Length" in delivery
    assert "manager->" + field + "[index]" in delivery
assert "DashboardSources.ClientAllowance" in delivery and "if (allowance is null)" in delivery
assert "Math.Clamp" not in delivery and "Math.Min" not in delivery
assert "deliveryDiagnostics.Reset()" in source and "deliveryDiagnostics.Text" in source
assert "ImGui.TextWrapped(state.DeliveryStatus)" in source and "{state.DeliveryStatus}\\nAttempts" in source
assert "NpcId" not in model.split("internal sealed class DashboardDeliveryDiagnostics", 1)[1].split("internal sealed class DashboardSchedule", 1)[0]
assert "GetResetDateTime" in source and "NextChallengeLogResetTimestamp" in source
assert "RequestResetTimestamps" not in source
assert "if (!player->HasWeeklyBingoJournal) return []" in source
assert "catalog.HasRow(order)" in source and "GetWeeklyBingoTaskStatus(index)" in source
assert "GetNextLeveAllowancesUnixTimestamp" in source and "ToLocalTime" not in source
assert '"ContentsTimer"' not in source and '"ContentsInfo" => 5' in source
timers = source.split("case 5:", 1)[1].split("case 6:", 1)[0]
assert "AgentContentsTimer.Instance()" in timers and "!timer->IsAgentActive()" in timers
assert 'Visible("ContentsInfo", timer->AddonId)' in timers
assert '"ContentsInfo"' in source.split("private static readonly string[] Addons", 1)[1].split(";", 1)[0]
doman = source.split("case 7:", 1)[1].split("default:", 1)[0]
assert "DomanEnclaveManager.Instance()" in doman and "!doma->IsLoaded" in doman
assert "state.Allowance == 0" in doman and "state.Donated > state.Allowance" in doman
assert "state.IsAcceptingDonations" in doman and "Completed:" not in doman
assert 'Observation("doman-enclave-weekly"' in doman
assert "client.Logout -= OnLogout" in source and "client.Login -= OnLogin" in source and "client.TerritoryChanged -= OnTerritoryChanged" in source
assert "dashboardLocal.Tick(now);" in plugin and "dashboardLocal.Dispose();" in plugin
assert plugin.index("dashboardLocal.Tick(now);") < plugin.index("if (!HasPairedSession || activeOwnedState is null || !clientState.IsLoggedIn) return;")
copy = source.split('ImGui.Button("Copy PRIVATE facts JSON', 1)[1].split('ImGui.Button("Copy aggregate', 1)[0]
assert "CurrentKey()" in copy and "key != activeKey" in copy and "policy.Export" in copy
draw = source.split("private void Draw()", 1)[1]
assert "GetExcelSheet" not in draw and "Read(group" not in draw
assert "ImGui.TextUnformatted(row)" in draw
scopes = re.search(r"SyncScopes = \[(.*?)\];", plugin).group(1)
assert "dashboard" not in scopes and "weekly" not in scopes
assert "not-authorized-by-this-experimental-export" in model and "local-only-no-server-contract" in model
assert "MaximumBytes = 384 * 1024" in model and "MaximumCharacters = 16" in model
assert "JsonExtensionData" in model and "UnsupportedMembers" in model
actual = json.loads((root / "artifacts/verification/dashboard/dashboard-facts-v1.json").read_text())
example = json.loads((root / "docs/examples/dashboard-facts-v1.json").read_text())
assert actual == example
assert actual["privacy"] == "private-personal-activity"
assert {r["system"] for r in actual["observations"]} == {
    "roulette-reward", "custom-deliveries-global", "custom-deliveries-client", "challenge-log", "weekly-tomestones",
    "wondrous-tails", "leve-allowance", "society-allowance", "map-availability", "squadron-mission", "squadron-training",
    "frontline-weekly", "rival-wings-weekly", "doman-enclave-weekly"
}
for observation in actual["observations"]:
    assert observation["gameVersion"] == "synthetic-game" and observation["nativeVersion"] == "synthetic-sdk"
    assert not {"characterId", "contentId", "localCharacterKey", "name", "account", "token", "dashboardId", "taskId"}.intersection(observation)
print("Dashboard Testing-only/read-only/source admission/session/private-export/no-upload boundaries passed.")

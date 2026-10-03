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
assert "npc.RemainingAllowances != npc.MaxAllowances - npc.UsedAllowances" in source
assert "CurrentNpcInitInProgress" in source and "NpcInfo.Valid" in source
assert "GetResetDateTime" in source and "NextChallengeLogResetTimestamp" in source
assert "RequestResetTimestamps" not in source
assert "if (!player->HasWeeklyBingoJournal) return []" in source
assert "catalog.HasRow(order)" in source and "GetWeeklyBingoTaskStatus(index)" in source
assert "GetNextLeveAllowancesUnixTimestamp" in source and "ToLocalTime" not in source
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
    "frontline-weekly", "rival-wings-weekly"
}
for observation in actual["observations"]:
    assert observation["gameVersion"] == "synthetic-game" and observation["nativeVersion"] == "synthetic-sdk"
    assert not {"characterId", "contentId", "localCharacterKey", "name", "account", "token", "dashboardId", "taskId"}.intersection(observation)
print("Dashboard Testing-only/read-only/source admission/session/private-export/no-upload boundaries passed.")

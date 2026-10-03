"""Read-only/source boundaries complement managed tests and SDK compilation."""
from pathlib import Path
import re
import json

root = Path(__file__).resolve().parents[1]
hunt = (root / "HuntBillLocalView.cs").read_text(encoding="utf-8")
sub = (root / "SubmarineLocalView.cs").read_text(encoding="utf-8")
plugin = (root / "Plugin.cs").read_text(encoding="utf-8")
for text in (hunt, sub):
    assert text.startswith("#if GILLIONS_TEST_BUILD")
    assert "framework.Update" not in text
    assert not re.search(r"HttpClient|SendAsync|RequestData|HookFrom|FireCallback|ReceiveEvent|GetIpcSubscriber|ObjectTable", text)
    event = text.split("private unsafe void OnAddon" if text is sub else "internal unsafe void Tick", 1)[1]
    assert event.index("PersonalObservationCompatibility.Supports") < event.index("PlayerState.Instance()" if text is sub else "CurrentCharacterKey()")
    assert "!framework.IsInFrameworkUpdateThread" in event
    assert "LocalRetentionEnabled" in event
    assert "client.Logout -= OnLogout" in text
    assert "PRIVATE" in text and "no upload" in text.lower()
assert "nextReadUtc" in sub and "AddSeconds(1)" in sub
assert "RegisterListener" not in hunt and "args.Addon" not in hunt
tick = hunt.split("internal unsafe void Tick", 1)[1]
assert tick.index("schedule.TryBegin") < tick.index("PersonalObservationCompatibility.Supports") < tick.index("InventoryManager.Instance()")
assert "InventoryType.KeyItems" in hunt and "!keyItems->IsLoaded" in hunt
assert "keyItems->Size is < 1 or > 256" in hunt and "item.Quantity > 0" in hunt
assert "presentItems.Contains(type.EventItem.RowId)" in hunt and "HuntObservationAdmission.CanUseBillCache" in hunt
assert "huntLocal.Tick(now);" in plugin
assert plugin.index("huntLocal.Tick(now);") < plugin.index("if (!HasPairedSession || activeOwnedState is null || !clientState.IsLoggedIn) return;")
assert "ObtainedFlags" in hunt and "GetObtainedHuntOrderRowId(index)" in hunt
assert "CurrentKills[index].Counts" in hunt and "CatalogTargets" in hunt
assert "orders.HasRow(orderId)" in hunt and "catalog.HasRow(row.Target.RowId)" in hunt
assert "presentation = new(id =>" in hunt and "sheet.GetRow(id).Singular.ExtractText()" in hunt
assert "b.Targets.OrderBy(t => t.TargetIndex).Select(presentation.TargetLine)" in hunt
draw = hunt.split("private void Draw()", 1)[1]
assert "GetExcelSheet" not in draw and "presentation.TargetLine" not in draw
assert "ImGui.PushTextWrapPos(0)" in draw and "ImGui.PopTextWrapPos()" in draw
assert "foreach (var row in state.Rows) ImGui.TextUnformatted(row)" in draw
assert "if ((hunt->ObtainedFlags & (1 << index)) == 0) continue" in hunt
assert "characterAssociation =" in (root / "HuntBills.cs").read_text()
assert "IsSubmarineExplorationUnlocked" in sub and "IsSubmarineExplorationExplored" in sub
assert sub.index("planning->AddonId == args.Addon.Id") < sub.index("HousingManager.IsSubmarineExplorationUnlocked")
assert "huntLocal.Dispose();" in plugin
assert 'SyncScopes = ["inventory"' in plugin and '"hunt_bills"' not in plugin and '"submarine_personal"' not in plugin
model = (root / "SubmarineVoyages.cs").read_text()
community = model.split("internal string PrepareExport()", 1)[1]
assert "LocalWorkshopKey" not in community and "workshopScope" not in community
assert "submarine-personal-v1" in model and "positive-observations-only" in model
assert "MaximumSnapshotBytes = 4096" in model
for example, fixture, array in [
    ("hunt-bills-v1.json", "personal-state/hunt-bills-v1.json", "bills"),
    ("submarine-personal-v1.json", "submarine-policy/submarine-personal-v1.json", "slots"),
]:
    document = json.loads((root / "docs/examples" / example).read_text())
    actual = json.loads((root / "artifacts/verification" / fixture).read_text())
    assert set(document) == set(actual), example
    assert set(document[array][0]) == set(actual[array][0]), example
    assert document["uploadState"] == "local-only-no-server-contract"
    assert document[array][0]["gameVersion"] == "synthetic-game"
print("Hunt/submarine Testing-only, opt-in, exact-patch, bounded observation, private export and no-upload source boundaries passed.")

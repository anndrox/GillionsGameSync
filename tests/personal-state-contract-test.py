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
assert "hunt->GetKillCount(index, t.TargetIndex)" in hunt and "!c.Valid(catalog[n].RequiredKills)" in hunt
assert "GetAvailableHuntOrderRowId(index)" in hunt
assert "chat.LogMessage += OnProgressMessage" in hunt and "chat.LogMessage -= OnProgressMessage" in hunt
progress = hunt.split("private void OnProgressMessage",1)[1].split("private void OnTerritoryChanged",1)[0]
assert "!store.LocalRetentionEnabled" in progress and "!framework.IsInFrameworkUpdateThread" in progress
assert "message.LogMessageId != HuntProgressMessage.LogId" in progress and "message.ParameterCount != 4" in progress
assert all(f"TryGetIntParameter({n}," in progress for n in (1,2,3))
assert not re.search("TryGetStringParameter|SourceEntity|TargetEntity|FormatLogMessage|ChatMessage|PreventOriginal|Address", progress)
assert "session.Apply(observation" in hunt and "session.Record(nativeObservation)" in hunt
assert "raw=" in hunt and "accessor=" in hunt and "retained-before=" in hunt
assert "orders.HasRow(orderId)" in hunt and "catalog.HasRow(row.Target.RowId)" in hunt
assert "presentation = new(id =>" in hunt and "sheet.GetRow(id).Singular.ExtractText()" in hunt
assert "b.Targets.OrderBy(t => t.TargetIndex).Select(presentation.TargetLine)" in hunt
draw = hunt.split("private void Draw()", 1)[1]
assert "GetExcelSheet" not in draw and "presentation.TargetLine" not in draw
assert "ImGui.PushTextWrapPos(0)" in draw and "ImGui.PopTextWrapPos()" in draw
assert "foreach (var row in state.Rows) ImGui.TextUnformatted(row)" in draw
assert "!session.MayReadFinal(index, now)" in hunt and "session.CanObserveFinal(observation)" in hunt
assert "corroborated ? HuntObservationAdmission.KeyItemEvidence : null" in hunt
assert "session.Reset()" in hunt and "session.Bind(characterKey)" in hunt
for event in ("client.TerritoryChanged", "conditions.ConditionChange"):
    assert event + " +=" in hunt and event + " -=" in hunt
assert "if (value && flag is ConditionFlag.BetweenAreas or ConditionFlag.BetweenAreas51) ClearLiveBaseline()" in hunt
assert "TickPersonalSync(now, prompt: huntChanged)" in plugin
manual = plugin.split("private async Task SyncAsync",1)[1].split("var snapshots = captured.Snapshots",1)[0]
assert "PersonalEnabled(\"hunt_bills\")" in manual and "huntLocal.Tick(DateTime.UtcNow, force: true)" in manual
assert "TickPersonalSync(DateTime.UtcNow, prompt: true)" in manual
assert "cancellation.CancelAfter(TimeSpan.FromSeconds(30))" in plugin
disposition = plugin.split("var disposition = PersonalSyncPolicy.Disposition",1)[1].split("RequestConfigurationSave();",1)[0]
assert "featureToken.IsCancellationRequested" in disposition and "permit.Cancellation.IsCancellationRequested" in disposition
assert "token.IsCancellationRequested" not in disposition
assert "characterAssociation =" in (root / "HuntBills.cs").read_text()
assert "IsSubmarineExplorationUnlocked" in sub and "IsSubmarineExplorationExplored" in sub
assert sub.index("planning->AddonId == args.Addon.Id") < sub.index("HousingManager.IsSubmarineExplorationUnlocked")
assert "huntLocal.Dispose();" in plugin
assert 'SyncScopes = ["inventory"' in plugin
scopes = plugin.split('SyncScopes = [', 1)[1].split('];', 1)[0]
assert 'hunt_bills' not in scopes and 'submarine_personal' not in scopes
assert 'public bool SyncPersonalHunts { get; set; }' in plugin
assert 'public bool SyncPersonalSubmarines { get; set; }' in plugin
assert 'X-Gillions-Personal-Contract' in plugin and 'personalHttp.SendAsync' in plugin
assert 'permit.Origin != PersonalSyncPolicy.Origin' in plugin
assert 'PersonalEnabled(prepared.Resource)' in plugin and 'token.ThrowIfCancellationRequested()' in plugin
assert 'PersistBeforeSend(() => FlushConfigurationSave(force: true))' in plugin
assert '(force || savePolicy.ShouldSave(now))' in plugin
assert plugin.count('configuration.Save(pluginInterface);') == 1
assert plugin.index('var terminal = PersonalSyncPolicy.TerminalStatus') < plugin.index('var receipt = await PersonalSyncPolicy.ReadReceiptAsync')
assert 'PersonalSyncPolicy.Owner(configuration.ActiveSession.Generation, HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId))' in plugin
assert 'transportCharacter == contentId' in sub and 'new SubmarineVoyageRetention { LocalRetentionEnabled = true }' in sub
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
    if array == "slots":
        for field in ["orderedSectorIds", "plannedSectorIds", "unlockedSectorIds", "exploredSectorIds"]:
            assert actual[array][0][field] is None or isinstance(actual[array][0][field], list), field
print("Hunt/submarine Testing-only, independent opt-ins, exact-patch, bounded observation, isolated authenticated personal transport boundaries passed.")

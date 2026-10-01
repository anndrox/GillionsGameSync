"""Source boundaries complement managed fixtures and actual SDK compilation."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
native = (root / "SubmarineLocalView.cs").read_text(encoding="utf-8")
model = (root / "SubmarineVoyages.cs").read_text(encoding="utf-8")
plugin = (root / "Plugin.cs").read_text(encoding="utf-8")
assert native.startswith("#if GILLIONS_TEST_BUILD")
assert model.startswith("#if GILLIONS_TEST_BUILD || GILLIONS_SUBMARINE_TESTS")
assert "lifecycle.RegisterListener" in native and "lifecycle.UnregisterListener" in native
assert "framework.Update" not in native
assert not re.search(r"HttpClient|SendAsync|HookFrom|ReceiveEvent|FireCallback|RequestData|Dispatch|Repair\(|Recall\(|ContentId|FreeCompanyId", native)
for guard in ["!store.LocalRetentionEnabled", "!framework.IsInFrameworkUpdateThread", "!client.IsLoggedIn",
              "!player->IsLoaded", "housing->CurrentTerritory !=", "WorkshopTerritory->IsLoaded()", "!args.Addon.IsVisible",
              "sub->Parent !=", "sub->RegisterTime == 0", "results->IsAgentActive()", "results->AddonId == args.Addon.Id"]:
    assert guard in native, guard
assert "ItemHQPrimary" in native and "ItemHQAdditional" in native and "row.Point" in native
assert "sector-aggregate-mismatch" in native and "if (result->ItemReturnListCount > 10)" in native
assert "VoyageBuild = inFlight ? snapshot.Build : null" in model
assert "candidate.ResultTimeBuild ??=" in model
assert "MaximumRecords = 400" in model and "MaximumRetainedBytes = 4 * 1024 * 1024" in model
assert not re.search(r"RemoveAt|RemoveRange|Clear\(\)|TakeLast", model)
assert "departureAtUtc = (DateTime?)null" in model
assert "ContributionConsentAtResult" in model and '"local-only-no-server-contract"' in model
assert "submarineLocal.Dispose();" in plugin and "configuration.SubmarineVoyages ??= new();" in plugin
print("Submarine testing-only, read-only event, loaded-state, bounded persistence and no-upload source contracts passed.")

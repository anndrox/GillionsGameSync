"""Source guards supplement actual SDK adapter and configuration fixtures."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = (root / "MarketContributionSource.cs").read_text(encoding="utf-8")
core = (root / "MarketContributionCore.cs").read_text(encoding="utf-8")
plugin = (root / "Plugin.cs").read_text(encoding="utf-8")
assert source.startswith("#if GILLIONS_TEST_BUILD") and core.startswith("#if GILLIONS_TEST_BUILD ||")
assert "market.OfferingsReceived += Listings" in source and "market.HistoryReceived += History" in source
assert "market.OfferingsReceived -= Listings" in source and "market.HistoryReceived -= History" in source
assert not re.search(r"BuyerName|RetainerName|RetainerId|ArtisanId|ContentId|PlayerName|Hook|SendRequest|SearchItem|Universalis", source)
assert not re.search(r"HttpClient|GameNetwork|ReceiveEvent|FireCallback|framework.Update|Task.Run", source)
assert 'completeness = "partial"' in core and "sourceSnapshotAtUtc = (DateTime?)null" in core
assert "MaximumPending = 64" in core and "attempts.Count >= 6" in core and "row.Failures < 2" in core
assert "ReadBounded" in core and "AllowAutoRedirect = false" in (root / "PartyFinderContributionCore.cs").read_text()
assert "UseCookies = false" in (root / "PartyFinderContributionCore.cs").read_text()
assert "private readonly HttpClient marketHttp = PartyFinderHttp.CreateClient();" in plugin
assert "public bool ContributeObservedMarketData { get; set; } = true;" in plugin
capture = plugin.split("private MarketContributionSession? CaptureMarketSession()", 1)[1].split("private bool PermitIsCurrent", 1)[0]
for guard in ["!framework.IsInFrameworkUpdateThread", "CurrentWorld.RowId", "ConditionFlag.BetweenAreas", "marketAcceptedGeneration", "RequirePermit(permit)", "!configuration.ContributeObservedMarketData", "permit.Token", "permit.Origin"]:
    assert guard in capture, guard
assert "now.AddMilliseconds(250)" in plugin and "marketSource.Dispose(); marketContributor.Dispose(); marketHttp.Dispose();" in plugin
assert 'X-Gillions-Market-Contract' in plugin and 'MarketContributor.Compatible(responseJson)' in plugin
assert "GillionsMarketBlockedGeneration" in plugin and "receivedAuthorizationDenial: true" in capture
ordinary_update = plugin.split("private void UpdateOwnedState()", 1)[1].split("private ", 1)[0]
ordinary_permit = plugin.split("private bool PermitIsCurrent(", 1)[1].split("private void RequirePermit", 1)[0]
assert "ContributeObservedMarketData" not in ordinary_update + ordinary_permit
print("Passive market testing-only/source/privacy/origin/consent/compatibility/bounded-maintenance contracts passed.")

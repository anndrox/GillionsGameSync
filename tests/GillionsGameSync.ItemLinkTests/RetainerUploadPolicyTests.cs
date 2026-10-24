using System.Text.Json;
using GillionsGameSync;

// Real payload builder, synthetic reads: a repeated identical roster read
// refreshes observation times but is not a Retainer change worth uploading.
internal static class RetainerUploadPolicyTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Retainer upload policy: " + why); }
        var t0 = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var character = new { name = "Fixture", world = "Test" };
        string Raw(RetainerVentureLocalState state) => JsonSerializer.Serialize(RetainerVentureSnapshotPolicy.BuildPayload(state, character));
        string Semantic(RetainerVentureLocalState state) {
            using var document = JsonDocument.Parse(Raw(state));
            return RetainerUploadPolicy.SemanticHash(document.RootElement);
        }
        RetainerVentureRosterRead Roster(DateTime at, byte level = 50) => new(at, true, [new("100", "Fixture", 10, level, 0, 0, 1000)]);

        var state = new RetainerVentureLocalState { CharacterContentId = "111" };
        RetainerVentureSnapshotPolicy.MergeRoster(state, Roster(t0), t0);
        var raw0 = Raw(state); var semantic0 = Semantic(state);
        RetainerVentureSnapshotPolicy.MergeRoster(state, Roster(t0.AddSeconds(30)), t0.AddSeconds(30));
        Check(Raw(state) != raw0, "fixture really refreshes observation times (the old 30-second upload cause)");
        Check(Semantic(state) == semantic0, "an identical roster read is not a content change");
        RetainerVentureSnapshotPolicy.MergeRoster(state, Roster(t0.AddSeconds(60), level: 51), t0.AddSeconds(60));
        Check(Semantic(state) != semantic0, "a level change is a content change");

        using (var a = JsonDocument.Parse("{\"x\":1,\"lastObservedAtUtc\":\"2026-01-01T00:00:00Z\",\"inner\":[{\"observedAtUtc\":\"a\",\"v\":2}]}"))
        using (var b = JsonDocument.Parse("{\"inner\":[{\"v\":2,\"observedAtUtc\":\"b\"}],\"lastObservedAtUtc\":\"2026-02-02T00:00:00Z\",\"x\":1}"))
        using (var c = JsonDocument.Parse("{\"x\":1,\"lastChangedAtUtc\":\"2026-01-01T00:00:00Z\",\"inner\":[{\"v\":2}]}"))
        using (var d = JsonDocument.Parse("{\"x\":1,\"lastChangedAtUtc\":\"2026-03-03T00:00:00Z\",\"inner\":[{\"v\":2}]}")) {
            Check(RetainerUploadPolicy.SemanticHash(a.RootElement) == RetainerUploadPolicy.SemanticHash(b.RootElement), "only observation times and ordering differ");
            Check(RetainerUploadPolicy.SemanticHash(c.RootElement) != RetainerUploadPolicy.SemanticHash(d.RootElement), "lastChangedAtUtc moves only with a value, so it stays semantic");
        }

        var h = "A"; var now = t0.AddMinutes(10);
        Check(!RetainerUploadPolicy.ShouldUpload(false, 0, h, h, now.AddMinutes(-1), now), "unchanged within five minutes is skipped");
        Check(RetainerUploadPolicy.ShouldUpload(false, 0, h, h, now.AddMinutes(-5), now), "unchanged refreshes freshness after five minutes");
        Check(RetainerUploadPolicy.ShouldUpload(false, 0, "B", h, now.AddSeconds(-1), now), "a content change uploads immediately");
        Check(RetainerUploadPolicy.ShouldUpload(false, 1, h, h, now.AddSeconds(-1), now), "pending venture results always upload");
        Check(RetainerUploadPolicy.ShouldUpload(true, 0, h, h, now.AddSeconds(-1), now), "Sync now always uploads");
        Check(RetainerUploadPolicy.ShouldUpload(false, 0, h, null, DateTime.MinValue, now), "first upload of a session");
        Check(RetainerUploadPolicy.ShouldUpload(false, 0, h, h, DateTime.MinValue, now), "restart or character change refreshes once");
        // 30-second automatic cadence over one hour with no change: 12 uploads instead of 120.
        var uploads = 0; var last = DateTime.MinValue; string? lastHash = null;
        for (var tick = 0; tick < 120; tick++) {
            var at = t0.AddSeconds(30 * tick);
            if (RetainerUploadPolicy.ShouldUpload(false, 0, h, lastHash, last, at)) { uploads++; last = at; lastHash = h; }
        }
        Check(uploads == 12, "an unchanged hour uploads 12 times, not 120 (was " + uploads + ")");
        Console.WriteLine($"Retainer upload cadence (content change, results, 5-minute freshness) PASS: {checks}");
    }
}

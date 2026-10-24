using System.Net;
using GillionsGameSync;

// Each feature row names its real condition: an unsupported game, a Site that has
// not admitted it, a decision to stay Off, waiting for data, or a reachable failure.
internal static class StatusAccuracyTests {
    internal static async Task RunAsync() {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Status accuracy: " + why); }

        // FATE: local qualification, Site admission and consent are distinct.
        Check(PublicHealth.FateState(false, true, null, null, null, false, true) == PublicHealth.UnsupportedGame, "unqualified game is not a client update");
        Check(PublicHealth.FateState(true, false, null, null, null, false, true) == "Waiting for supported context", "unbound (logged out / first presence) waits");
        Check(PublicHealth.FateState(true, true, "FATE_SOURCE_UNSUPPORTED", false, true, false, true) == "Not available on Gillions", "Site not admitting the source is not a version mismatch");
        Check(PublicHealth.FateState(true, true, "TESTING_PRODUCT_REQUIRED", false, true, false, true) == "Not available on Gillions", "product refusal");
        Check(PublicHealth.FateState(true, true, "FATE_SCHEMA_UNSUPPORTED", false, true, false, true) == "Update required", "only a schema refusal asks for an update");
        Check(PublicHealth.FateState(true, true, "FATE_POLICY_REQUIRED", false, false, false, true) == "Off on Gillions", "Site policy off");
        Check(PublicHealth.FateState(true, true, "DEVICE_INVALID", false, true, false, true) == "Authorization expired or revoked", "device refusal");
        Check(PublicHealth.FateState(true, true, null, true, true, false, true) == "Temporarily unavailable", "granted but suspended is not Ready");
        Check(PublicHealth.FateState(true, true, null, true, true, true, true) == "Ready", "granted and authorized");
        Check(PublicHealth.FateState(true, true, null, true, true, true, false) == "Waiting for supported context", "granted but outside a supported area is not Ready");
        foreach (var code in new[] { "DEVICE_REVOKED", "TOKEN_INVALID" })
            Check(PublicHealth.FateState(true, true, code, false, true, false, true) == "Authorization expired or revoked", "device refusal " + code);

        // Contribution rows (Market, Party Finder).
        string Contribution(bool site = true, bool loggedIn = true, bool fresh = true, string decision = "explicit", bool allowed = true, bool accepted = true, bool stopped = false)
            => PublicHealth.ContributionState(site, loggedIn, fresh, decision, allowed, accepted, stopped);
        Check(Contribution() == "Ready", "enabled and accepted");
        Check(Contribution(decision: "legacy", allowed: false) == "Off", "a legacy/local opt-out is Off, not unavailable");
        Check(Contribution(allowed: false) == "Off on Gillions", "an explicit Site decision");
        Check(Contribution(loggedIn: false) == "Waiting for supported context", "explicit ON while logged out is not Off on Gillions");
        // Logout invalidates the lease, so the real logged-out state is also not fresh.
        Check(Contribution(loggedIn: false, fresh: false, decision: "unavailable", allowed: false) == "Waiting for supported context", "logged out waits rather than reporting a failure");
        Check(Contribution(decision: "invalid", allowed: false) == "Not available on Gillions", "an unreadable Site decision is not a local opt-out");
        Check(Contribution(accepted: false) == "Not available on Gillions", "Site did not acknowledge this product");
        Check(Contribution(stopped: true) == "Not available on Gillions", "a Site denial is never Ready");
        Check(Contribution(fresh: false) == "Temporarily unavailable", "no fresh permission lease");
        Check(Contribution(site: false) == "Not available on Gillions", "an unsupported destination");

        // Hunts.
        Check(PublicHealth.HuntState(false, true, true) == PublicHealth.UnsupportedGame, "unqualified game");
        Check(PublicHealth.HuntState(true, false, false) == "Off", "Hunt Progress and private Hunts both off");
        Check(PublicHealth.HuntState(true, true, false) == "Waiting for supported context", "native source not observed yet");
        Check(PublicHealth.HuntState(true, true, true) == "Ready", "fresh observation");

        // Every new label survives the support summary whitelist.
        foreach (var label in new[] { "Off", "Not available on Gillions", PublicHealth.UnsupportedGame }) {
            var summary = new PublicHealth("Connected", "c", label, label, label, label, null, "Testing", "0.0.91.0", "2026.10.16.0000.0000").SupportSummary();
            Check(summary.Contains("Hunts: " + label) && summary.Contains("Market: " + label), "support summary keeps " + label);
        }

        // Only requests that reached (or failed to reach) Gillions are connection states.
        foreach (var stage in new[] { "snapshot_http", "snapshot_status", "snapshot_receipt", "gil_http", "gil_status", "gil_receipt" })
            Check(SyncFailureDiagnostic.IsTransportStage(stage), "transport stage " + stage);
        foreach (var stage in new[] { "capture_scheduled", "capture_native", "prepare_snapshots", "snapshot_request", "gil_prepare", "snapshot_commit", "final_commit" })
            Check(!SyncFailureDiagnostic.IsTransportStage(stage), "local stage " + stage + " is not Gillions unavailable");

        // Authorization and version refusals keep their code instead of "Gillions unavailable".
        foreach (var (status, code) in new[] { (HttpStatusCode.Unauthorized, "DEVICE_INVALID"), (HttpStatusCode.Forbidden, "CLIENT_VERSION_UNSUPPORTED") }) {
            using var response = new HttpResponseMessage(status) { Content = new StringContent("{\"code\":\"" + code + "\",\"error\":\"SYNTHETIC_SECRET\"}") };
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(response); throw new Exception("Expected rejection."); }
            catch (GillionsSyncRejectedException error) { Check(error.Code == code && !error.Message.Contains("SYNTHETIC_SECRET"), "typed refusal " + code); }
        }
        using (var server = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"code\":\"DEVICE_INVALID\"}") }) {
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(server); throw new Exception("Expected rejection."); }
            catch (InvalidOperationException error) { Check(error is not GillionsSyncRejectedException, "a 5xx is an unavailable Gillions whatever its body"); }
        }

        // A failed save keeps its changes and backs off instead of re-serializing every frame.
        // Site refusals are classified, never shown as an unreachable Gillions, and
        // the Site's text only classifies: it never reaches the exception message.
        const string notLinkedText = "This in-game character is not linked to your Gillions account. Add the character in Preferences before syncing it.";
        const string mismatchText = "The observed character does not match this paired Gillions character.";
        Check(SiteRefusalPolicy.Classify("INVALID_GAME_SYNC_REQUEST", notLinkedText) == SiteRefusalPolicy.CharacterNotLinked, "deployed not-linked text");
        Check(SiteRefusalPolicy.Classify("CHARACTER_NOT_LINKED", "") == SiteRefusalPolicy.CharacterNotLinked
            && SiteRefusalPolicy.Classify("character_not_found", "") == SiteRefusalPolicy.CharacterNotLinked, "not-linked codes");
        Check(SiteRefusalPolicy.Classify("character_mismatch", mismatchText) == SiteRefusalPolicy.CharacterMismatch, "paired-character mismatch");
        Check(SiteRefusalPolicy.Classify("invalid_retainer_request", "Retainer presence client identity does not match the authenticated product.") == SiteRefusalPolicy.ProductMismatch
            && SiteRefusalPolicy.Classify("CLIENT_PRODUCT_MISMATCH", "") == SiteRefusalPolicy.ProductMismatch, "product mismatch text and code");
        foreach (var odd in new[] { "", "invalid_retainer_request", "BAD CODE", "X<script>", new string('A', 80) })
            Check(SiteRefusalPolicy.Classify(odd, "anything") == SiteRefusalPolicy.Rejected, "unrecognized code is a generic refusal: " + odd);
        Check(SiteRefusalPolicy.IsRefusal(400) && SiteRefusalPolicy.IsRefusal(403) && SiteRefusalPolicy.IsRefusal(404)
            && !SiteRefusalPolicy.IsRefusal(408) && !SiteRefusalPolicy.IsRefusal(429) && !SiteRefusalPolicy.IsRefusal(500) && !SiteRefusalPolicy.IsRefusal(503),
            "timeouts, rate limits and server errors stay retryable");
        string Body(string code, string error) => System.Text.Json.JsonSerializer.Serialize(new { ok = false, code, error });
        foreach (var (status, body, expected) in new[] {
            (HttpStatusCode.BadRequest, Body("INVALID_GAME_SYNC_REQUEST", notLinkedText), SiteRefusalPolicy.CharacterNotLinked),
            (HttpStatusCode.Forbidden, Body("character_mismatch", mismatchText), SiteRefusalPolicy.CharacterMismatch),
            (HttpStatusCode.BadRequest, Body("invalid_retainer_request", "SYNTHETIC_SECRET"), SiteRefusalPolicy.Rejected),
            (HttpStatusCode.BadRequest, "not json", SiteRefusalPolicy.Rejected) }) {
            using var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(response); throw new Exception("Expected rejection."); }
            catch (GillionsSyncRejectedException error) {
                Check(error.Code == expected && !error.Message.Contains("linked") && !error.Message.Contains("SYNTHETIC_SECRET") && !error.Message.Contains("observed"), "typed refusal " + expected);
                var diagnostic = SyncFailureDiagnostic.Describe(error, "snapshot_status", "inventory", (int)status);
                Check(diagnostic.Contains("code=" + expected) && !diagnostic.Contains("SYNTHETIC_SECRET") && !diagnostic.Contains("linked"), "diagnostic logs the code, never the Site text");
            }
        }
        foreach (var status in new[] { HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway }) {
            using var response = new HttpResponseMessage(status) { Content = new StringContent(Body("character_mismatch", mismatchText)) };
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(response); throw new Exception("Expected rejection."); }
            catch (InvalidOperationException error) { Check(error is not GillionsSyncRejectedException, "retryable status stays untyped: " + (int)status); }
        }
        Check(PublicHealth.ConnectionState(true, false, SiteRefusalPolicy.CharacterNotLinked) == PublicHealth.CharacterNotLinked
            && PublicHealth.ConnectionState(true, false, SiteRefusalPolicy.CharacterMismatch) == PublicHealth.OtherCharacter
            && PublicHealth.ConnectionState(true, false, SiteRefusalPolicy.ProductMismatch) == PublicHealth.ReconnectRequired
            && PublicHealth.ConnectionState(true, false, SiteRefusalPolicy.Rejected) == PublicHealth.Refused, "each refusal has its own connection state");
        Check(PublicHealth.ConnectionState(false, false, SiteRefusalPolicy.Rejected) == "Not connected"
            && PublicHealth.ConnectionState(true, true, SiteRefusalPolicy.CharacterMismatch) == "Connecting", "unpaired and pairing still win");
        foreach (var label in new[] { PublicHealth.CharacterNotLinked, PublicHealth.OtherCharacter, PublicHealth.ReconnectRequired, PublicHealth.Refused })
            Check(new PublicHealth(label, "c", "Ready", "Ready", "Ready", "Ready", null, "Testing", "0.0.92.0", "2026.10.16.0000.0000").SupportSummary().Contains("Connection: " + label),
                "support summary keeps " + label);

        var now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var save = new ObservationSavePolicy();
        save.RequestDurable();
        Check(save.ShouldSave(now) && save.Failed(now), "first failure is reported once");
        Check(!save.ShouldSave(now.AddSeconds(1)) && save.ShouldSave(now.AddSeconds(5)), "retry waits five seconds");
        Check(!save.Failed(now.AddSeconds(5)) && !save.ShouldSave(now.AddSeconds(14)) && save.ShouldSave(now.AddSeconds(15)), "repeated failure doubles the wait, logged once");
        save.Saved(now.AddSeconds(15));
        Check(!save.ShouldSave(now.AddSeconds(16)), "a successful save clears the pending change");
        save.RequestDurable();
        Check(save.ShouldSave(now.AddSeconds(16)), "after success a durable change saves on the next tick");
        for (var i = 0; i < 10; i++) save.Failed(now);
        Check(save.ShouldSave(now.AddSeconds(60)), "backoff is capped at one minute");
        Check(!save.ShouldSave(now.AddSeconds(1)) && save.ShouldSave(now.AddSeconds(1), ignoreBackoff: true), "a Site denial or unload flush does not wait out the backoff");

        Console.WriteLine($"Status accuracy, failure classification and save backoff PASS: {checks}");
    }
}

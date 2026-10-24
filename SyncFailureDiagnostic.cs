using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

// Exception messages/Data/ToString can contain peer payloads and credentials.
// Only locally defined fixed messages and managed method metadata are admitted.
internal static class SyncFailureDiagnostic {
    internal const int MaximumLength = 3072;
    // Stages where a request was sent to Gillions or its response was read.
    internal static bool IsTransportStage(string stage) => stage.EndsWith("_http", StringComparison.Ordinal)
        || stage.EndsWith("_status", StringComparison.Ordinal) || stage.EndsWith("_receipt", StringComparison.Ordinal);
    private static readonly string[] KnownMessages = [
        "Pair and log into a character before syncing.",
        "The current character is not ready.",
        "Saved character ownership is invalid. Pair again to start a fresh connection.",
        "No player inventory is currently loaded. Log into the selected character, open Inventory once, then try sync again.",
        "Invalid Retainer receipt.", "Invalid sync receipt.", "Invalid Gil receipt.",
        "Gillions response exceeded the size limit.",
        "Pending Gil data failed validation and was preserved.",
        "Configuration persistence is unavailable after disposal."
    ];
    internal static string Describe(Exception error, string stage, string resource, int? httpStatus) {
        var text = new StringBuilder("stage=").Append(SafeLabel(stage)).Append("; resource=")
            .Append(SafeLabel(resource)).Append("; HTTP=").Append(httpStatus is >= 100 and <= 599 ? httpStatus.Value.ToString() : "not-received");
        for (var depth = 0; error is not null && depth < 3; depth++, error = error.InnerException!) {
            text.Append("; exception=").Append(SafeLabel(error.GetType().FullName ?? "unknown"))
                .Append("; message=").Append(SafeMessage(error));
            // A classified Site refusal code is a bounded identifier, never the Site's text.
            if (error is GillionsSyncRejectedException rejection) text.Append("; code=").Append(SafeLabel(rejection.Code));
            foreach (var frame in (new StackTrace(error, false).GetFrames() ?? []).Take(12)) {
                var method = frame.GetMethod();
                text.Append("\n at ").Append(SafeLabel(method?.DeclaringType?.FullName ?? "unknown"))
                    .Append('.').Append(SafeLabel(method?.Name ?? "unknown"))
                    .Append(" IL+").Append(frame.GetILOffset().ToString("X"));
            }
        }
        return text.Length <= MaximumLength ? text.ToString() : text.ToString(0, MaximumLength);
    }
    private static string SafeMessage(Exception error) {
        // Do not invoke a third-party exception's virtual Message getter.
        if (error.GetType() != typeof(InvalidOperationException) && error.GetType() != typeof(GillionsSyncRejectedException)) return "[unrecognized message withheld; method stack retained]";
        if (KnownMessages.Contains(error.Message, StringComparer.Ordinal)) return error.Message;
        if (Regex.IsMatch(error.Message,
            @"^Gillions rejected the request \(HTTP [1-5][0-9]{2}\)\.$", RegexOptions.CultureInvariant)) return error.Message;
        return "[unrecognized message withheld; method stack retained]";
    }
    private static string SafeLabel(string label) => label.Length is > 0 and <= 140
        && label.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '+' or '<' or '>' or '`') ? label : "unknown";
}

using GillionsGameSync;
using System.Runtime.CompilerServices;

internal static class SyncFailureDiagnosticTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool yes) { checks++; if (!yes) throw new Exception("Sync failure diagnostic boundary failed."); }
        Exception captured;
        try { ThrowPrivateMessage(); throw new Exception("unreachable"); } catch (InvalidOperationException error) { captured = error; }
        var text = SyncFailureDiagnostic.Describe(captured, "gil_receipt", "gil_ledger", 200);
        Check(text.Contains("stage=gil_receipt") && text.Contains("resource=gil_ledger") && text.Contains("HTTP=200"));
        Check(text.Contains("InvalidOperationException") && text.Contains(nameof(ThrowPrivateMessage)));
        Check(!text.Contains("PRIVATE") && !text.Contains("example.test") && !text.Contains("123456789"));
        captured.Data["credential"] = "PRIVATE_DATA_SECRET";
        Check(!SyncFailureDiagnostic.Describe(captured,"capture_native","character",null).Contains("PRIVATE"));
        Check(SyncFailureDiagnostic.Describe(new InvalidOperationException("Invalid Gil receipt."),"gil_receipt","gil_ledger",200).Contains("message=Invalid Gil receipt."));
        Check(SyncFailureDiagnostic.Describe(new InvalidOperationException("Gillions rejected the request (HTTP 400)."),"gil_status","gil_ledger",400).Contains("message=Gillions rejected the request (HTTP 400)."));
        Check(!SyncFailureDiagnostic.Describe(new InvalidOperationException("Gillions rejected the request (HTTP 400). PRIVATE"),"gil_status","gil_ledger",400).Contains("PRIVATE"));
        Check(SyncFailureDiagnostic.Describe(captured,"PRIVATE credential!","PRIVATE credential!",null).StartsWith("stage=unknown; resource=unknown; HTTP=not-received"));
        Exception nested = captured;
        for (var i=0;i<20;i++) nested=new InvalidOperationException("PRIVATE",nested);
        text=SyncFailureDiagnostic.Describe(nested,"capture_framework","none",null);
        Check(text.Split("; exception=").Length==4 && text.Length<=SyncFailureDiagnostic.MaximumLength);
        Check(!text.Contains("PRIVATE") && !text.Contains("C:\\") && !text.Contains("C:/"));
        Check(SyncFailureDiagnostic.Describe(new OperationCanceledException("PRIVATE"),"snapshot_http","inventory",null).Contains("OperationCanceledException"));
        Check(SyncFailureDiagnostic.Describe(captured,"snapshot_http","inventory",999).Contains("HTTP=not-received"));
        Check(SyncFailureDiagnostic.Describe(new HostileMessage(),"capture_framework","none",null).Contains("HostileMessage"));
        Console.WriteLine($"Sync failure diagnostics PASS: {checks}; synthetic only, no exception payload/path/identifier logging.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowPrivateMessage() => throw new InvalidOperationException("PRIVATE credential https://example.test?token=PRIVATE 123456789");
    private sealed class HostileMessage : Exception { public override string Message => throw new Exception("PRIVATE"); }
}

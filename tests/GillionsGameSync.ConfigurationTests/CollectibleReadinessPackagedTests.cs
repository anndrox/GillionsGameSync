using System.Reflection;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

// Real compiled collector/cache adapters with managed service fixtures. No
// native function, running client, character data or HTTP endpoint is invoked.
internal static class CollectibleReadinessPackagedTests {
    internal static void ReproduceBaseline(Assembly plugin) {
        var source = new UnavailableCollectibleData();
        var unlocks = DispatchProxy.Create<IUnlockState, CollectibleServiceProxy>();
        var method = plugin.GetType("GillionsGameSync.CollectibleCollector", true)!
            .GetMethod("ReadPortraitConditions", BindingFlags.Static | BindingFlags.Public)!;
        var first = (long[])method.Invoke(null, [source, unlocks])!;
        var second = (long[])method.Invoke(null, [source, unlocks])!;
        if (first.Length != 0 || second.Length != 0 || source.Calls != 1)
            throw new Exception("The expected exact-baseline cache defect was not reproduced.");
        Console.WriteLine("Exact baseline defect reproduced: unavailable BannerCondition sheet became owned-empty twice; source requested once (cached missing result). No live player/native invocation.");
    }

    internal static void Run(Assembly plugin) {
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Packaged collectibles: " + why); }
        var source = new UnavailableCollectibleData();
        var cacheType = plugin.GetType("GillionsGameSync.SheetRowCache`1", true)!;
        var conditionsCache = cacheType.MakeGenericType(typeof(BannerCondition));
        for (var attempt = 0; attempt < 2; attempt++) {
            try { conditionsCache.GetMethod("GetRequired", statics)!.Invoke(null, [source]); throw new Exception("Unavailable sheet accepted."); }
            catch (TargetInvocationException e) when (e.InnerException?.GetType().Name == "CollectibleSourceUnavailableException") { checks++; }
        }
        Check(source.Calls == 2, "actual IDataManager null source retries on same service rather than caching []");

        T Row<T>(uint id) where T : struct => (T)Activator.CreateInstance(typeof(T), [null, 0u, id])!;
        void Seed<T>(T[] rows) where T : struct {
            var closed = cacheType.MakeGenericType(typeof(T));
            var cache = closed.GetField("Cache", statics)!.GetValue(null)!;
            cache.GetType().GetMethod("Get", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(cache, [source, new Func<IEnumerable<T>?>(() => rows)]);
        }
        Seed(new[] { Row<BannerCondition>(344), Row<BannerCondition>(696), Row<BannerCondition>(938) });
        var definitions = (BannerCondition[])conditionsCache.GetMethod("GetRequired", statics)!.Invoke(null, [source])!;
        Check(definitions.Select(r => r.RowId).SequenceEqual(new uint[] { 344, 696, 938 }), "actual cache recovers with canonical condition rows");
        var collector = plugin.GetType("GillionsGameSync.CollectibleCollector", true)!;
        var read = collector.GetMethod("Read", statics)!;
        long[] OwnedConditions(Func<BannerCondition, bool> unlocked) =>
            (long[])read.MakeGenericMethod(typeof(BannerCondition)).Invoke(null, [source, unlocked])!;
        Check(OwnedConditions(row => row.RowId == 696).SequenceEqual([696L]), "compiled generic reader preserves BannerCondition ID");
        Check(OwnedConditions(_ => false).Length == 0, "loaded definitions plus no unlocks produces valid owned-empty");
        Check(OwnedConditions(row => row.RowId == 938).SequenceEqual([938L]), "compiled reader rereads ownership on subsequent character observation");
        try {
            OwnedConditions(_ => throw new InvalidOperationException("fixture native read interruption"));
            throw new Exception("Interrupted read accepted.");
        } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { checks++; }
        Check(OwnedConditions(row => row.RowId == 344).SequenceEqual([344L]), "read failure cannot poison later ownership");

        Seed(new[] { Row<BannerFrame>(96) });
        var unlocks = DispatchProxy.Create<IUnlockState, CollectibleServiceProxy>();
        var unlockProxy = (CollectibleServiceProxy)unlocks;
        unlockProxy.Handler = (method, values) => method.Name == "IsBannerFrameUnlocked" && values is [BannerFrame row]
            ? row.RowId == 96 : throw new Exception("Portrait element used the wrong native unlock method.");
        var elements = (long[])collector.GetMethod("ReadPortraitFrames", statics)!.Invoke(null, [source, unlocks])!;
        Check(elements.SequenceEqual([96L]), "actual portrait-frame adapter retains element IDs and IUnlockState namespace");
        Check(!OwnedConditions(_ => true).Contains(96), "element96 cannot become an owned condition through the shared reader");

        var client = DispatchProxy.Create<IClientState, CollectibleServiceProxy>();
        ((CollectibleServiceProxy)client).Handler = (method, _) => method.Name == "get_IsLoggedIn" ? false
            : throw new Exception("Unexpected client read in logged-out fixture.");
        var objects = DispatchProxy.Create<IObjectTable, CollectibleServiceProxy>();
        ((CollectibleServiceProxy)objects).Handler = (method, _) => method.Name == "get_LocalPlayer" ? null
            : throw new Exception("Unexpected object read in logged-out fixture.");
        var dataCalls = source.Calls;
        var unlockCalls = unlockProxy.Calls;
        var withheld = new List<string>();
        var snapshots = (System.Collections.IEnumerable)plugin.GetType("GillionsGameSync.DirectGameSnapshotCollector", true)!
            .GetMethod("Collect", statics)!.Invoke(null, [null, client, objects, source, unlocks, null, null, new[] { "collectibles" }, null, withheld])!;
        Check(!snapshots.Cast<object>().Any(), "actual collector withholds whole resource while logged out/not initialized");
        // With no local player the outer identity guard now withholds every resource
        // before the collectible gate runs; that gate's own logged-out behaviour is
        // covered by CollectibleSnapshotPolicy fixtures in the ItemLink tests.
        Check(withheld.SequenceEqual(["collectibles:StaleContext"]), "withheld resource is named with its availability, not sent empty");
        Check(source.Calls == dataCalls && unlockProxy.Calls == unlockCalls, "unready entrypoint performs no sheet/unlock observation");
        Console.WriteLine($"Packaged collectible readiness PASS: {checks}; actual cache/typed adapter/entrypoint, synthetic only.");
    }
}

// DispatchProxy cannot emit IDataManager's self-constrained generic sheet
// signatures correctly. Implement the real SDK interface, failing any unused API.
internal sealed class UnavailableCollectibleData : IDataManager {
    public int Calls { get; private set; }
    public Dalamud.Game.ClientLanguage Language => throw new NotSupportedException();
    public Lumina.GameData GameData => throw new NotSupportedException();
    public Lumina.Excel.ExcelModule Excel => throw new NotSupportedException();
    public bool HasModifiedGameDataFiles => throw new NotSupportedException();
    public Lumina.Excel.ExcelSheet<T> GetExcelSheet<T>(Dalamud.Game.ClientLanguage? language = null, string? name = null)
        where T : struct, Lumina.Excel.IExcelRow<T> { Calls++; return null!; }
    public Lumina.Excel.SubrowExcelSheet<T> GetSubrowExcelSheet<T>(Dalamud.Game.ClientLanguage? language = null, string? name = null)
        where T : struct, Lumina.Excel.IExcelSubrow<T> => throw new NotSupportedException();
    public Lumina.Data.FileResource? GetFile(string path) => throw new NotSupportedException();
    public T? GetFile<T>(string path) where T : Lumina.Data.FileResource => throw new NotSupportedException();
    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken) where T : Lumina.Data.FileResource => throw new NotSupportedException();
    public bool FileExists(string path) => throw new NotSupportedException();
}

public class CollectibleServiceProxy : DispatchProxy {
    public int Calls { get; private set; }
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => throw new Exception("Unconfigured service fixture.");
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        Calls++;
        return Handler(targetMethod!, args);
    }
}

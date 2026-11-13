using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class DesynthesisPackagedTests {
    internal static void Run(Assembly assembly) {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var queueType = assembly.GetType("GillionsGameSync.DesynthesisQueue", true)!;
        var outputType = assembly.GetType("GillionsGameSync.DesynthesisOutput", true)!;
        var sourceType = assembly.GetType("GillionsGameSync.DesynthesisResultSource", true)!;
        var source = RuntimeHelpers.GetUninitializedObject(sourceType);
        var bulk = sourceType.GetMethod("ShouldReadBulkResult", instance)!;
        bool Refresh(bool running) => (bool)bulk.Invoke(source, [running])!;
        if (Refresh(false) || Refresh(true) || !Refresh(true) || !Refresh(true) || !Refresh(false) || Refresh(false))
            throw new Exception("Desynthesis: bulk refresh boundaries must skip startup and include each refresh through the final result.");
        typeof(DesynthesisPackagedTests).GetMethod(nameof(CheckQueue), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(outputType).Invoke(null, [queueType, sourceType, instance]);
    }

    private delegate bool Capture<T>(uint source, int? job, DateTime now, ReadOnlySpan<T> outputs);
    private delegate bool Read<T>(nint address, out uint source, Span<T> outputs, out int count);

    private static unsafe void CheckQueue<T>(Type queueType, Type sourceType, BindingFlags instance) where T : unmanaged {
        var queue = Activator.CreateInstance(queueType, true)!;
        var capture = queueType.GetMethod("Capture", instance)!.CreateDelegate<Capture<T>>(queue);
        var enable = queueType.GetMethod("SetEnabled", instance)!;
        var prepare = queueType.GetMethod("Prepare", instance)!;
        var acknowledge = queueType.GetMethod("Acknowledge", instance)!;
        var read = sourceType.GetMethod("ReadResult", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Read<T>>();
        var now = new DateTime(2026, 11, 12, 19, 59, 0, DateTimeKind.Utc);
        int checks = 0;
        void Check(bool condition, string name) {
            checks++;
            if (!condition) throw new Exception("Desynthesis: " + name);
        }
        T Output(uint id, int count) => (T)Activator.CreateInstance(typeof(T), [id, count])!;
        void Enabled(bool value) => enable.Invoke(queue, [value]);
        object? Prepare() => prepare.Invoke(queue, null);
        void Accept(object batch) => acknowledge.Invoke(queue, [batch]);
        static byte[] Bytes(object batch) => (byte[])batch.GetType().GetProperty("Payload", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(batch)!;
        static JsonElement Payload(object batch) {
            using var doc = JsonDocument.Parse(Bytes(batch));
            return doc.RootElement.Clone();
        }
        static string[] Keys(JsonElement value) => value.EnumerateObject().Select(property => property.Name).Order().ToArray();

        T[] output = [Output(2, 1), Output(3, 999)];
        // Synthetic memory at the measured November offsets, with no game calls.
        byte* agent = stackalloc byte[0x470];
        new Span<byte>(agent, 0x470).Clear();
        *(uint*)(agent + 0x444) = 1_005_594;
        *(uint*)(agent + 0x448) = 1_005_594;
        *(int*)(agent + 0x44c) = 1;
        *(uint*)(agent + 0x450) = 2;
        *(int*)(agent + 0x454) = 1;
        *(uint*)(agent + 0x458) = 1_000_003;
        *(int*)(agent + 0x45c) = 999;
        Span<T> decoded = stackalloc T[3];
        Check(read((nint)agent, out var source, decoded, out var count) && source == 5594 && count == 2
            && EqualityComparer<T>.Default.Equals(decoded[0], output[0])
            && EqualityComparer<T>.Default.Equals(decoded[1], output[1]), "reads one displayed result, normalises HQ and excludes the source");
        *(uint*)(agent + 0x458) = 2;
        Check(!read((nint)agent, out _, decoded, out _) && !read(0, out _, decoded, out _), "native duplicate and null agent fail closed");

        Check(!capture(5594, null, now, output) && Prepare() is null, "no capture or batch without permission");
        Enabled(true);
        Check(!capture(0, 9, now, output), "reject invalid source");
        Check(!capture(5594, 7, now, output) && !capture(5594, 16, now, output), "job range");
        Check(!capture(5594, 8, DateTime.SpecifyKind(now, DateTimeKind.Local), output), "UTC only");
        Check(!capture(5594, 8, DateTime.UtcNow.AddMinutes(3), output), "reject timestamps beyond the Site's future tolerance");
        Check(!capture(5594, null, now, []) && !capture(5594, null, now, [.. output, Output(4, 1), Output(5, 1)]),
            "output count range");
        Check(!capture(5594, null, now, [Output(0, 1)]) && !capture(5594, null, now, [Output(5594, 1)])
            && !capture(5594, null, now, [Output(2, 1), Output(2, 2)]), "no zero, source or duplicate output");
        Check(!capture(5594, null, now, [Output(2, 0)]) && !capture(5594, null, now, [Output(2, 1000)]), "quantity range");
        Check(capture(5594, null, now, output) && capture(5594, 15, now, output), "valid job and explicit null");
        var batch = Prepare()!;
        var payload = Payload(batch);
        Check(Keys(payload).SequenceEqual(["observations", "schema"])
            && payload.GetProperty("schema").GetString() == "desynthesis-results-v1", "exact payload keys and schema");
        var rows = payload.GetProperty("observations").EnumerateArray().ToArray();
        Check(rows.Length == 2 && rows[0].GetProperty("desynthJobId").ValueKind == JsonValueKind.Null
            && rows[1].GetProperty("desynthJobId").GetInt32() == 15, "nullable job on wire");
        var eventIds = rows.Select(row => row.GetProperty("eventId").GetString()!).ToArray();
        Check(eventIds.Distinct().Count() == rows.Length && eventIds.All(id => Regex.IsMatch(id, "^[A-Za-z0-9_-]{8,64}$"))
            && eventIds.All(id => Guid.TryParseExact(id, "N", out _)), "GUID-based unique event IDs");
        foreach (var row in rows) {
            Check(Keys(row).SequenceEqual(["desynthJobId", "eventId", "observedAtUtc", "outputs", "sourceItemId"])
                && row.GetProperty("observedAtUtc").GetString() == "2026-11-12T19:59:00Z", "exact observation keys and UTC");
            Check(row.GetProperty("outputs").EnumerateArray().All(item => Keys(item).SequenceEqual(["itemId", "quantity"])
                && item.GetProperty("itemId").GetUInt32() != 5594), "exact output keys and source exclusion");
        }
        Check(capture(5595, 8, now, output) && ReferenceEquals(batch, Prepare()) && Bytes(batch).SequenceEqual(Bytes(Prepare()!)),
            "failure and concurrent capture retain the same prepared batch and event IDs");
        Enabled(false);
        Check(!capture(5594, null, now, output) && Prepare() is null, "Off clears the prepared and unsent results");
        Enabled(true);
        Check(Prepare() is null, "On cannot resurrect results from before Off");
        for (uint id = 1000; id <= 1500; id++)
            if (!capture(id, 8, now, output)) throw new Exception("Valid overflow fixture refused.");
        var first = Prepare()!;
        var firstRows = Payload(first).GetProperty("observations");
        Check(firstRows.GetArrayLength() == 250 && firstRows[0].GetProperty("sourceItemId").GetUInt32() == 1001
            && firstRows[249].GetProperty("sourceItemId").GetUInt32() == 1250, "result 501 drops oldest and batch is capped at 250");
        Accept(first);
        var second = Prepare()!;
        Accept(first);
        var secondRows = Payload(second).GetProperty("observations");
        Check(ReferenceEquals(second, Prepare()) && secondRows.GetArrayLength() == 250
            && secondRows[249].GetProperty("sourceItemId").GetUInt32() == 1500, "late receipts leave the successor untouched");
        Accept(second);
        Check(Prepare() is null, "acknowledging both batches drains exactly 500");
        Console.WriteLine($"Packaged desynthesis PASS: {checks} checks on payload, permission, queue and retry.");
    }
}

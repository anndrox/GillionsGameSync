using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class AppearanceRenderPackagedTests {
    // Windows x64 fixture for ClientStructs 7.57.0.9480. All memory and vtables
    // belong to the test. The callbacks model the two read-only type queries.
    [UnmanagedCallersOnly]
    private static int CharacterBaseType(nint _) => 3;
    [UnmanagedCallersOnly]
    private static byte HumanType(nint _) => 1;
    [UnmanagedCallersOnly]
    private static byte WeaponType(nint _) => 4;

    internal static unsafe void Run(Assembly plugin) {
        var read = plugin.GetType("GillionsGameSync.CharacterAppearanceCollector", true)!
            .GetMethod("ReadEquipment", BindingFlags.Static | BindingFlags.NonPublic)!;
        var parameters = read.GetParameters();
        int checks = 0;
        void Check(bool passed, string description) {
            checks++;
            if (!passed) throw new Exception("Packaged appearance: " + description);
        }

        byte* character = stackalloc byte[0x2370];
        byte* human = stackalloc byte[0x1000];
        byte* main = stackalloc byte[0x1000];
        byte* off = stackalloc byte[0x1000];
        byte* inventory = stackalloc byte[14 * 0x48];
        new Span<byte>(character, 0x2370).Clear();
        new Span<byte>(human, 0x1000).Clear();
        new Span<byte>(main, 0x1000).Clear();
        new Span<byte>(off, 0x1000).Clear();
        new Span<byte>(inventory, 14 * 0x48).Clear();
        nint* humanTable = stackalloc nint[51];
        nint* weaponTable = stackalloc nint[51];
        humanTable[2] = weaponTable[2] = (nint)(delegate* unmanaged<nint, int>)&CharacterBaseType;
        humanTable[50] = (nint)(delegate* unmanaged<nint, byte>)&HumanType;
        weaponTable[50] = (nint)(delegate* unmanaged<nint, byte>)&WeaponType;
        *(nint**)human = humanTable;
        *(nint**)main = weaponTable;
        *(nint**)off = weaponTable;

        const int actorDrawData = 0x6F8;
        const int actorDrawObject = 0x100;
        const int weaponStride = 0x70;
        const int firstWeapon = actorDrawData + 0x10;
        const int weaponDrawObject = 0x18;
        *(byte**)(character + actorDrawObject) = human;
        *(byte**)(character + firstWeapon + weaponDrawObject) = main;
        *(byte**)(character + firstWeapon + weaponStride + weaponDrawObject) = off;

        (string Key, int Inventory, byte First, byte Second)[] slots = [
            ("mainHand", 0, 0, 0), ("offHand", 1, 0, 0),
            ("head", 2, 11, 21), ("body", 3, 12, 22), ("hands", 4, 13, 23),
            ("legs", 6, 0, 0), ("feet", 7, 14, 24), ("ears", 8, 0, 0),
            ("neck", 9, 0, 0), ("wrists", 10, 0, 0), ("ring1", 11, 0, 0), ("ring2", 12, 0, 0),
        ];
        var models = new Dictionary<uint, (ulong Main, ulong Sub)>();
        for (int i = 0; i < slots.Length; i++) {
            var slot = slots[i];
            var item = inventory + slot.Inventory * 0x48;
            uint baseId = 1000u + (uint)slot.Inventory;
            uint glamourId = i < 2 ? 0u : 2000u + (uint)slot.Inventory;
            *(int*)(item + 0x08) = 1000; // EquippedItems
            *(short*)(item + 0x0C) = (short)slot.Inventory;
            *(uint*)(item + 0x10) = baseId;
            *(uint*)(item + 0x3C) = glamourId;
            if (i < 2) {
                ulong model = 0x0300020001UL + (ulong)i;
                models[baseId] = (model, 0);
                *(ulong*)(character + firstWeapon + i * weaponStride) = model;
                *(ulong*)((i == 0 ? main : off) + 0xA20) = model;
            } else {
                ulong model = 0x010400UL + (ulong)i;
                models[baseId] = (model + 0x10000, 0);
                models[glamourId] = (model, 0);
                // Cache IDs match the visible model but all cache stains are zero.
                *(ulong*)(character + actorDrawData + 0x1D0 + (i - 2) * 8) = model;
                var armour = human + 0xA40 + (i - 2) * 8;
                *(ulong*)armour = model;
                armour[3] = slot.First;
                armour[4] = slot.Second;
            }
        }
        Func<uint, (ulong Main, ulong Sub)?> lookup = id => models.TryGetValue(id, out var model) ? model : null;
        object? Read() => read.Invoke(null, [
            Pointer.Box(character, parameters[0].ParameterType),
            Pointer.Box(inventory, parameters[1].ParameterType),
            lookup,
        ]);
        JsonElement Wire() => JsonSerializer.SerializeToElement(
            Read() ?? throw new Exception("Complete fixture withheld"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var equipment = Wire();
        Check(equipment.EnumerateObject().Count() == 12, "all twelve wire slots");
        foreach (var slot in slots) {
            var row = equipment.GetProperty(slot.Key);
            Check(row.GetProperty("itemId").GetUInt32() == 1000u + slot.Inventory, slot.Key + " underlying inventory index");
            Check(row.GetProperty("glamourItemId").GetUInt32() == (slot.Inventory < 2 ? 0u : 2000u + slot.Inventory),
                slot.Key + " shown model matches the correct glamour");
            Check(row.GetProperty("stain0Id").GetByte() == slot.First && row.GetProperty("stain1Id").GetByte() == slot.Second,
                slot.Key + " rendered dyes reach the wire");
        }
        // The owner had undyed weapons. Also vary the two weapon objects to
        // distinguish their two rendered dye fields from zero cache values.
        main[0xA26] = 51;
        main[0xA27] = 52;
        off[0xA26] = 61;
        off[0xA27] = 62;
        equipment = Wire();
        Check(equipment.GetProperty("mainHand").GetProperty("stain0Id").GetByte() == 51
            && equipment.GetProperty("mainHand").GetProperty("stain1Id").GetByte() == 52, "main hand uses its draw object");
        Check(equipment.GetProperty("offHand").GetProperty("stain0Id").GetByte() == 61
            && equipment.GetProperty("offHand").GetProperty("stain1Id").GetByte() == 62, "off hand uses its draw object");

        *(byte**)(character + firstWeapon + weaponStride + weaponDrawObject) = null;
        Check(Read() is null, "missing drawn equipped weapon withholds snapshot");
        *(uint*)(inventory + 0x48 + 0x10) = 0;
        Check(Wire().GetProperty("offHand").GetProperty("itemId").GetUInt32() == 0, "empty equipped slot needs no draw object");
        *(byte**)(character + actorDrawObject) = main;
        Check(Read() is null, "non-human root model withholds snapshot");
        Console.WriteLine($"Packaged appearance rendered dyes PASS: {checks} checks on synthetic ClientStructs memory.");
    }
}

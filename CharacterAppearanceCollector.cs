#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using EquipmentSlot = FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer.EquipmentSlot;
using EquippedSlot = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.GearsetItemIndex;
using WeaponSlot = FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer.WeaponSlot;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Invoked exclusively by Plugin's framework update. No pointers/spans escape Read.
internal sealed class CharacterAppearanceCollector(IClientState client,IObjectTable objects,IPlayerState player,IDataManager data,ICondition conditions) {
    private static readonly (string Key, EquippedSlot Inventory, EquipmentSlot Armour, WeaponSlot? Weapon)[] Slots = [
        ("mainHand", EquippedSlot.MainHand, default, WeaponSlot.MainHand),
        ("offHand", EquippedSlot.OffHand, default, WeaponSlot.OffHand),
        ("head", EquippedSlot.Head, EquipmentSlot.Head, null),
        ("body", EquippedSlot.Body, EquipmentSlot.Body, null),
        ("hands", EquippedSlot.Hands, EquipmentSlot.Hands, null),
        ("legs", EquippedSlot.Legs, EquipmentSlot.Legs, null),
        ("feet", EquippedSlot.Feet, EquipmentSlot.Feet, null),
        ("ears", EquippedSlot.Ears, EquipmentSlot.Ears, null),
        ("neck", EquippedSlot.Neck, EquipmentSlot.Neck, null),
        ("wrists", EquippedSlot.Wrists, EquipmentSlot.Wrists, null),
        ("ring1", EquippedSlot.RingRight, EquipmentSlot.RFinger, null),
        ("ring2", EquippedSlot.RingLeft, EquipmentSlot.LFinger, null),
    ];

    internal string GameVersion => data.GameData.Repositories.TryGetValue("ffxiv",out var r)?r.Version:"unavailable";
    internal string NativeVersion => typeof(InventoryManager).Assembly.GetName().Version?.ToString()??"unavailable";
    internal bool Qualified => NativeCompatibility.SupportsRuntime(NativeFamily.Appearance, GameVersion);
    internal bool Available(ulong character) => Qualified&&character!=0&&client.IsLoggedIn&&player.IsLoaded&&player.ContentId==character&&!client.IsGPosing
        &&!conditions[ConditionFlag.BetweenAreas]&&!conditions[ConditionFlag.BetweenAreas51]&&!conditions[ConditionFlag.LoggingOut]
        &&!conditions[ConditionFlag.Transformed]&&!conditions[ConditionFlag.RolePlaying]&&!conditions[ConditionFlag.Disguised];
    internal unsafe AppearanceSnapshot? Read(ulong character,DateTime now) {
        if(!Available(character))return null;
        var local=objects.LocalPlayer;if(local is null)return null;
        var characterAddress = (Character*)local.Address;
        if (characterAddress == null) return null;
        var customization=local.Customize;
        if(customization.Length!=26)return null;
        var hex=Convert.ToHexStringLower(customization.ToArray());
        var manager=InventoryManager.Instance();if(manager==null)return null;
        var container=manager->GetInventoryContainer(InventoryType.EquippedItems);
        if(container==null||!container->IsLoaded||container->Type!=InventoryType.EquippedItems||container->Items==null||container->Size!=14)return null;
        var items = data.GetExcelSheet<Item>();
        var equipment = ReadEquipment(characterAddress, container->Items, id =>
            items.GetRowOrDefault(id) is { } row ? (row.ModelMain, row.ModelSub) : null);
        if (equipment is null) return null;
        if (!Available(character) || objects.LocalPlayer?.Address != local.Address) return null;
        return new() {CollectorVersion=typeof(Plugin).Assembly.GetName().Version!.ToString(4),GameVersion=GameVersion,ClientStructsVersion=NativeVersion,
            ObservedAtUtc=now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture),CustomizationHex=hex,Equipment=equipment};
    }

    internal static unsafe Dictionary<string, AppearanceSlot>? ReadEquipment(Character* character, InventoryItem* items,
        Func<uint, (ulong Main, ulong Sub)?> lookupModels) {
        var drawObject = character->GameObject.DrawObject;
        if (!IsModel(drawObject, CharacterBase.ModelType.Human)) return null;
        var human = (Human*)drawObject;
        var equipment = new Dictionary<string, AppearanceSlot>(Slots.Length);
        foreach (var slot in Slots) {
            var index = (int)slot.Inventory;
            var item = items + index;
            if (item->IsSymbolic || item->Container != InventoryType.EquippedItems || item->Slot != index) return null;
            var itemId = item->ItemId;
            var glamourId = item->GlamourId;
            if (itemId == 0) {
                if (glamourId != 0) return null;
                equipment.Add(slot.Key, new());
                continue;
            }

            AppearanceDrawSlot? drawn = slot.Weapon is { } weaponSlot
                ? ReadWeapon(&character->DrawData, weaponSlot)
                : ReadArmour(human, slot.Armour);
            if (drawn is not { } model) return null;
            var weapon = slot.Weapon.HasValue;
            var baseMatches = Matches(lookupModels(itemId), model.Model, weapon);
            var glamourMatches = glamourId != 0 && Matches(lookupModels(glamourId), model.Model, weapon);
            var result = AppearancePolicy.FromDrawSlot(itemId, glamourId, model, baseMatches, glamourMatches);
            if (result is null) return null;
            equipment.Add(slot.Key, result);
        }
        return character->GameObject.DrawObject == drawObject ? equipment : null;
    }

    // The game object's model IDs can match while their cached stain bytes
    // differ from the rendered equipment. Read both stains from the draw objects.
    private static unsafe AppearanceDrawSlot ReadArmour(Human* human, EquipmentSlot slot) {
        var armour = human->EquipmentModels[(int)slot];
        return new(armour.Value & 0xFFFFFF, armour.Stain0, armour.Stain1);
    }

    private static unsafe AppearanceDrawSlot? ReadWeapon(DrawDataContainer* draw, WeaponSlot slot) {
        var drawObject = draw->Weapon(slot).DrawData.DrawObject;
        if (!IsModel(drawObject, CharacterBase.ModelType.Weapon)) return null;
        var weapon = (Weapon*)drawObject;
        var model = weapon->ModelSetId | ((ulong)weapon->SecondaryId << 16) | ((ulong)weapon->Variant << 32);
        return new(model, weapon->Stain0, weapon->Stain1);
    }

    private static unsafe bool IsModel(DrawObject* drawObject, CharacterBase.ModelType type) =>
        drawObject != null && drawObject->Object.VirtualTable != null
        && drawObject->Object.GetObjectType() == ObjectType.CharacterBase
        && ((CharacterBase*)drawObject)->GetModelType() == type;

    private static bool Matches((ulong Main, ulong Sub)? item, ulong drawn, bool weapon) {
        if (item is not { } row) return false;
        var mask = weapon ? 0x0000FFFFFFFFFFFFUL : 0xFFFFFFUL;
        return (row.Main & mask) == drawn || row.Sub != 0 && (row.Sub & mask) == drawn;
    }
}
#endif

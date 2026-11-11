#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Invoked exclusively by Plugin's framework update. No pointers/spans escape Read.
internal sealed class CharacterAppearanceCollector(IClientState client,IObjectTable objects,IPlayerState player,IDataManager data,ICondition conditions) {
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
        var equipment=new Dictionary<string,AppearanceSlot>();
        for(var i=0;i<AppearancePolicy.Slots.Length;i++){
            var index=AppearancePolicy.Indices[i];var item=container->Items+index;
            if (item->IsSymbolic || item->Container != InventoryType.EquippedItems || item->Slot != index) return null;
            var itemId = item->ItemId;
            var glamourId = item->GlamourId;
            var weapon = i < 2;
            var draw = ReadDrawSlot(&characterAddress->DrawData, i);
            var baseModel = itemId == 0 ? null : items.GetRowOrDefault(itemId);
            var glamourModel = glamourId == 0 ? null : items.GetRowOrDefault(glamourId);
            var slot = AppearancePolicy.FromDrawSlot(itemId, glamourId, draw,
                Matches(baseModel, draw.Model, weapon), Matches(glamourModel, draw.Model, weapon));
            if (slot is null) return null;
            equipment.Add(AppearancePolicy.Slots[i],slot);
        }
        if (!Available(character) || objects.LocalPlayer?.Address != local.Address) return null;
        return new() {CollectorVersion=typeof(Plugin).Assembly.GetName().Version!.ToString(4),GameVersion=GameVersion,ClientStructsVersion=NativeVersion,
            ObservedAtUtc=now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture),CustomizationHex=hex,Equipment=equipment};
    }

    private static unsafe AppearanceDrawSlot ReadDrawSlot(DrawDataContainer* draw, int slot) {
        if (slot < 2) {
            var weapon = draw->WeaponData[slot].ModelId;
            return new(weapon.Value & 0x0000FFFFFFFFFFFF, weapon.Stain0, weapon.Stain1);
        }
        // DrawData's armour list omits the two weapon entries and uses a
        // separate sequence from EquippedItems (which includes the unused belt).
        var armour = draw->EquipmentModelIds[slot - 2];
        return new(armour.Value & 0xFFFFFF, armour.Stain0, armour.Stain1);
    }

    private static bool Matches(Item? item, ulong drawn, bool weapon) {
        if (item is not { } row) return false;
        var mask = weapon ? 0x0000FFFFFFFFFFFFUL : 0xFFFFFFUL;
        return (row.ModelMain & mask) == drawn || row.ModelSub != 0 && (row.ModelSub & mask) == drawn;
    }
}
#endif

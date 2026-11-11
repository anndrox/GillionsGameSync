#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

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
        var customization=local.Customize;
        if(customization.Length!=26)return null;
        var hex=Convert.ToHexStringLower(customization.ToArray());
        var manager=InventoryManager.Instance();if(manager==null)return null;
        var container=manager->GetInventoryContainer(InventoryType.EquippedItems);
        if(container==null||!container->IsLoaded||container->Type!=InventoryType.EquippedItems||container->Items==null||container->Size!=14)return null;
        var equipment=new Dictionary<string,AppearanceSlot>();
        for(var i=0;i<AppearancePolicy.Slots.Length;i++){
            var index=AppearancePolicy.Indices[i];var item=container->Items+index;
            if(item->IsSymbolic||item->Container!=InventoryType.EquippedItems||item->Slot!=index||item->Stains.Length!=2)return null;
            var slot=new AppearanceSlot {ItemId=item->ItemId,GlamourItemId=item->GlamourId,Stain0Id=item->Stains[0],Stain1Id=item->Stains[1]};
            // Loaded zero is empty; unavailable or internally inconsistent data is never an empty look.
            if(slot.ItemId==0&&(slot.GlamourItemId!=0||slot.Stain0Id!=0||slot.Stain1Id!=0))return null;
            equipment.Add(AppearancePolicy.Slots[i],slot);
        }
        if(!Available(character))return null;
        return new() {CollectorVersion=typeof(Plugin).Assembly.GetName().Version!.ToString(4),GameVersion=GameVersion,ClientStructsVersion=NativeVersion,
            ObservedAtUtc=now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture),CustomizationHex=hex,Equipment=equipment};
    }
}
#endif

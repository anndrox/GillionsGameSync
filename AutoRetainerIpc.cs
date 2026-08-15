using System;
using System.Collections.Generic;
#if !GILLIONS_POLICY_TESTS
using Dalamud.Plugin;
#endif

namespace GillionsGameSync;

// Dalamud IPC converts provider values between plugin load contexts. Using
// AutoRetainerAPI's concrete type here would run its plugin-owned constructor
// inside Gillions, where AutoRetainer's service singleton is intentionally not
// initialized. This complete neutral mirror keeps that conversion data-only.
public sealed class AutoRetainerAdditionalDataMirror {
    public bool EntrustDuplicates;
    public bool WithdrawGil;
    public int WithdrawGilPercent = 100;
    public bool Deposit;
    public AutoRetainerVenturePlanMirror VenturePlan = new();
    public string LinkedVenturePlan = "";
    public uint VenturePlanIndex;
    public bool EnablePlanner;
    public int Ilvl = -1;
    public int Gathering = -1;
    public int Perception = -1;
    public Guid EntrustPlan = Guid.Empty;
}

public sealed class AutoRetainerVenturePlanMirror {
    public string Name = "";
    public List<AutoRetainerPlannedVentureMirror> List = [];
    public AutoRetainerPlanCompleteBehaviorMirror PlanCompleteBehavior;
}

public sealed class AutoRetainerPlannedVentureMirror {
    public uint ID;
    public int Num = 1;
}

public enum AutoRetainerPlanCompleteBehaviorMirror {
    Restart_plan,
    Assign_Quick_Venture,
    Do_nothing,
    Repeat_last_venture,
}

#if !GILLIONS_POLICY_TESTS
internal sealed class AutoRetainerIpc(IDalamudPluginInterface pluginInterface) {
    public object? ReadAdditionalRetainerData(ulong contentId, string retainerName) => pluginInterface
        .GetIpcSubscriber<ulong, string, AutoRetainerAdditionalDataMirror>("AutoRetainer.GetAdditionalRetainerData")
        .InvokeFunc(contentId, retainerName);

    public void WriteAdditionalRetainerData(ulong contentId, string retainerName, object data) {
        if (data is not AutoRetainerAdditionalDataMirror mirror)
            throw new InvalidOperationException("AutoRetainer returned an unexpected additional-data record.");
        pluginInterface
            .GetIpcSubscriber<ulong, string, AutoRetainerAdditionalDataMirror, object>("AutoRetainer.WriteAdditionalRetainerData")
            .InvokeAction(contentId, retainerName, mirror);
    }
}
#endif

#if GILLIONS_TEST_BUILD
using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

internal sealed class DesynthesisResultSource : IDisposable {
    private const string Single = "SalvageResult";
    private const string Bulk = "SalvageAutoDialog";
    private readonly IAddonLifecycle lifecycle;
    private readonly IGameGui gameGui;
    private readonly IDataManager data;
    private readonly IFramework framework;
    private readonly Func<bool> authorized;
    private readonly DesynthesisQueue queue;
    private bool singleCaptured;
    private bool bulkActive;

    internal DesynthesisResultSource(IAddonLifecycle lifecycle, IGameGui gameGui, IDataManager data, IFramework framework,
        Func<bool> authorized, DesynthesisQueue queue) {
        this.lifecycle = lifecycle;
        this.gameGui = gameGui;
        this.data = data;
        this.framework = framework;
        this.authorized = authorized;
        this.queue = queue;
        lifecycle.RegisterListener(AddonEvent.PostSetup, Single, OnResult);
        lifecycle.RegisterListener(AddonEvent.PostShow, Single, OnResult);
        lifecycle.RegisterListener(AddonEvent.PostHide, Single, OnSingleClosed);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Single, OnSingleClosed);
        lifecycle.RegisterListener(AddonEvent.PostRefresh, Bulk, OnResult);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Bulk, OnBulkClosed);
    }

    private void OnSingleClosed(AddonEvent _, AddonArgs args) => singleCaptured = false;
    private void OnBulkClosed(AddonEvent _, AddonArgs args) => bulkActive = false;

    private unsafe void OnResult(AddonEvent _, AddonArgs args) {
        if (!framework.IsInFrameworkUpdateThread) return;
        if (!authorized()) {
            // A result received while Off must not be taken by a later Show.
            if (args.AddonName == Single) singleCaptured = true;
            if (args.AddonName == Bulk) bulkActive = false;
            return;
        }
        if (!data.GameData.Repositories.TryGetValue("ffxiv", out var game)
            || !NativeCompatibility.SupportsRuntime(NativeFamily.Desynthesis, game.Version)) return;
        // One route owns each result. While the bulk dialog is running, ignore
        // the single-result window's lifecycle even if the game also opens it.
        if (args.AddonName == Bulk) {
            // Starting the dialog can refresh it before the first result.
            if (!ShouldReadBulkResult(BulkRunning(args.Addon.Address))) return;
        } else if (args.AddonName == Single) {
            if (singleCaptured || bulkActive || BulkRunning(gameGui.GetAddonByName(Bulk).Address)) return;
        } else return;
        var agent = gameGui.GetAgentById((int)AgentId.Salvage).Address;
        Span<DesynthesisOutput> outputs = stackalloc DesynthesisOutput[3];
        if (!ReadResult(agent, out var source, outputs, out var count)) return;
        // The agent does not expose a class for the completed result. The
        // source row verifies desynthesis eligibility without inferring a job.
        var items = data.GetExcelSheet<Item>();
        if (items?.GetRowOrDefault(source) is not { Desynth: > 0 }) return;
        foreach (var output in outputs[..count])
            if (!items.HasRow(output.ItemId)) return;
        if (queue.Capture(source, null, DateTime.UtcNow, outputs[..count]) && args.AddonName == Single)
            singleCaptured = true;
    }

    internal bool ShouldReadBulkResult(bool running) {
        var wasRunning = bulkActive;
        bulkActive = running;
        return wasRunning;
    }

    private static unsafe bool BulkRunning(nint address) =>
        address != 0 && ((AddonSalvageAutoDialog*)address)->AtkUnitBase.IsVisible
            && ((AddonSalvageAutoDialog*)address)->IsDesynthesizing;

    internal static unsafe bool ReadResult(nint address, out uint source, Span<DesynthesisOutput> outputs, out int count) {
        source = 0;
        count = 0;
        if (address == 0 || outputs.Length < 3) return false;
        var agent = (AgentSalvage*)address;
        source = BaseItemId(agent->DesynthItemId);
        if (source == 0) return false;
        foreach (var result in agent->DesynthResults) {
            var item = BaseItemId(result.ItemId);
            if (item == 0 && result.Quantity == 0 || item == source) continue;
            if (item == 0 || result.Quantity is < 1 or > 999) return false;
            for (var i = 0; i < count; i++)
                if (outputs[i].ItemId == item) return false;
            outputs[count++] = new(item, result.Quantity);
        }
        return count > 0;
    }

    private static uint BaseItemId(uint item) => item is >= 1_000_000 and < 2_000_000 ? item - 1_000_000 : item;

    public void Dispose() {
        lifecycle.UnregisterListener(OnResult);
        lifecycle.UnregisterListener(OnSingleClosed);
        lifecycle.UnregisterListener(OnBulkClosed);
    }
}
#endif

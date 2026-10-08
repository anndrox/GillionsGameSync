#if GILLIONS_TEST_BUILD
// The installed SDK labels this typed sheet experimental; this local test exists
// to evaluate it. Do not carry this opt-in into the stable collector.
#pragma warning disable PendingExcelSchema
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using XBMPet = Lumina.Excel.Sheets.Experimental.XBMPet;

namespace GillionsGameSync;

// Owned by the testing Plugin lifecycle; never reads/writes configuration or transport.
internal sealed class BeastmasterLocalView : IDisposable {
    private readonly IDalamudPluginInterface ui;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly BeastmasterReadiness readiness = new();
    private bool visible;
    private bool enabled = false;
    private bool disposed;
    private bool resetPending;
    private ulong sampledCharacter;
    private DateTime nextSample;
    private string status = "Sampling off. Enable to begin this local test.";
    private string state = "Unknown";
    private string[] rows = [];
    private string sampledAt = "Never";
    private string counts = "Ownership unknown";
    private (uint Id, string Name)[]? catalog;

    public BeastmasterLocalView(IDalamudPluginInterface pluginInterface, ICommandManager commands,
        IFramework framework, IClientState clientState, IDataManager dataManager) {
        ui = pluginInterface; this.commands = commands; this.framework = framework;
        client = clientState; data = dataManager;
        commands.AddHandler("/gillionsbst", new CommandInfo(Open) { HelpMessage = "Open the local Beastmaster diagnostic (no uploads)." });
        ui.UiBuilder.Draw += Draw;

        framework.Update += Update;
        client.Login += OnLogin;
        client.Logout += OnLogout;
    }

    private void Open(string command, string arguments) => visible = true;
    public void Show() => visible = true;
    private void OnLogin() => resetPending = true;
    private void OnLogout(int type, int code) => resetPending = true;
    private void Clear(string reason) {
        readiness.Reset(); sampledCharacter = 0; rows = []; counts = "Ownership unknown"; state = "Unknown";
        status = reason; sampledAt = "Never";
    }

    private unsafe void Update(IFramework _) {
        if (disposed) return;
        if (resetPending) { resetPending = false; Clear("Session changed; awaiting fresh loading state."); nextSample = DateTime.MinValue; }
        if (!enabled || !visible) return;
        // Check character validity on every active frame; native pet reads run once a second.
        var player = PlayerState.Instance();
        if (!client.IsLoggedIn || player == null || !player->IsLoaded || player->ContentId == 0) {
            Clear("Not logged in / player not ready. Ownership unknown.");
            nextSample = DateTime.MinValue; return;
        }
        if (sampledCharacter != player->ContentId) {
            Clear("Character changed; awaiting fresh loading state.");
            sampledCharacter = player->ContentId; nextSample = DateTime.MinValue;
        }
        if (DateTime.UtcNow < nextSample) return;
        nextSample = DateTime.UtcNow.AddSeconds(1);
        rows = []; counts = "Ownership unknown";
        sampledAt = DateTime.Now.ToString("HH:mm:ss");
        try {
            var manager = XBMManager.Instance();
            if (manager == null) { readiness.Reset(); state = "Unavailable"; status = "Native manager unavailable. Ownership unknown."; return; }
            var nativeState = manager->State;
            state = nativeState.ToString();
            var admitted = readiness.Observe(player->ContentId,
                nativeState is XBMManager.DataState.None or XBMManager.DataState.Requested,
                nativeState == XBMManager.DataState.Received);
            if (!admitted) {
                status = nativeState == XBMManager.DataState.Received
                    ? "List already received, but current-character freshness is unverified. Keep sampling on, log out/in, then open the bestiary."
                    : "List not loaded. Open the bestiary manually; unknown does not mean zero pets.";
                return;
            }
            catalog ??= data.GetExcelSheet<XBMPet>().Where(row => row.Pet.RowId != 0 && row.Pet.IsValid)
                .Select(row => (row.RowId, row.Pet.Value.Name.ToString())).ToArray();
            if (catalog.Length == 0) { status = "Pet catalog unavailable. Ownership unknown."; return; }
            var results = catalog.Select(pet => (pet.Id, pet.Name, Owned: manager->IsPetUnlocked(pet.Id))).ToArray();
            var owned = results.Count(pet => pet.Owned);
            if (manager->NumUnlockedPets != owned) {
                status = "Native count and catalog query disagree. Ownership unknown; report this mismatch.";
                counts = $"Native count: {manager->NumUnlockedPets}; queried count: {owned}; catalog: {catalog.Length}";
                return;
            }
            counts = $"Owned: {owned} / catalog: {catalog.Length}";
            rows = results.Select(pet => $"{(pet.Owned ? "Owned" : "Not owned")} | {pet.Id} | {pet.Name}").ToArray();
            status = "Fresh loading observed for this character. Compare every result with the bestiary; live correctness is not yet established.";
        } catch (Exception) {
            readiness.Reset(); sampledCharacter = 0; rows = []; counts = "Ownership unknown";
            status = "Read failed. Ownership unknown. Disable sampling and report the game / Dalamud versions.";
        }
    }

    private void Draw() {
        if (!visible || disposed) return;
        ImGui.SetNextWindowSize(new Vector2(680, 520), ImGuiCond.FirstUseEver);
        var draw = ImGui.Begin("Beastmaster local test###GillionsBeastmasterLocalView", ref visible);
        if (draw) {
            ImGui.TextWrapped("Beastmaster reads stay local and are not saved or uploaded. No pairing is needed for this test. Sampling stops when this window closes.");
            if (ImGui.Checkbox("Enable local sampling", ref enabled)) {
                Clear(enabled ? "Waiting for sample..." : "Sampling off."); nextSample = DateTime.MinValue;
            }
            ImGui.TextWrapped($"Loading state: {state} | Last sample: {sampledAt}");
            ImGui.TextWrapped(status);
            ImGui.TextWrapped(counts);
            if (ImGui.Button("Copy current diagnostic text")) {
                ImGui.SetClipboardText($"Gillions Game Sync Testing {typeof(Plugin).Assembly.GetName().Version}\nBeastmaster local read\nState: {state}\nSample: {sampledAt}\n{status}\n{counts}\n" + string.Join("\n", rows));
            }
            ImGui.Separator();
            ImGui.TextWrapped("Check: before/after bestiary opening; known owned and unowned pets; a manual capture; logout/login; a different character. No character identifiers are displayed or copied.");
            ImGui.BeginChild("Pets", new Vector2(0, 0), false);
            foreach (var row in rows) ImGui.TextUnformatted(row);
            ImGui.EndChild();
        }
        ImGui.End();
        if (!visible) { enabled = false; Clear("Sampling off. Enable to begin this local test."); }
    }

    public void Dispose() {
        disposed = true;
        framework.Update -= Update; client.Login -= OnLogin; client.Logout -= OnLogout;
        ui.UiBuilder.Draw -= Draw;
        commands.RemoveHandler("/gillionsbst"); Clear("Disposed.");
    }
}
#endif
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace GillionsGameSync;

// The standard Dalamud WindowSystem owns placement, sizing and close handling.
// No custom persistence, UI framework, focus requests or network work in Draw.
internal sealed class PublicGameSyncUi : IDisposable {
    private readonly WindowSystem windows = new("GillionsGameSync");
    private readonly Surface main, settings, pairing, hunts;
    private readonly HuntProgressState progress;
    private readonly Func<bool> locked;
    private readonly Action closeHunts;
    private readonly HuntProgressWindowGate huntWindow = new();
    internal bool Visible => main.IsOpen || settings.IsOpen || pairing.IsOpen;
    internal PublicGameSyncUi(Action drawMain, Action drawSettings, Action drawPairing, Action drawHunts,
        HuntProgressState progress, Func<bool> locked, Action closeHunts, bool firstRun, bool testing) {
        this.progress=progress; this.locked=locked; this.closeHunts=closeHunts;
        string badge=testing ? " [TESTING]" : "";
        main=new("Gillions Game Sync"+badge+"###GillionsGameSync",drawMain,420);
        settings=new("Gillions Settings"+badge+"###GillionsSettings",drawSettings,520);
        pairing=new("Welcome to Gillions"+badge+"###GillionsPairing",drawPairing,460);
        hunts=new("Hunt Progress###GillionsHuntProgress",drawHunts,300);
        // Keep the player's chosen geometry through content changes and reopenings.
        // FirstUseEver and the unchanged ID let Dalamud/ImGui own persistence.
        hunts.Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
        hunts.Size=new Vector2(300,180);
        hunts.SizeConstraints=new WindowSizeConstraints {
            MinimumSize=new Vector2(220,100), MaximumSize=new Vector2(float.MaxValue,float.MaxValue)
        };
        hunts.Flags |= ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoNavInputs;
        hunts.RespectCloseHotkey=false; hunts.DisableWindowSounds=true; hunts.DisableFadeInFadeOut=true;
        foreach(var window in new[]{main,settings,pairing,hunts}) windows.AddWindow(window);
        pairing.IsOpen=firstRun;
    }
    internal void ShowMain()=>main.IsOpen=true;
    internal void ShowSettings()=>settings.IsOpen=true;
    internal void ShowPairing()=>pairing.IsOpen=true;
    internal void FinishPairing() { pairing.IsOpen=false; main.IsOpen=true; }
    internal void Draw() {
        hunts.Flags = locked() ? hunts.Flags | ImGuiWindowFlags.NoMove : hunts.Flags & ~ImGuiWindowFlags.NoMove;
        bool requestedOpen=huntWindow.Prepare(progress.Visible,progress.InteractionRevision);
        hunts.IsOpen=requestedOpen;
        windows.Draw();
        if(huntWindow.Observe(requestedOpen,hunts.IsOpen,progress.InteractionRevision)) closeHunts();
    }
    public void Dispose()=>windows.RemoveAllWindows();
    private sealed class Surface : Window {
        private readonly Action draw;
        internal Surface(string name,Action draw,float width) : base(name) {
            this.draw=draw;
            Size=new Vector2(width,0); SizeCondition=ImGuiCond.FirstUseEver;
            Flags=ImGuiWindowFlags.AlwaysAutoResize;
            AllowPinning=false; AllowClickthrough=false;
        }
        public override void Draw()=>draw();
    }
}

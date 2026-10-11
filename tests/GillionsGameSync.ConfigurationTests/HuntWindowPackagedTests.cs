using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

// Exact assembly/window properties only; no Draw, game, network or OS input.
internal static class HuntWindowPackagedTests {
    internal static void Run(Assembly assembly, bool testing) {
        const BindingFlags nonpublic=BindingFlags.NonPublic|BindingFlags.Instance;
        var type=assembly.GetType("GillionsGameSync.PublicGameSyncUi",true)!;
        var state=Activator.CreateInstance(assembly.GetType("GillionsGameSync.HuntProgressState",true)!,true)!;
        Action noop=()=>{};
        using var ui=(IDisposable)Activator.CreateInstance(type,nonpublic,null,
            [noop,noop,noop,noop,state,(Func<bool>)(()=>false),noop,false,testing],null)!;
        var hunts=(Window)type.GetField("hunts",nonpublic)!.GetValue(ui)!;
        int checks=0;
        void Check(bool value,string message) { checks++; if(!value) throw new InvalidOperationException(message); }
        Check((hunts.Flags&(ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoScrollbar))==0,
            "Hunt Progress must be resizable, scrollable and use normal saved geometry.");
        Check(hunts.Size==new Vector2(300,180)&&hunts.SizeCondition==ImGuiCond.FirstUseEver,
            "Hunt initial dimensions must apply only on first use, never on every open/frame.");
        Check(hunts.SizeConstraints is { } bounds&&bounds.MinimumSize==new Vector2(220,100)
            &&bounds.MaximumSize==new Vector2(float.MaxValue,float.MaxValue),
            "Hunt Progress requires a readable scaled minimum without a fixed maximum.");
        var passive=ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNavFocus|ImGuiWindowFlags.NoNavInputs;
        Check((hunts.Flags&passive)==passive&&!hunts.RespectCloseHotkey&&hunts.DisableWindowSounds&&hunts.DisableFadeInFadeOut,
            "Manual resize must preserve passive overlay/focus/sound boundaries.");
        Check(hunts.WindowName=="Hunt Progress###GillionsHuntProgress", "Saved Hunt window identity changed.");
        foreach(var (name,size,minimum,id) in new[]{
            ("main",new Vector2(420,340),new Vector2(360,220),"Gillions Game Sync"),
            ("settings",new Vector2(520,460),new Vector2(420,260),"Gillions Settings"),
            ("pairing",new Vector2(460,520),new Vector2(400,300),"Welcome to Gillions")}) {
            var window=(Window)type.GetField(name,nonpublic)!.GetValue(ui)!;
            Check((window.Flags&(ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoScrollbar))==0,
                name+" must respect player resizing, saved geometry and standard scrolling.");
            Check(window.Size==size&&window.SizeCondition==ImGuiCond.FirstUseEver,name+" default must not fight later resizing/reopen.");
            Check(window.SizeConstraints is { } limits&&limits.MinimumSize==minimum&&limits.MaximumSize==new Vector2(float.MaxValue,float.MaxValue),
                name+" must repair previously saved narrow widths using standard constraints.");
            Check(window.WindowName.StartsWith(id+(testing?" [TESTING]":"")+"###",StringComparison.Ordinal),name+" identity/channel changed.");
        }
        Console.WriteLine($"Exact packaged windows: {checks} geometry/persistence/passive/manual-sizing checks PASS; no live UI or game invocation.");
    }
}

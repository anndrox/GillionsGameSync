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
        void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
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
        foreach(var name in new[]{"main","settings","pairing"}) {
            var window=(Window)type.GetField(name,nonpublic)!.GetValue(ui)!;
            Check((window.Flags&ImGuiWindowFlags.AlwaysAutoResize)!=0,"Only Hunt Progress should lose auto-sizing.");
        }
        Console.WriteLine("Exact packaged Hunt window: 8 geometry/persistence/passive/other-window checks PASS; no live UI or game invocation.");
    }
}

using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

// Offline actual candidate Draw methods; no plugin constructor/services,
// Windows input, browser, game or network. C# is needed for the typed .NET
// ImGui API; Python rasterizes its actual triangles, not a second UI design.
if(args.Length!=3) throw new ArgumentException("Exact DLL, Dalamud library directory, output directory required.");
string binary=Path.GetFullPath(args[0]),libs=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[2]);
Directory.CreateDirectory(output);
AssemblyLoadContext.Default.Resolving+=(_,name)=>{var file=Path.Combine(libs,name.Name+".dll");return File.Exists(file)?AssemblyLoadContext.Default.LoadFromAssemblyPath(file):null;};
NativeLibrary.Load(Path.Combine(libs,"cimgui.dll"));
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(binary);
using(var asset=assembly.GetManifestResourceStream("GillionsGameSync.Branding.png") ?? throw new InvalidOperationException("Exact packaged approved icon missing.")) {
    using var destination=File.Create(Path.Combine(output,"branding.png"));asset.CopyTo(destination);
}
object Proxy(Type type,Func<MethodInfo,object?[],object?> handler) {
    var proxy=DispatchProxy.Create(type,typeof(TextureFixtureProxy));((TextureFixtureProxy)proxy).Handler=handler;return proxy;
}
var textureWrapType=Assembly.Load("Dalamud").GetType("Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap",true)!;
var wrap=Proxy(textureWrapType,(m,a)=>m.Name switch { "get_Handle"=>new ImTextureID(2ul),"get_Width" or "get_Height"=>128,"get_Size"=>new Vector2(128),_=>null });
var shared=Proxy(Assembly.Load("Dalamud").GetType("Dalamud.Interface.Textures.ISharedImmediateTexture",true)!,(m,a)=> {
    if(m.Name=="TryGetWrap") { a[0]=wrap;a[1]=null;return true; }throw new InvalidOperationException("Unexpected texture fixture call: "+m.Name);
});
var provider=Proxy(Assembly.Load("Dalamud").GetType("Dalamud.Plugin.Services.ITextureProvider",true)!,(m,a)=> {
    if(m.Name=="GetFromManifestResource"&&Equals(a[1],"GillionsGameSync.Branding.png"))return shared;
    throw new InvalidOperationException("Unexpected texture provider call: "+m.Name);
});
Type T(string n)=>assembly.GetType("GillionsGameSync."+n,true)!;
const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
object New(string n,params object?[] values)=>Activator.CreateInstance(T(n),values)!;
bool testing=assembly.GetName().Name=="GillionsGameSyncTest";
Action noop=()=>{};
using var actualWindows=(IDisposable)Activator.CreateInstance(T("PublicGameSyncUi"),flags,null,
    [noop,noop,noop,noop,Activator.CreateInstance(T("HuntProgressState"),true)!,(Func<bool>)(()=>false),noop,false,testing],null)!;
var actualHuntWindow=(Window)T("PublicGameSyncUi").GetField("hunts",flags)!.GetValue(actualWindows)!;
var cases=new[]{
    ("01-pairing-welcome","DrawPairingPublic",false,460f,1f,0),
    ("02-pairing-connected","DrawPairingPublic",true,460f,1f,0),
    ("03-main-connected","DrawMainPublic",true,420f,1f,0),
    ("04-main-disconnected","DrawMainPublic",false,420f,1f,0),
    ("05-main-testing","DrawMainPublic",true,420f,1f,0),
    ("06-settings-general","DrawPublicSettings",true,520f,1f,0),
    ("07-settings-hunts","DrawPublicSettings",true,520f,1f,1),
    ("08-settings-connection","DrawPublicSettings",true,520f,1f,2),
    ("09-settings-advanced","DrawPublicSettings",true,520f,1f,3),
    ("10-privacy-site-link","DrawMainPublic",true,420f,1f,0),
    ("11-hunt-progress-multiple","DrawHuntProgress",true,300f,1f,0),
    ("12-hunt-progress-one-left","DrawHuntProgress",true,300f,1f,0),
    ("13-hunt-progress-complete","DrawHuntProgress",true,300f,1f,0),
    ("14-hunt-progress-updating","DrawHuntProgress",true,300f,1f,0),
    ("15-hunt-progress-all-complete","DrawHuntProgress",true,300f,1f,0),
    ("16-hunt-progress-long-name-scale","DrawHuntProgress",true,300f,1.5f,0),
    ("17-pairing-failure-recovery","DrawPairingPublic",false,460f,1f,0),
    ("18-paired-origin-next-different","DrawPairingPublic",true,460f,1f,0),
    ("19-invalid-next-pair-origin","DrawPairingPublic",false,460f,1f,0),
    ("20-pairing-connecting","DrawPairingPublic",false,460f,1f,0),
    ("21-hunt-progress-narrow-scroll","DrawHuntProgress",true,220f,1f,0),
    ("22-hunt-progress-wide-static","DrawHuntProgress",true,480f,1f,0),
    ("23-pairing-welcome-125","DrawPairingPublic",false,460f,1.25f,0),
    ("24-pairing-welcome-150","DrawPairingPublic",false,460f,1.5f,0),
    ("25-main-connected-125","DrawMainPublic",true,420f,1.25f,0),
    ("26-main-connected-150","DrawMainPublic",true,420f,1.5f,0),
    ("27-main-narrow","DrawMainPublic",true,320f,1f,0),
    ("28-settings-general-125","DrawPublicSettings",true,520f,1.25f,0),
    ("29-settings-hunts-150","DrawPublicSettings",true,520f,1.5f,1),
    ("30-settings-connection-125","DrawPublicSettings",true,520f,1.25f,2),
    ("31-settings-advanced-150","DrawPublicSettings",true,520f,1.5f,3),
    ("32-hunt-progress-duplicate-names","DrawHuntProgress",true,300f,1f,0),
    ("33-hunt-progress-125","DrawHuntProgress",true,300f,1.25f,0),
    ("34-pairing-narrow","DrawPairingPublic",false,320f,1f,0),
    ("35-main-waiting-context","DrawMainPublic",true,420f,1f,0),
    ("36-main-update-required","DrawMainPublic",true,420f,1f,0),
    ("37-main-site-unavailable","DrawMainPublic",true,420f,1f,0),
    ("38-main-wide","DrawMainPublic",true,560f,1f,0),
};
foreach(var (name,method,paired,width,scale,tab) in cases) {
    object plugin=RuntimeHelpers.GetUninitializedObject(T("Plugin"));
    void Set(string field,object? v)=>T("Plugin").GetField(field,flags)!.SetValue(plugin,v);
    Set("textureProvider",provider);
    var config=Activator.CreateInstance(T("PluginConfiguration"))!;
    config.GetType().GetProperty("ShowHuntProgress")!.SetValue(config,true);
    if(paired) config.GetType().GetProperty("ActiveSession")!.SetValue(config,New("PairedSession",1,testing?"https://test.gillions.app":"https://gillions.app","synthetic-device","synthetic-generation","synthetic-fingerprint"));
    Set("configuration",config);Set("uiPairingCode","");Set("uiServerAddress",testing?"https://test.gillions.app":"https://gillions.app");
    if(name.StartsWith("18-")) {
        config.GetType().GetProperty("ActiveSession")!.SetValue(config,New("PairedSession",1,"https://gillions.app","synthetic-device","synthetic-generation","synthetic-fingerprint"));
        Set("pairingRepair",true);Set("uiServerAddress","https://test.gillions.app");
    }
    if(name.StartsWith("19-"))Set("uiServerAddress","http://invalid.example/synthetic-secret");
    var health=New("PublicHealth",paired?"Connected":"Not connected",paired?"Example Character · Example World":"Character unavailable",
        paired?"Ready":"Gillions unavailable",paired?"Ready":"Gillions unavailable",paired?"Ready":"Gillions unavailable",paired?"Ready":"Gillions unavailable",
        paired?new DateTime(2026,10,7,12,0,0,DateTimeKind.Utc):null,testing?"Testing":"Public",assembly.GetName().Version!.ToString(4),"2026.09.15.0000.0000");
    if(name.StartsWith("35-")) foreach(string field in new[]{"Hunts","PartyFinder","Fates","Market"})
        health.GetType().GetProperty(field)!.SetValue(health,"Waiting for supported context");
    if(name.StartsWith("36-"))health.GetType().GetProperty("Connection")!.SetValue(health,"Update required");
    if(name.StartsWith("37-"))health.GetType().GetProperty("Connection")!.SetValue(health,"Gillions unavailable");
    Set("publicHealth",health);
    var ui=T("PluginUiSnapshot").GetField("Empty",BindingFlags.Public|BindingFlags.Static)!.GetValue(null)!;
    ui=ui.GetType().GetMethod("<Clone>$")!.Invoke(ui,[])!;
    ui.GetType().GetProperty("Paired")!.SetValue(ui,paired);Set("uiState",ui);
    if(name.StartsWith("17-"))ui.GetType().GetProperty("Message")!.SetValue(ui,"Gillions could not complete the request. Pending records were kept; please try again.");
    if(name.StartsWith("20-"))ui.GetType().GetProperty("Pairing")!.SetValue(ui,true);
    var model=Activator.CreateInstance(T("HuntProgressState"),true)!;
    var rows=Array.CreateInstance(T("HuntProgressRow"),name.Contains("scroll")||name.Contains("wide-static")?10:name.Contains("duplicate-names")?3:name.Contains("multiple")?2:name.Contains("updating")||name.Contains("all-complete")?0:1);
    for(int i=0;i<rows.Length;i++) rows.SetValue(New("HuntProgressRow","fixture-"+i,
        name.Contains("duplicate-names")?(i==1?"Horned lizard":"Aspis"):name.Contains("long-name")?"An exceptionally long localized-like Hunt target name that wraps on multiple lines":i==0?"Example hunt target":"Another example hunt target",
        name.Contains("complete")?3:name.Contains("one-left")?2:1,3),i);
    T("HuntProgressState").GetProperty("Display",flags)!.SetValue(model,New("HuntProgressDisplay","Example current area",
        name.Contains("updating")?"Hunt progress updating…":name.Contains("all-complete")?"All current Hunt targets here are complete.":"",rows));
    Set("huntProgress",model);
    Render(name,plugin,T("Plugin").GetMethod(method,flags)!,width,scale,tab);
}
File.WriteAllText(Path.Combine(output,"controlled-render.json"),JsonSerializer.Serialize(new{
    mode="CONTROLLED OFFLINE: actual compiled Draw methods and exact Hunt window flags; synthetic state, installed Dalamud Noto Sans font and default ImGui style. Hunt geometry checks simulate resize and close/reopen in ImGui only, not OS input. Not FFXIV screenshots, runtime admission, installed-game focus, keyboard, WindowSystem placement or live performance proof.",
    product=assembly.GetName().Name,version=assembly.GetName().Version!.ToString(4),dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(binary))).ToLowerInvariant(),
    allComplete="Model-only fixture: runtime cannot prove exact current-order/reset coverage, so all-complete remains fail-closed.",cases=cases.Select(c=>c.Item1)
},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Controlled UI evidence: {cases.Length} compiled states; no game/services/network/Windows input.");

unsafe void Render(string name,object plugin,MethodInfo draw,float width,float scale,int tab) {
    ImGui.CreateContext();
    try {
        var io=ImGui.GetIO();io.DisplaySize=new Vector2(1280,1400);io.DeltaTime=1f/60;
        io.IniFilename=null;io.LogFilename=null;io.FontGlobalScale=scale;
        string font=Path.GetFullPath(Path.Combine(libs,"../../../dalamudAssets/dev/UIRes/NotoSansCJKjp-Medium.otf"));
        if(!File.Exists(font))throw new FileNotFoundException("Installed Dalamud font required; do not substitute a missing-glyph preview.",font);
        ushort* ranges=stackalloc ushort[]{0x20,0xff,0x2000,0x206f,0x2713,0x2713,0};
        io.Fonts.AddFontFromFileTTF(font,17f,null,ranges);io.Fonts.Build();
        byte* pixels=null;int w=0,h=0,bpp=0;
        io.Fonts.GetTexDataAsRGBA32(0,&pixels,&w,&h,&bpp);io.Fonts.SetTexID(0,new ImTextureID(1ul));
        var atlas=new byte[w*h*bpp];Marshal.Copy((IntPtr)pixels,atlas,0,atlas.Length);
        File.WriteAllBytes(Path.Combine(output,"font.rgba"),atlas);
        ImGui.StyleColorsDark();ImGui.GetStyle().ScaleAllSizes(scale);
        string title=draw.Name=="DrawHuntProgress"?"Hunt Progress":draw.Name=="DrawPublicSettings"?"Gillions Settings":draw.Name=="DrawPairingPublic"?"Welcome to Gillions":"Gillions Game Sync";
        if(testing&&draw.Name!="DrawHuntProgress")title+=" [TESTING]";
        // Synthetic ImGui IO restricted to the Settings tab strip, not OS input.
        bool hunt=draw.Name=="DrawHuntProgress";
        var chosenSize=new Vector2(width*scale,(name.Contains("duplicate-names")?300:180)*scale);
        var progressModel=hunt?T("Plugin").GetField("huntProgress",flags)!.GetValue(plugin):null;
        var displayProperty=T("HuntProgressState").GetProperty("Display",flags)!;
        var originalDisplay=hunt?displayProperty.GetValue(progressModel):null;
        float scrollMax=0;
        for(int frame=0;frame<7;frame++) {
            if(tab>0&&frame>0){
                var style=ImGui.GetStyle();string[] labels=["General","Hunts","Connection","Advanced"];
                float x=24+style.WindowPadding.X;
                for(int i=0;i<tab;i++)x+=ImGui.CalcTextSize(labels[i]).X+style.FramePadding.X*2+style.ItemInnerSpacing.X;
                x+=ImGui.CalcTextSize(labels[tab]).X/2+style.FramePadding.X;
                float line=ImGui.GetFontSize();
                float y=24+style.FramePadding.Y*2+line+style.WindowPadding.Y+20*scale+style.ItemSpacing.Y+(style.FramePadding.Y*2+line)/2;
                io.AddMousePosEvent(x,y);io.AddMouseButtonEvent(0,frame==2);
            }
            ImGui.NewFrame();
            if(hunt&&frame==3) { ImGui.Render();continue; } // closed for one frame
            if(hunt&&frame==4) displayProperty.SetValue(progressModel,New("HuntProgressDisplay","Example current area","Hunt progress updating…",Array.CreateInstance(T("HuntProgressRow"),0)));
            if(hunt&&frame==5) displayProperty.SetValue(progressModel,originalDisplay);
            ImGui.SetNextWindowPos(new Vector2(24,24),ImGuiCond.Always);
            if(hunt) {
                var constraints=actualHuntWindow.SizeConstraints!.Value;
                ImGui.SetNextWindowSizeConstraints(constraints.MinimumSize*scale,constraints.MaximumSize);
                ImGui.SetNextWindowSize(frame==2?chosenSize:actualHuntWindow.Size!.Value*scale,
                    frame==2?ImGuiCond.Always:actualHuntWindow.SizeCondition);
            } else ImGui.SetNextWindowSize(new Vector2(width*scale,0),ImGuiCond.Always);
            bool open=true;
            if(ImGui.Begin(hunt?actualHuntWindow.WindowName:title,ref open,hunt?actualHuntWindow.Flags:ImGuiWindowFlags.AlwaysAutoResize))draw.Invoke(plugin,[]);
            if(hunt&&frame>=4) {
                var size=ImGui.GetWindowSize();
                if(Vector2.Distance(size,chosenSize)>1)throw new InvalidOperationException("Hunt content/reopen reset the user's chosen size: "+name);
                scrollMax=ImGui.GetScrollMaxY();
            }
            ImGui.End();ImGui.Render();
        }
        if(name.Contains("scroll")&&scrollMax<=0)throw new InvalidOperationException("Small Hunt viewport must scroll rather than auto-grow.");
        var data=ImGui.GetDrawData();var triangles=new List<object>();
        for(int list=0;list<data.CmdListsCount;list++) {
            var commands=new ImDrawListPtr(data.CmdLists[list]);
            for(int i=0;i<commands.CmdBuffer.Size;i++) {
                var cmd=commands.CmdBuffer[i];
                if(cmd.UserCallback!=null)throw new InvalidOperationException("Custom callbacks cannot be certified by this controlled renderer.");
                if(cmd.TextureId.Handle is not (1ul or 2ul))throw new InvalidOperationException("Unexpected texture: do not silently omit it.");
                for(int index=0;index<cmd.ElemCount;index+=3) {
                    var vertices=new List<float[]>();
                    for(int j=0;j<3;j++) {
                        var v=commands.VtxBuffer[(int)cmd.VtxOffset+commands.IdxBuffer[(int)cmd.IdxOffset+index+j]];
                        vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col&255,(v.Col>>8)&255,(v.Col>>16)&255,v.Col>>24]);
                    }
                    triangles.Add(new{texture=cmd.TextureId.Handle,clip=new[]{cmd.ClipRect.X,cmd.ClipRect.Y,cmd.ClipRect.Z,cmd.ClipRect.W},v=vertices});
                }
            }
        }
        File.WriteAllText(Path.Combine(output,name+".draw.json"),JsonSerializer.Serialize(new{width=1280,height=1400,atlasWidth=w,atlasHeight=h,triangles}));
    }finally{ImGui.DestroyContext();}
}

public class TextureFixtureProxy : DispatchProxy {
    internal Func<MethodInfo,object?[],object?> Handler=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args!);
}

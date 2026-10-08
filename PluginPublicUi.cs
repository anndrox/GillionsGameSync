using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly PublicGameSyncUi publicUi;
    private readonly ITextureProvider textureProvider;
    private readonly HuntProgressState huntProgress = new();
    private DateTime nextPublicUiUtc;
    private bool pairingRepair;
    private string publicConnectionFailure = "";
    private bool supportCopyAcknowledged;
    private void ObserveConnectionFailure(Exception error) {
        publicConnectionFailure = error is GillionsSyncRejectedException r ? r.Code switch {
            "DEVICE_INVALID" or "DEVICE_REVOKED" or "TOKEN_INVALID" => "DEVICE_INVALID",
            "UNSUPPORTED_CLIENT" or "VERSION_UNSUPPORTED" or "CLIENT_VERSION_UNSUPPORTED" => "VERSION_UNSUPPORTED",
            "ACCOUNT_DISABLED" or "TRIAL_EXPIRED" => r.Code,
            _ => "UNAVAILABLE"
        } : "UNAVAILABLE";
    }
    private volatile PublicHealth publicHealth = new("Not connected","Character unavailable","Waiting for supported context",
        "Waiting for supported context","Waiting for supported context","Waiting for supported context",null,"Public","","unavailable");
#if GILLIONS_TEST_BUILD
    private const bool IsTesting=true;
#else
    private const bool IsTesting=false;
#endif
    private string PrivacyUrl => (SyncOrigin.TryNormalize(configuration.ActiveSession?.Origin,out var current) ? current : GillionsEndpoints.DefaultServerUrl)+"/gillions-sync";
    private void DrawActionFeedback(bool pairing=false) {
        string message=PublicConnectionPresentation.ActionMessage(uiState.Message,pairing);
        if(message.Length>0) Label(message);
    }
    private void PublishPublicHealth(DateTime now) {
        string code=publicConnectionFailure.Length>0 ? publicConnectionFailure : configuration.SyncBlockedCode;
        string connection=code=="UNAVAILABLE" ? "Gillions unavailable" : PublicHealth.ConnectionState(HasPairedSession,pairingInFlight,code);
        string character=activeOwnedState is { } c ? $"{c.CharacterName} · {c.CharacterWorld}" : "Character unavailable";
        string waiting=HasPairedSession ? "Waiting for supported context" : "Gillions unavailable";
        string game=dataManager.GameData.Repositories.TryGetValue("ffxiv",out var repo) ? repo.Version : "unavailable";
        string hunts=waiting, fates=waiting, market=waiting;
        if (HasPairedSession) hunts=!PersonalObservationCompatibility.Supports(game,typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.MobHunt).Assembly.GetName().Version?.ToString())
            ? "Update required" : huntLocal.Progress.ExpiresAt>Environment.TickCount64 && huntLocal.Progress.Availability.Length==0 ? "Ready" : "Waiting for supported context";
#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD
        if (HasPairedSession) {
            fates=PublicHealth.FateState(FatePolicy.Compatible(fateLocal.ObservedSource),fateSender.Grant?.Reason,
                fateSender.Grant?.Admission.Granted,fateSender.Grant?.PolicyEnabled);
            market=ExplicitPermission("marketContribution") && !MarketEnabled ? "Off on Gillions"
                : MarketEnabled && marketAcceptedGeneration.Length>0 ? "Ready" : "Temporarily unavailable";
        }
#endif
        publicHealth=new(connection,character,hunts,HasPairedSession ? "Ready" : waiting,fates,market,
            activeOwnedState?.LastSyncUtc,IsTesting ? "Testing" : "Public",PluginVersion,game);
    }
    private static void Label(string text)=>ImGui.TextWrapped(text);
    private void DrawBranding(float size) {
        // A shallow, non-interactive band around the approved coin/Aetheryte
        // asset. Keep its full square aspect ratio at the current font scale.
        // Shared resource loading owns lifetime; no download or new artwork.
        float scale=ImGui.GetFontSize()/17f;
        var start=ImGui.GetCursorScreenPos();
        var band=new Vector2(ImGui.GetContentRegionAvail().X,size*scale);
        var ink=ImGui.GetWindowDrawList();
        ink.AddRectFilled(start,start+band,ImGui.GetColorU32(new Vector4(.055f,.10f,.16f,1)));
        ink.AddLine(start+new Vector2(0,band.Y),start+band,
            ImGui.GetColorU32(new Vector4(.68f,.52f,.24f,1)),scale);
        if(textureProvider?.GetFromManifestResource(typeof(Plugin).Assembly,"GillionsGameSync.Branding.png").TryGetWrap(out var icon,out _) == true) {
            ImGui.Image(icon.Handle,new Vector2(band.Y,band.Y));
        }
        ImGui.SetCursorScreenPos(start+new Vector2(band.Y+8*scale,(band.Y-ImGui.GetFontSize())/2));
        ImGui.TextColored(new Vector4(.92f,.78f,.48f,1),"Gillions Game Sync");
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(band);
    }
    private static void DrawConnection(string state) {
        // Emphasis is redundant with readable text, never a color-only state.
        ImGui.TextColored(state=="Connected" ? new Vector4(.54f,.86f,.87f,1) : ImGui.GetStyle().Colors[(int)ImGuiCol.Text],
            state=="Connected" ? "Connected to Gillions" : state);
    }
    private void DrawPairingBranding() {
        // First-run composition: two-line identity, deliberate negative space,
        // and one full approved coin/Aetheryte accent at right. No new artwork.
        float scale=ImGui.GetFontSize()/17f;
        var start=ImGui.GetCursorScreenPos();
        var band=new Vector2(ImGui.GetContentRegionAvail().X,56*scale);
        var ink=ImGui.GetWindowDrawList();
        ink.AddRectFilled(start,start+band,ImGui.GetColorU32(new Vector4(.055f,.10f,.16f,1)));
        ink.AddLine(start+new Vector2(0,band.Y),start+band,
            ImGui.GetColorU32(new Vector4(.68f,.52f,.24f,1)),scale);
        ink.AddText(ImGui.GetFont(),23*scale,start+new Vector2(12,2)*scale,
            ImGui.GetColorU32(new Vector4(.92f,.78f,.48f,1)),"GILLIONS");
        ink.AddText(ImGui.GetFont(),17*scale,start+new Vector2(12,30)*scale,
            ImGui.GetColorU32(new Vector4(.54f,.86f,.87f,1)),"Game Sync");
        if(textureProvider?.GetFromManifestResource(typeof(Plugin).Assembly,"GillionsGameSync.Branding.png").TryGetWrap(out var icon,out _) == true) {
            ImGui.SetCursorScreenPos(start+new Vector2(band.X-56*scale,4*scale));
            ImGui.Image(icon.Handle,new Vector2(48*scale,48*scale));
        }
        ImGui.SetCursorScreenPos(start); ImGui.Dummy(band);
    }
    private static void DrawFeatureHealth(PublicHealth health,bool quietGlobal=false) {
        // Fixed label column, wrapping value column: no consent controls.
        if(!ImGui.BeginTable("FeatureHealth",2,ImGuiTableFlags.SizingFixedFit)) return;
        ImGui.TableSetupColumn("Feature",ImGuiTableColumnFlags.WidthFixed,105*ImGui.GetFontSize()/17f);
        ImGui.TableSetupColumn("Health",ImGuiTableColumnFlags.WidthStretch);
        DrawFeatureRow("Hunts",quietGlobal ? "Unavailable" : health.Hunts);
        DrawFeatureRow("Party Finder",quietGlobal ? "Unavailable" : health.PartyFinder);
        DrawFeatureRow("FATEs",quietGlobal ? "Unavailable" : health.Fates);
        DrawFeatureRow("Market",quietGlobal ? "Unavailable" : health.Market);
        ImGui.EndTable();
    }
    private static void DrawFeatureRow(string name,string status) {
        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted(name);
        ImGui.TableNextColumn(); Label(status);
    }
    private static void Link(string label,string url) { if(ImGui.Button(label)) Util.OpenLink(url); }
    private void DrawPrivacy() {
        Label("Privacy and data controls are managed on Gillions.");
        Link("Open privacy & data settings",PrivacyUrl);
    }
    private void DrawMainPublic() {
        var h=publicHealth;
        DrawBranding(32); ImGui.Separator(); DrawConnection(h.Connection); Label(h.Character);
        if(h.LastSync is { } t) ImGui.TextDisabled($"Last sync: {t.ToLocalTime():g}");
        else Label("Waiting for first sync");
        if(uiState.Model.Warning is { } warning) Label(warning);
        if(h.Connection=="Not connected" || h.Connection=="Authorization expired or revoked") {
            Label("Connect Game Sync to your Gillions account to use supported current-game features.");
            if(ImGui.Button("Connect to Gillions")) { pairingRepair=HasPairedSession; publicUi.ShowPairing(); }
        } else if(h.Connection=="Update required") Link("Update Game Sync","https://github.com/anndrox/GillionsGameSync");
        else if(h.Connection=="Gillions unavailable") Label("Gillions is temporarily unreachable. Local history is preserved; Game Sync will retry safely.");
        else if(h.Connection=="Account unavailable") Label("Check your Gillions account access on the website.");
        ImGui.Separator();
        bool sharedUnavailable=h.Connection is "Not connected" or "Gillions unavailable" or "Authorization expired or revoked" or "Account unavailable" or "Update required"
            && h.Hunts=="Gillions unavailable" && h.PartyFinder=="Gillions unavailable"
            && h.Fates=="Gillions unavailable" && h.Market=="Gillions unavailable";
        DrawFeatureHealth(h,sharedUnavailable);
        ImGui.Separator(); DrawPrivacy();
        Link("Open Gillions",configuration.ActiveSession?.Origin ?? GillionsEndpoints.DefaultServerUrl);
        ImGui.SameLine();
        if(ImGui.Button("Settings")) publicUi.ShowSettings();
    }
    private void DrawPairingPublic() {
        DrawPairingBranding(); ImGui.Separator();
        if(uiState.Paired && !pairingRepair) {
            Label("Game Sync is ready"); Label("Connected as:"); Label(publicHealth.Character); DrawPrivacy();
            Link("Open Gillions",configuration.ActiveSession?.Origin ?? GillionsEndpoints.DefaultServerUrl);
            if(ImGui.Button("Finish")) { publicUi.FinishPairing(); QueueUiAction(()=> { configuration.OnboardingCompleted=true; RequestConfigurationSave(); }); }
            return;
        }
        Label("Welcome to Gillions Game Sync");
        Label("Connect supported current FFXIV information to Gillions so Hunts and other companion tools stay useful while you play.");
        Label("Some public-world observations may be contributed according to your Gillions account settings.");
        Label("Game Sync does not move your character, fight, teleport or automatically join parties.");
        string nextOrigin=PublicConnectionPresentation.PairingOrigin(uiServerAddress);
        if(nextOrigin.Length>0) {
            if(pairingRepair) Label("Next pairing destination: "+nextOrigin);
            else Label("You’ll continue on "+(nextOrigin.StartsWith("https://",StringComparison.Ordinal) ? nextOrigin[8..] : nextOrigin)+" to connect this device.");
            Link("Connect to Gillions",PublicConnectionPresentation.PairingUrl(uiServerAddress));
            Link("Learn about data & privacy",nextOrigin+"/gillions-sync");
        } else {
            Label("The saved pairing destination is invalid. Choose the approved default before entering a code.");
            if(ImGui.Button("Use default pairing destination")) {
                uiServerAddress=IsTesting ? "https://test.gillions.app" : GillionsEndpoints.DefaultServerUrl;
                string destination=uiServerAddress;
                QueueUiAction(()=> { configuration.ServerUrl=destination; RequestConfigurationSave(); });
            }
        }
        Label(uiState.Pairing ? "Waiting for Gillions authorization…" : publicHealth.Connection);
        DrawActionFeedback(pairing:true);
        // Leave room for the established visible field label in narrow windows.
        // Field identity, password masking and pairing actions stay unchanged.
        ImGui.PushItemWidth(Math.Max(80*ImGui.GetFontSize()/17f,
            ImGui.GetContentRegionAvail().X-ImGui.CalcTextSize("Pairing code").X-ImGui.GetStyle().ItemInnerSpacing.X));
        DrawPairingControls(uiState);
        ImGui.PopItemWidth();
    }
    private void DrawPublicSettings() {
        DrawBranding(20);
        if(!ImGui.BeginTabBar("GillionsSettingsTabs")) return;
        if(ImGui.BeginTabItem("General")) {
            Label("Window placement follows Dalamud. These compact windows size to their content. Game Sync never automates gameplay.");
            ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Hunts")) {
            bool show=configuration.ShowHuntProgress, locked=configuration.LockHuntProgressPosition;
            if(ImGui.Checkbox("Show Hunt Progress",ref show)) QueueUiAction(()=> { configuration.ShowHuntProgress=show; RequestConfigurationSave(); });
            if(ImGui.Checkbox("Lock position",ref locked)) QueueUiAction(()=> { configuration.LockHuntProgressPosition=locked; RequestConfigurationSave(); });
            Label("A local, display-only count panel for your current area. Closing it suppresses auto-show until you change territory.");
            if(show && ImGui.Button("Open Hunt Progress")) QueueUiAction(()=>huntProgress.Open());
            ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Connection")) {
            DrawConnection(publicHealth.Connection); Label(publicHealth.Character); DrawPrivacy();
            DrawActionFeedback();
            Link("Open Gillions",configuration.ActiveSession?.Origin ?? GillionsEndpoints.DefaultServerUrl);
            if(ImGui.Button("Reconnect")) { pairingRepair=true; publicUi.ShowPairing(); }
            if(uiState.Paired) { ImGui.Spacing(); ImGui.Separator(); }
            if(uiState.Paired && ImGui.Button("Disconnect Game Sync")) QueueUiAction(()=> {
                configuration.PairingRequired=true; configuration.ActiveSession=null;
                configuration.DeviceToken=""; configuration.DeviceId=""; configuration.PairingCode="";
                ResetSessionContext(); RequestConfigurationSave(); settingsMessage="Disconnected. Saved history remains on this PC.";
            });
            ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Advanced")) {
            ImGui.TextDisabled("Support information"); ImGui.Separator();
            Label("Game Sync "+PluginVersion+" · Channel: "+publicHealth.Channel);
            Label(publicHealth.Connection); Label("Game: "+publicHealth.GameVersion);
            DrawFeatureHealth(publicHealth);
            ImGui.Separator(); ImGui.TextDisabled("Recent sync & support");
            Label(publicHealth.LastSync is { } last ? $"Last successful sync: {last.ToLocalTime():g}" : "No successful sync recorded for this character.");
            if(ImGui.Button("Copy support summary")) { ImGui.SetClipboardText(publicHealth.SupportSummary()); supportCopyAcknowledged=true; }
            if(supportCopyAcknowledged) Label("Support summary copied.");
            DrawActionFeedback();
            if(!uiState.Model.CanSync) Label(!uiState.Paired ? "Connect to Gillions before syncing." : uiState.Model.Status.StartsWith("Log into",StringComparison.Ordinal) ? "Log into a character before syncing." : "A sync is already in progress. Please wait.");
            ImGui.BeginDisabled(!uiState.Model.CanSync);
            if(ImGui.Button("Sync now")) _=SyncAsync();
            ImGui.EndDisabled();
            ImGui.Separator(); ImGui.TextDisabled("Data providers");
            ImGui.TextUnformatted("Data provided by"); ImGui.SameLine();
            if(ImGui.SmallButton("xivpf.com")) Util.OpenLink("https://xivpf.com");
#if GILLIONS_TEST_BUILD
            if(ImGui.CollapsingHeader("Testing diagnostics")) {
                Label("Diagnostics stay local. Private exports contain gameplay details: do not share configuration or credentials.");
                if(ImGui.Button("FATE diagnostics")) fateLocal.Show();
                if(ImGui.Button("Hunt diagnostics")) huntLocal.Show();
                if(ImGui.Button("Submarine diagnostics")) submarineLocal.Show();
                if(ImGui.Button("Daily / weekly facts diagnostics")) dashboardLocal.Show();
                if(ImGui.Button("Beastmaster diagnostics")) beastmasterLocal.Show();
                // Origin selection is Testing-only connection setup, not a policy editor.
                if(!uiState.Paired && ImGui.Button("Use shared TEST for next pairing")) {
                    uiServerAddress=PersonalSyncPolicy.Origin;
                    QueueUiAction(()=> { configuration.ServerUrl=PersonalSyncPolicy.Origin; RequestConfigurationSave(); });
                }
            }
#endif
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
    private void DrawHuntProgress() {
        var display=huntProgress.Display;
        Label(display.Area);
        if(display.Status.Length>0) Label(display.Status);
        foreach(var row in display.Rows) {
            ImGui.Separator(); Label(row.Name);
            Label(row.Complete ? "✓ Complete" : $"{row.Remaining} left");
            if(!row.Complete) ImGui.TextDisabled($"{row.Current} / {row.Required}");
        }
    }
}

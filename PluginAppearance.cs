#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private CharacterAppearanceCollector appearanceCollector=null!;
    private readonly AppearanceQueue appearanceQueue=new();
    private CancellationTokenSource appearanceCancellation=new();
    private string appearanceBinding="", appearanceStatus="Appearance sharing is off. No appearance reads.";
    private string? appearanceGrant;
    private long appearanceCharacterId;
    private bool appearanceKnown,appearanceAllowed,appearanceFlight;
    private DateTime appearanceLease,appearanceNextRead,appearanceNextSend;
    private string AppearanceKey => HasPairedSession&&activeRetainerCharacterContentId!=0?$"{configuration.ActiveSession!.Generation}:{activeRetainerCharacterContentId}":"";
    private AppearanceChoice? AppearanceIntent => configuration.CharacterAppearanceChoices.GetValueOrDefault(AppearanceKey);
    private void ClearAppearance(){appearanceCancellation.Cancel();appearanceQueue.Clear();}
    private void ResetAppearance(){ClearAppearance();appearanceBinding="";appearanceKnown=false;appearanceAllowed=false;appearanceGrant=null;appearanceLease=DateTime.MinValue;}
    private bool AppearancePaired => HasPairedSession&&activeOwnedState is not null&&NativeProduct.TransportOrigin(configuration.ActiveSession?.Origin);
    private bool AppearanceAuthorized(DateTime now)=>AppearancePaired&&AppearanceIntent is {Enabled:true,Pending:false}&&appearanceKnown&&appearanceAllowed&&appearanceGrant is not null&&now<appearanceLease;
    private bool ApplyAppearance(string json,SyncRequestPermit permit) {
        if(!PermitIsCurrent(permit)||!AppearancePaired)return false;
        try {
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});
            var a=doc.RootElement.GetProperty("characterAppearance");
            if(a.GetProperty("contract").GetString()!=AppearancePolicy.Contract||a.GetProperty("resourceType").GetString()!=AppearancePolicy.Resource
                ||a.GetProperty("capability").GetString()!=AppearancePolicy.Capability||a.GetProperty("permission").GetString()!=AppearancePolicy.Permission
                ||a.GetProperty("maxPayloadBytes").GetInt32()!=16384||a.GetProperty("maxEnvelopeBytes").GetInt32()!=20480
                ||a.GetProperty("characterId").GetInt64()<=0)throw new JsonException();
            var generation=a.GetProperty("generation").GetString();var allowed=a.GetProperty("enabled").GetBoolean();
            if(generation is not null&&!Guid.TryParseExact(generation,"D",out _)||allowed&&generation is null)throw new JsonException();
            if(generation!=appearanceGrant||!allowed)ClearAppearance();
            appearanceGrant=generation;appearanceAllowed=allowed;appearanceKnown=true;appearanceLease=DateTime.UtcNow.AddSeconds(25);
            appearanceCharacterId=a.GetProperty("characterId").GetInt64();
            return true;
        }catch(Exception e) when(e is JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or FormatException){appearanceKnown=false;appearanceAllowed=false;ClearAppearance();return false;}
    }
    private void DrawAppearanceSettings(){
        if(!publicUi.Visible)return;
        if(ImGui.Begin("My character appearance")){
            var choice=AppearanceIntent;var enabled=choice?.Enabled==true;
            ImGui.BeginDisabled(!AppearancePaired||(!appearanceKnown&&!enabled));
            if(ImGui.Checkbox(AppearancePolicy.Setting,ref enabled)){
                var requested=enabled;var binding=AppearanceKey;var expected=appearanceGrant;
                QueueUiAction(()=>{
                    if(binding.Length==0||binding!=AppearanceKey)return;
                    ClearAppearance();
                    configuration.CharacterAppearanceChoices[binding]=new(){Enabled=requested,Pending=true,Nonce=Guid.NewGuid().ToString("N"),ExpectedGeneration=expected,CharacterId=appearanceCharacterId};
                    configuration.Save(pluginInterface); // persist opt-out control before dispatch; no appearance data
                    appearanceNextSend=DateTime.MinValue;
                });
            }
            ImGui.EndDisabled();ImGui.TextWrapped(AppearancePolicy.Explanation);ImGui.TextWrapped(appearanceStatus);
        }
        ImGui.End();
    }
    private void TickAppearance(DateTime now){
        var binding=AppearanceKey;
        if(binding!=appearanceBinding){ResetAppearance();appearanceBinding=binding;}
        var intent=AppearanceIntent;
        if(!AppearancePaired||intent is null){ClearAppearance();appearanceStatus="Appearance sharing is off for this character. No appearance reads.";return;}
        if(intent.Pending){
            ClearAppearance();appearanceStatus=intent.Enabled?"Waiting for Gillions to accept appearance sharing.":"Sharing is off. Removal will finish when Gillions is reachable.";
            if(!appearanceFlight&&appearanceKnown&&now>=appearanceNextSend){appearanceFlight=true;appearanceNextSend=now.AddSeconds(60);var permit=CapturePermit(SyncRequestMode.Personal);_ = SendAppearanceConsent(permit,intent);}
            return;
        }
        if(!AppearanceAuthorized(now)){ClearAppearance();appearanceStatus=intent.Enabled?"Appearance sharing is paused or removed on Gillions. Turn it off and on to share again.":"Appearance sharing is off. No appearance reads.";return;}
        if(!appearanceCollector.Qualified){ClearAppearance();appearanceStatus="Appearance sharing paused after a game or SDK change. Waiting for qualification.";return;}
        if(!appearanceCollector.Available(activeRetainerCharacterContentId)){ClearAppearance();appearanceStatus="Appearance unavailable during a transition, GPose or transformation.";return;}
        if(now>=appearanceNextRead){
            appearanceNextRead=now.AddSeconds(5);
            try{var snapshot=appearanceCollector.Read(activeRetainerCharacterContentId,now);if(snapshot is null){ClearAppearance();appearanceStatus="Equipped appearance is not available yet.";return;}appearanceQueue.Observe(snapshot);}
            catch(Exception){ClearAppearance();appearanceStatus="Equipped appearance is not available yet.";return;}
        }
        if(!appearanceFlight&&appearanceQueue.Pending is { } p&&now>=appearanceNextSend){
            appearanceFlight=true;appearanceNextSend=now.AddSeconds(5);
            if(appearanceCancellation.IsCancellationRequested){appearanceCancellation.Dispose();appearanceCancellation=new();}
            var permit=CapturePermit(SyncRequestMode.Personal);var grant=appearanceGrant!;var cancellation=appearanceCancellation.Token;
            _ = SendAppearance(permit,p,grant,cancellation);
        }
    }
    private static void AppearanceHeaders(HttpRequestMessage r){r.Headers.Add("X-Gillions-Personal-Contract",AppearancePolicy.Contract);r.Headers.Add("X-Gillions-Personal-Resource",AppearancePolicy.Resource);r.Headers.Add("X-Gillions-Personal-Capability",AppearancePolicy.Capability);}
    private async Task SendAppearanceConsent(SyncRequestPermit permit,AppearanceChoice choice){
        try{
            using var request=Request("/api/game-sync/appearance-permission",permit,new {enabled=choice.Enabled,expectedGeneration=choice.ExpectedGeneration,requestNonce=choice.Nonce,characterId=choice.CharacterId});AppearanceHeaders(request);
            Task<HttpResponseMessage>? pending=null;
            await framework.RunOnFrameworkThread(()=>{RequirePermit(permit);if(!ReferenceEquals(AppearanceIntent,choice))throw new OperationCanceledException();pending=personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,permit.Cancellation);});
            using var response=await pending!;
            var json=response.IsSuccessStatusCode?await SyncResponsePolicy.ReadAsync(response.Content,permit.Cancellation):null;
            await framework.RunOnFrameworkThread(()=>{
                if(!PermitIsCurrent(permit)||!ReferenceEquals(AppearanceIntent,choice))return;
                if(json is not null&&ApplyAppearance(json,permit)){
                    choice.Pending=false;if(choice.Enabled&&!appearanceAllowed)choice.Enabled=false;
                    configuration.Save(pluginInterface);appearanceNextSend=DateTime.MinValue;
                }else if((int)response.StatusCode is 400 or 403 or 409){
                    if(choice.Enabled){choice.Pending=false;choice.Enabled=false;configuration.Save(pluginInterface);}
                    appearanceKnown=false;ClearAppearance();appearanceStatus="Appearance permission changed. Reload the connection before sharing again.";
                }
            });
        }catch(Exception){/* No request, payload or private exception data is logged. Retry only the saved control. */}
        finally{if(!disposed)await framework.RunOnFrameworkThread(()=>appearanceFlight=false);}
    }
    private async Task SendAppearance(SyncRequestPermit permit,AppearancePreparation p,string grant,CancellationToken featureToken){
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation,featureToken);linked.CancelAfter(TimeSpan.FromSeconds(20));var token=linked.Token;
        try{
            using var request=SnapshotRequest("/api/game-sync/sync",permit,AppearancePolicy.Resource,p.Nonce,Encoding.UTF8.GetBytes(p.Payload));AppearanceHeaders(request);request.Headers.Add("X-Gillions-Appearance-Grant",grant);
            if(request.Content!.Headers.ContentLength>20480)throw new InvalidOperationException();
            Task<HttpResponseMessage>? pending=null;
            await framework.RunOnFrameworkThread(()=>{RequirePermit(permit);token.ThrowIfCancellationRequested();if(!AppearanceAuthorized(DateTime.UtcNow)||appearanceGrant!=grant||!appearanceCollector.Available(permit.ContentId))throw new OperationCanceledException();pending=personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);});
            using var response=await pending!;bool accepted=false;
            if((int)response.StatusCode is 200 or 201){using var d=JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(response.Content,token));var r=d.RootElement;accepted=r.GetProperty("ok").GetBoolean()&&r.GetProperty("nonce").GetString()==p.Nonce&&r.GetProperty("contentHash").GetString()==p.Hash;}
            await framework.RunOnFrameworkThread(()=>{
                if(!PermitIsCurrent(permit)||token.IsCancellationRequested||appearanceGrant!=grant)return;
                if(accepted){appearanceQueue.Accepted(p);appearanceStatus="Your current appearance was shared privately with Gillions.";}
                else {appearanceNextSend=DateTime.UtcNow.AddSeconds(60);if((int)response.StatusCode is >=400 and <500 && (int)response.StatusCode!=429){ClearAppearance();appearanceKnown=false;}appearanceStatus="Appearance upload paused. Waiting for a compatible connection.";}
            });
        }catch(Exception){if(!disposed)await framework.RunOnFrameworkThread(()=>{if(PermitIsCurrent(permit))appearanceNextSend=DateTime.UtcNow.AddSeconds(60);});}
        finally{if(!disposed)await framework.RunOnFrameworkThread(()=>appearanceFlight=false);}
    }
}
#endif

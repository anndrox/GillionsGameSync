using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GillionsGameSync;

// All I/O is in-process synthetic data. No sockets, native SDK or live settings.
internal sealed class FakeHandler : HttpMessageHandler {
    internal int Status=400;
    internal int Calls;
    internal Action? BeforeResponse;
    internal readonly List<string> Bodies=[];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
        token.ThrowIfCancellationRequested();Calls++;
        Bodies.Add(await request.Content!.ReadAsStringAsync(token));
        BeforeResponse?.Invoke();BeforeResponse=null;
        return new((HttpStatusCode)Status) { RequestMessage=request,Content=new StringContent(Status is >=200 and <=299
            ? JsonSerializer.Serialize(new {ok=true,snapshotId=Guid.NewGuid(),unchanged=false,receivedAt=DateTime.UtcNow}) : "synthetic terminal") };
    }
}
internal sealed class ImmediateFramework { internal Task RunOnFrameworkThread(Action action) {action();return Task.CompletedTask;} }
internal sealed class Noop {internal void SetEnabled(bool b){} internal void RefreshSession(object? o){} internal void ClearSession(){} internal void DecisionChanged(string? s){} internal void Clear(){} internal bool Supported=>false; internal void Apply(params object[] a){} internal void Debug(string s,params object[] a){} }
internal static class MarketContributor {internal static bool Compatible(string s)=>false;}
internal static class TravelSyncPolicy {internal const string Capability="travel_context_v1";internal static bool Compatible(string s,string o)=>false;}
internal static class HuntFocusState {internal const string Header="X-Synthetic-Focus",Contract="none";}
internal static class HuntMapTransport { internal static CancellationTokenSource Deadline(CancellationToken a,CancellationToken b)=>CancellationTokenSource.CreateLinkedTokenSource(a,b); }
internal sealed class WebsiteResponseClock {internal DateTime UtcNow=>DateTime.UtcNow;}
internal sealed record RetainerPresenceDocument();
public sealed record RetainerClientProfile(string ProductName,bool RequiresExplicitServerProductAcceptance);
internal static class RetainerClientPolicy {internal const int ContractVersion=1;}
internal static class RetainerPresencePolicy {internal static TimeSpan NextSuccessDelay(int n)=>TimeSpan.FromSeconds(15);internal static TimeSpan NextFailureDelay(int n,int j)=>TimeSpan.FromSeconds(30);}
internal sealed class FixtureConfiguration {
    internal PairedSession? ActiveSession;
    internal bool EnableItemLinkRequests,EnablePartyFinderLinkRequests,AutomaticallyShowHuntMap,ContributeObservedMarketData,ShareHuntRoutingLocation,SyncPersonalHunts=true,SyncPersonalSubmarines;
    internal HuntBillRetention HuntBills=new(){LocalRetentionEnabled=false};
    internal SubmarineRetention SubmarineVoyages=new();
    internal PersonalSyncState PersonalSync=new();
}
internal sealed class SubmarineRetention {internal bool LocalRetentionEnabled;}
internal sealed class LocalCoverage {
    internal HuntBillItemCoverage Coverage=new(); internal HuntBillRetentionPolicy History;
    internal LocalCoverage(HuntBillRetentionPolicy history)=>History=history;
    internal long CoverageEpoch=>Coverage.Epoch;
    internal string? CoveragePayload(string key,DateTime now)=>Coverage.Payload(History,key,now,Stopwatch.GetTimestamp());
}
public sealed partial class Plugin : IDisposable {
    private FixtureConfiguration configuration=new();
    private readonly SyncRequestLifetime lifetime=new();
    private readonly ImmediateFramework framework=new();
    private readonly Noop marketContributor=new(),partyFinderContributor=new(),travelLocal=new(),travelRefusals=new(),huntFocus=new(),log=new();
    private bool PartyFinderContributionEnabled=>false;
    private void ClearTravelPending(){}
    private void RequestConfigurationSave(){}
    private void RecordDiagnostic(string s){}
    private bool HasPairedSession=>configuration.ActiveSession is not null;
    private object? activeOwnedState=new();
    private ulong activeRetainerCharacterContentId=123;
    private HuntBillItemSync huntCoverageSync=new();
    private LocalCoverage huntLocal;
    private bool huntCoverageAccepted,disposed,personalInFlight,retainerUploadServerSupported,presenceInFlight,travelAccepted;
    private string marketAcceptedGeneration="",personalStatus="";
    private readonly HashSet<string> personalAccepted=[];
    private readonly Dictionary<string,CancellationTokenSource> personalCancellation=[];
    private int personalFailures,presenceFailureCount;
    private DateTime personalRetryUtc=DateTime.MinValue,nextPersonalUtc=DateTime.MinValue,nextRetainerPresenceUtc,nextHuntFocusPresenceUtc;
    private readonly HttpClient personalHttp;
    internal readonly FakeHandler Handler=new();
    private readonly RetainerClientProfile RetainerClient=new(NativeProduct.Name,true);
    private string nextPresence="";
    private string responseOrigin="https://gillions.app";
    private bool HuntFocusEligible()=>false;
    private bool HuntFocusActive(DateTime now)=>false;
    private WebsiteResponseClock CommandResponseClock(HttpResponseMessage r,SyncRequestPermit p,TimeSpan t)=>new();
    private void ClearConnectionFailure(bool presence){}
    private void ObserveConnectionFailure(Exception e){}
    private Task EnsureSuccessfulResponse(HttpResponseMessage r,CancellationToken t) {r.EnsureSuccessStatusCode();return Task.CompletedTask;}
    private Task<HttpResponseMessage> SendPresenceAsync(HttpRequestMessage r,SyncRequestPermit p,CancellationToken t) {
        t.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){
            RequestMessage=new HttpRequestMessage(HttpMethod.Post,responseOrigin+"/api/game-sync/presence"),Content=new StringContent(nextPresence)});
    }
    private HttpRequestMessage Request(string path,SyncRequestPermit p,object body)=>new(HttpMethod.Post,p.Origin+path){Content=new StringContent("{}")};
    private HttpRequestMessage SnapshotRequest(string path,SyncRequestPermit p,string resource,string nonce,byte[] payload)=>new(HttpMethod.Post,p.Origin+path){Content=new StringContent(JsonSerializer.Serialize(new{resourceType=resource,nonce,payload=JsonNode.Parse(payload)}),Encoding.UTF8,"application/json")};
    private bool PermitIsCurrent(SyncRequestPermit p)=>lifetime.Accepts(p,activeRetainerCharacterContentId,configuration.ActiveSession,"fixture-token-at-least-twenty-four",true,true);
    private void RequirePermit(SyncRequestPermit p) {if(!PermitIsCurrent(p))throw new OperationCanceledException();}
    private Task CommitAsync(SyncRequestPermit p,Action<object> change) {RequirePermit(p);change(activeOwnedState!);return Task.CompletedTask;}
    internal Plugin() {
        configuration.ActiveSession=PairedSession.Create("https://gillions.app","11111111-1111-4111-8111-111111111111","fixture-token-at-least-twenty-four");
        personalHttp=new(Handler);huntLocal=new(new HuntBillRetentionPolicy(configuration.HuntBills){SiteRetentionAuthorized=()=>ExplicitPermission("personalHunts")});
    }
    internal string Owner=>PersonalSyncPolicy.Owner(configuration.ActiveSession!.Generation,HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId));
    internal string Character=>HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId);
    internal SyncRequestPermit Permit()=>lifetime.Capture(SyncRequestMode.Personal,activeRetainerCharacterContentId,configuration.ActiveSession,configuration.ActiveSession!.Origin,"fixture-token-at-least-twenty-four");
    internal async Task Presence(string json,string origin="https://gillions.app") {nextPresence=json;responseOrigin=origin;ClearRetainerServerAcceptance();await SendRetainerPresenceAsync(Permit(),new());}
    internal PersonalPreparedSnapshot? Prepare(string payload) {
        // Same published freshness/permission filter and preparation call as TickPersonalSync.
        if(!PersonalEnabled("hunt_bills")||!huntCoverageAccepted||!personalAccepted.Contains("hunt_bills"))return null;
        var value=PermissionObservationFilter.FreshPersonal("hunt_bills",payload,FreshFrom("personalHunts"));
        return value is null?null:huntCoverageSync.Prepare(Owner,value,huntLocal.CoverageEpoch,DateTime.UtcNow,Stopwatch.GetTimestamp());
    }
    internal bool Enabled=>PersonalEnabled("hunt_bills");
    internal DateTime LatestIssued=>(DateTime)typeof(PermissionAuthority).GetField("latestIssued",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(permissionAuthority)!;
    internal string AuthorityState=>JsonSerializer.Serialize(new {
        issued=LatestIssued, identity=permissionAuthority.Identity("personalHunts",Environment.TickCount64), enrollment=permissionAuthority.Enrollment,
        expires=typeof(PermissionAuthority).GetField("expiresClock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(permissionAuthority)});
    internal string? SupportState=>((JsonElement?)typeof(HuntBillItemSync).GetField("support",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(huntCoverageSync))?.GetRawText();
    internal bool RecoverySpent=>(bool)typeof(HuntBillItemSync).GetField("supportRecoveryUsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(huntCoverageSync)!;
    internal void SetLive(HuntBillItemCoverage coverage,HuntBillRetentionPolicy history) {huntLocal.Coverage=coverage;huntLocal.History=history;}
    internal void Block(PersonalPreparedSnapshot p)=>huntCoverageSync.Block(p);
    internal async Task Send(PersonalPreparedSnapshot p,bool cancel=false) {using var c=new CancellationTokenSource();if(cancel)c.Cancel();await SendPersonalAsync(Permit(),p,c.Token,true);}
    internal void Revoke() {permissionAuthority.Invalidate();ReconcilePermissions(force:true);}
    internal void ChangeCharacter(ulong id) {lifetime.Invalidate();permissionAuthority.Bind("","");permissionTransitions.Clear();permissionFreshFrom.Clear();huntCoverageSync.Clear();huntLocal.Coverage.Clear();activeRetainerCharacterContentId=id;}
    internal void ChangeSession() {ChangeCharacter(activeRetainerCharacterContentId);configuration.ActiveSession=PairedSession.Create("https://gillions.app","11111111-1111-4111-8111-111111111111","fixture-token-at-least-twenty-four");}
    internal bool Current(PersonalPreparedSnapshot p)=>CoveragePreparedCurrent(p,Permit());
    public void Dispose() {disposed=true;lifetime.Dispose();personalHttp.Dispose();}
}

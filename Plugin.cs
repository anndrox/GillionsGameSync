using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Configuration;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

internal static class GillionsEndpoints {
    public static string DefaultServerUrl {
        get {
            var configured = typeof(Plugin).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, "GillionsPublicBaseUrl", StringComparison.Ordinal))
                ?.Value;
            return string.IsNullOrWhiteSpace(configured)
                ? throw new InvalidOperationException("GillionsPublicBaseUrl build metadata is missing.")
                : configured.TrimEnd('/');
        }
    }
}

public sealed class Plugin : IDalamudPlugin {
    public string Name => "Gillions Game Sync";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private readonly IDataManager dataManager;
    private readonly IUnlockState unlockState;
    private readonly IGameInventory gameInventory;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
#if GILLIONS_TEST_BUILD
    private readonly BeastmasterLocalView beastmasterLocal;
    private readonly SubmarineLocalView submarineLocal;
    private readonly HuntBillLocalView huntLocal;
    private readonly DashboardLocalView dashboardLocal;
    private readonly TravelContextLocalView travelLocal;
    private readonly TravelSyncState travelSync = new();
    private bool travelAccepted, travelInFlight;
    private string travelBinding = "";
    private volatile string travelStatus = "Travel sync OFF; no location uploads.";
    private readonly ICondition marketConditions;
    private readonly MarketContributor marketContributor;
    private readonly MarketContributionSource marketSource;
    private readonly HttpClient marketHttp = PartyFinderHttp.CreateClient();
    private string marketAcceptedGeneration = "";
    private DateTime nextMarketMaintenanceUtc;
    private readonly HttpClient personalHttp = PartyFinderHttp.CreateClient();
    private readonly HashSet<string> personalAccepted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> personalCancellation = new(StringComparer.Ordinal);
    private bool personalInFlight;
    private DateTime nextPersonalUtc;
    private DateTime personalRetryUtc;
    private int personalFailures;
    private bool observedPersonalHunts, observedPersonalSubmarines;
    private volatile string personalStatus = "Private Hunt/submarine sync OFF. Local retention and ordinary sync are independent.";
    private PartyFinderLinkRequestProcessor partyFinderLinkProcessor = new();
    private bool observedPartyFinderLinks;
    private volatile string partyFinderLinkStatus = "Party Finder website links OFF; existing item links unchanged.";
    private readonly IGameGui huntMapGui;
    private HuntMapProcessor huntMapProcessor = new();
    private CancellationTokenSource huntMapCancellation = new();
    private bool observedHuntMapGuidance;
    private DateTime nextHuntMapPollUtc;
    private volatile string huntMapStatus = "Automatic Hunt maps OFF.";
#endif
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient partyFinderHttp = PartyFinderHttp.CreateClient();
    private readonly PluginConfiguration configuration;
    private readonly IPartyFinderContributor partyFinderContributor;
    private readonly SyncRequestLifetime requestLifetime = new();
    private readonly DurableEvidenceBudget evidenceBudget = new();
    private readonly ObservationSavePolicy savePolicy = new();
    private readonly PluginUiRefreshPolicy uiRefreshPolicy = new();
    private OwnedCharacterState? activeOwnedState;
    private string activeGeneration = "";
    private volatile bool disposed;
    private bool logoutPending;
    private volatile PluginUiSnapshot uiState = PluginUiSnapshot.Empty;
    private volatile bool dataDetailsExpanded;
    private volatile bool diagnosticsExpanded;
    private string uiServerAddress = "";
    private string uiPairingCode = "";
    private DateTime nextUiDetailsUtc;
    private EvidenceBudgetUsage? uiBudget;
    private string uiAvailability = "";
    private bool pairingInFlight;
    private bool observedAutomaticSync;
    private bool observedItemLinks;
    private DateTime nextRetainerUploadUtc = DateTime.MinValue;
    private DateTime nextTransientMaintenanceUtc = DateTime.MinValue;
    private string settingsMessage = "";
    // A paired background sync must never interrupt login with its settings
    // window. Open it explicitly from Dalamud configuration when needed.
    private volatile bool settingsVisible;
    private volatile bool showPairingDetails;
    private DateTime nextAutomaticSyncUtc = DateTime.MinValue;
    private DateTime nextRetainerListingCaptureUtc = DateTime.MinValue;
    private DateTime nextGilLedgerPollUtc = DateTime.MinValue;
    private DateTime nextGilLedgerUploadUtc = DateTime.MaxValue;
    private DateTime nextInventorySyncUtc = DateTime.MaxValue;
    private DateTime nextGilLedgerFlushUtc = DateTime.MinValue;
    private DateTime nextItemLinkPollUtc = DateTime.MinValue;
    private DateTime nextRetainerVentureResultCaptureUtc = DateTime.MinValue;
    private DateTime nextRetainerVentureRosterCaptureUtc = DateTime.MinValue;
    private DateTime nextRetainerPresenceUtc = DateTime.MinValue;
    private string lastRetainerVentureResultProbeStatus = "inactive";
    private bool presenceInFlight;
    private int presenceFailureCount;
    private ulong activeRetainerCharacterContentId;
    private bool retainerUploadServerSupported;
    private long? lastObservedGil;
    private string? lastObservedRetainerId;
    private long? lastObservedRetainerGil;
    private RetainerBalanceRead? pendingRetainerBalance;
    private DateTime pendingRetainerBalanceSinceUtc = DateTime.MinValue;
    private readonly List<RetainerWithdrawalObservation> recentRetainerWithdrawals = [];
    private bool gilLedgerDirty;
    private readonly List<GilLedgerLogEvidence> recentGilLedgerLogs = [];
    private readonly List<GilLedgerChatEvidence> recentGilLedgerChat = [];
    private readonly Dictionary<string, DateTime> emittedRetainerChatEvidence = new(StringComparer.Ordinal);
    private bool syncInFlight;
    private bool itemLinkPollInFlight;
    private ItemLinkRequestProcessor itemLinkRequestProcessor = new();
    private readonly PairedClientHydrationState pairedClientHydration = new();
    private int automaticScopeIndex;
    private readonly object diagnosticsLock = new();
    private readonly List<string> diagnostics = [];
    private DateTime diagnosticRecordingUntilUtc = DateTime.MinValue;
#if GILLIONS_TEST_BUILD
    private static readonly string PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(4) ?? "unavailable";
#else
    private static readonly string PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "1.0.4";
#endif
#if GILLIONS_TEST_BUILD
    private const string CommandName = "/gillionssynctest";
#else
    private const string CommandName = "/gillionssync";
#endif
    private static readonly string[] SyncScopes = ["inventory", "currencies", "achievements", "collectibles", "character", "quest_journal", "reputation", "shared_fates", "glamour_plates"];
#if GILLIONS_TEST_BUILD
    private static readonly RetainerClientProfile RetainerClient = RetainerClientPolicy.Testing;
#else
    private static readonly RetainerClientProfile RetainerClient = RetainerClientPolicy.Stable;
#endif
    private static readonly string[] CurrentChangelog = [
#if GILLIONS_TEST_BUILD
        "Hunts use three-second bounded reads and prompt semantic-change sync. Sync now refreshes eligible Hunt data without bypassing consent/backoff. Recent same-session final counters can survive bill-item removal; disappearance alone remains UNKNOWN.",
        "Automatic Hunt map guidance has its own default-OFF permission. Secure TEST requests consume the current Site revision before opening a public reference map; never teleport, move or target. Site v1 selects one candidate; alternative cycling is not yet supported.",
        "Testing PF contribution now uses the approved secure TEST paired origin, never production. Website PF links require a new separate opt-in and compatible Site request contract; they deliver a native chat link requiring a final in-game click, never join or apply.",
        "Hunt routing context has separate OFF-by-default location consent and server travel grant. Rounded location and naturally visible public Teleport cache use authenticated HTTPS TEST only, RAM-only, latest-only, 15-second read/send admission and 45-second expiry. Cached Gil is a quote, not final charge. OFF cancels travel only; no movement history.",
        "Private daily/weekly native facts are a separate OFF-by-default Testing experiment with bounded local retention and explicit PRIVATE export. No Dashboard configuration or uploads; unavailable sources preserve prior state, and reset/cache ownership remains unverified.",
        "Observed market contribution is on by default and has its own off switch. Only naturally received partial listings/recent sales go to compatible Gillions intake; no scanning or buyer/retainer identities. Existing pairing is authentication, not anonymous transport.",
        "Submarine voyage retention remains read-only and separate from community preparation. Private Hunt/submarine sync has separate OFF-by-default permissions, uses only the approved HTTPS shared TEST with explicit compatible Testing pairing, and never uploads community voyage results.",
        "Hunt Bills and private submarine snapshots preserve positive/unknown/provisional semantics. Missing data never means empty. Dashboard facts remain local: no server Dashboard intake contract is activated.",
        "Testing Party Finder contribution requires fresh Testing pairing with site permission and a separate local opt-in. Public listings go to Gillions HTTPS, not directly to xivpf.com or localhost.",
        "Gillions keeps only an expiring, runtime current-listing cache; the paired token is used only for Gillions Authorization, never listing data. Permission denials stay stopped across logout/reload until fresh pairing.",
#else
        "Party Finder contribution is available as a separate off-by-default choice that sends only public listings directly to xivpf.com.",
#endif
        "Retainer observations, venture results, inventory, listings and ordinary character sync remain available.",
        "Venture planning and AutoRetainer integration have been removed. Existing stored plans and backups are preserved without further control.",
        "Pair once after updating so new records belong to the correct connection and character. Older local history stays preserved and inactive.",
        "Offline storage keeps admitted records and reports a coverage gap if it fills. The main window now focuses on connection and sync controls.",
    ];
    // Automatic work must remain below a visible frame hitch. One resource is
    // collected per cadence; changed inventory gets its own short debounce.
    private const int AutomaticSyncIntervalSeconds = 30;
    // Listing state changes only while a retainer is active. Five seconds is
    // responsive enough for a read-only market snapshot while avoiding a
    // recurring native-container walk during normal gameplay.
    private const int RetainerCaptureIntervalSeconds = 5;
    // Inventory events still flush after their short debounce. This fallback
    // catches Gil-only changes that do not produce an inventory event without
    // repeatedly reading native state during ordinary movement.
    private const int GilLedgerPollIntervalMilliseconds = 2000;
    private const int InventoryChangeDebounceMilliseconds = 1500;
    // Retainer UI transitions briefly expose another container's balance.
    // Require the same native retainer/balance pair to remain present before
    // treating it as an accounting observation.
    private const int RetainerBalanceStabilityMilliseconds = 1500;
    private const int RetainerReceiptCorrelationSeconds = 5;
    private const int AutomaticFailureRetrySeconds = 10;
    private const int ItemLinkPollIntervalSeconds = 5;
    private const int UnsupportedItemLinkRetryMinutes = 15;
    private const int NormalVentureResultCaptureIntervalMilliseconds = 100;
    private const int NormalVentureRosterCaptureIntervalMilliseconds = 30000;
    private const int ActiveVentureRosterCaptureIntervalMilliseconds = 1000;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IClientState clientState, IObjectTable objects, IFramework framework, IDataManager dataManager, IUnlockState unlockState, IGameInventory gameInventory, IPartyFinderGui partyFinderGui, IChatGui chatGui, IPluginLog log
#if GILLIONS_TEST_BUILD
        , IAddonLifecycle addonLifecycle, IMarketBoard marketBoard, ICondition marketConditions, IGameGui gameGui, IPlayerState travelPlayerState
#endif
    ) {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.clientState = clientState;
        this.objects = objects;
        this.framework = framework;
        this.dataManager = dataManager;
        this.unlockState = unlockState;
        this.gameInventory = gameInventory;
        this.chatGui = chatGui;
        this.log = log;
        configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        partyFinderContributor = PartyFinderContributorFactory.Create(partyFinderGui, partyFinderHttp, log,
            () => PartyFinderContributionEnabled, CapturePartyFinderSession, RecordDiagnostic);
        uiServerAddress = configuration.ServerUrl;
        configuration.OwnedCharacters ??= new(StringComparer.Ordinal);
        configuration.CoverageGap ??= new();
        if (configuration.ActiveSession is null || !configuration.ActiveSession.IsValid(configuration.DeviceId, configuration.DeviceToken)) {
            if (!string.IsNullOrWhiteSpace(configuration.DeviceToken)) { configuration.PairingRequired = true; RequestConfigurationSave(); }
        }
        observedAutomaticSync = configuration.AutomaticSync; observedItemLinks = configuration.EnableItemLinkRequests;
        if (!evidenceBudget.Measure(configuration.OwnedCharacters.Values).Fits && !configuration.CoverageGap.Paused) {
            configuration.CoverageGap.Reject(DateTime.UtcNow); RequestConfigurationSave();
        }
        pairedClientHydration.PluginStarted(HasPairedSession);
        clientState.Login += OnLogin;
        clientState.Logout += OnLogout;
        commands.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = "Pair or sync your selected Gillions data." });
        pluginInterface.UiBuilder.Draw += DrawSettings;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        framework.Update += OnFrameworkUpdate;
        gameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
        chatGui.LogMessage += OnLogMessage;
        chatGui.ChatMessage += OnChatMessage;
#if GILLIONS_TEST_BUILD
        huntMapGui = gameGui;
        observedHuntMapGuidance = configuration.AutomaticallyShowHuntMap;
        beastmasterLocal = new BeastmasterLocalView(pluginInterface, commands, framework, clientState, dataManager);
        configuration.SubmarineVoyages ??= new();
        submarineLocal = new SubmarineLocalView(pluginInterface, commands, framework, clientState, dataManager,
            addonLifecycle, configuration.SubmarineVoyages, () => { RequestConfigurationSave(); FlushConfigurationSave(); });
        configuration.HuntBills ??= new();
        huntLocal = new HuntBillLocalView(pluginInterface, commands, framework, clientState, dataManager,
            marketConditions, configuration.HuntBills, () => { RequestConfigurationSave(); FlushConfigurationSave(); });
        configuration.DashboardFacts ??= new();
        configuration.PersonalSync ??= new();
        dashboardLocal = new DashboardLocalView(pluginInterface, commands, framework, clientState, dataManager,
            gameGui, marketConditions, addonLifecycle, configuration.DashboardFacts, () => { RequestConfigurationSave(); FlushConfigurationSave(); });
        travelLocal = new TravelContextLocalView(pluginInterface,clientState,objects,travelPlayerState,dataManager,gameGui,marketConditions);
        travelLocal.Invalidated += ClearTravelPending;
        travelLocal.SetEnabled(configuration.ShareHuntRoutingLocation);
        this.marketConditions = marketConditions;
        marketContributor = new MarketContributor(RecordDiagnostic);
        marketContributor.SetEnabled(configuration.ContributeObservedMarketData);
        marketSource = new MarketContributionSource(marketBoard, marketContributor, CaptureMarketSession);
#endif
    }

    private void OnCommand(string command, string arguments) {
        settingsVisible = true;
        if (arguments.Trim().Equals("pair", StringComparison.OrdinalIgnoreCase)) showPairingDetails = true;
        else _ = SyncAsync();
    }

    private static unsafe ulong ReadLocalContentId() {
        var player = PlayerState.Instance();
        return player is null || !player->IsLoaded ? 0 : player->ContentId;
    }

    private void OpenSettings() => settingsVisible = true;

    // Pairing code is entered locally by the user in the plugin configuration UI.
    // It is single-use; Gillions returns a revocable per-device credential.
    private async Task PairAsync() {
        SyncRequestPermit? permit = null;
        var ownsPairAttempt = false;
        try {
            permit = await framework.RunOnFrameworkThread(() => {
                if (disposed || pairingInFlight) throw new OperationCanceledException();
                if (string.IsNullOrWhiteSpace(configuration.PairingCode) || !SyncOrigin.TryNormalize(configuration.ServerUrl, out var origin))
                    throw new InvalidOperationException("Enter a pairing code and a valid HTTPS server address.");
                pairingInFlight = true;
                ownsPairAttempt = true;
                configuration.PairingRequired = true;
                var pairingCode = configuration.PairingCode; configuration.PairingCode = "";
                ResetSessionContext(); RefreshSessionContext(); RequestConfigurationSave();
                return requestLifetime.Capture(SyncRequestMode.Pair, activeRetainerCharacterContentId, null, origin, pairingCode);
            });
            using var request = Request("/api/game-sync/enroll", permit, new { version = PluginVersion });
            using var response = await SendAsync(request, permit);
            await EnsureSuccessfulResponse(response, permit.Cancellation);
            var enrolled = JsonSerializer.Deserialize<EnrollmentResponse>(await SyncResponsePolicy.ReadAsync(response.Content, permit.Cancellation), SyncResponsePolicy.Options);
            if (enrolled is null || !enrolled.ok) throw new InvalidOperationException("Invalid pairing response.");
            var session = PairedSession.Create(permit.Origin, enrolled.device_id, enrolled.token);
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit);
                configuration.DeviceToken = enrolled.token; configuration.DeviceId = enrolled.device_id;
                configuration.ActiveSession = session; configuration.PairingRequired = false; configuration.PairingCode = "";
#if GILLIONS_TEST_BUILD
                configuration.SyncPersonalHunts = false; configuration.SyncPersonalSubmarines = false;
#endif
                configuration.SyncBlockedCode = ""; configuration.SyncBlockedMessage = "";
                RefreshSessionContext(); pairedClientHydration.PairingSucceeded();
                presenceFailureCount = 0; nextRetainerPresenceUtc = DateTime.MinValue;
                RequestConfigurationSave(); settingsMessage = "Connected. Gillions will load your selected character.";
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!disposed && (permit is null || PermitIsCurrent(permit))) settingsMessage = OperationFailureMessage(error);
            });
            log.Warning("Gillions pairing did not complete ({Type}).", error.GetType().Name);
        } finally {
            if (ownsPairAttempt && !disposed) await framework.RunOnFrameworkThread(() => { if (!disposed) pairingInFlight = false; });
        }
    }

    private async Task SendRetainerPresenceAsync(SyncRequestPermit permit, RetainerPresenceDocument presence) {
        try {
            using var request = Request("/api/game-sync/presence", permit, presence);
#if GILLIONS_TEST_BUILD
            request.Headers.Add("X-Gillions-Market-Contract", "1"); // Optional capability; existing body/identity unchanged.
            if (permit.Origin == PersonalSyncPolicy.Origin) {
                request.Headers.Add("X-Gillions-Personal-Contract", PersonalSyncPolicy.Contract);
                request.Headers.Add("X-Gillions-Personal-Capability", TravelSyncPolicy.Capability);
            }
#endif
#if GILLIONS_TEST_BUILD
            using var response = await SendPresenceAsync(request, permit);
#else
            using var response = await SendAsync(request, permit);
#endif
            await EnsureSuccessfulResponse(response, permit.Cancellation);
            var responseJson = await SyncResponsePolicy.ReadAsync(response.Content, permit.Cancellation);
            if (!RetainerPresenceResponsePolicy.TryParse(responseJson, RetainerClient, out var uploadSupported))
                throw new InvalidOperationException("Invalid presence response.");
            await CommitAsync(permit, _ => {
                retainerUploadServerSupported = uploadSupported; presenceFailureCount = 0;
#if GILLIONS_TEST_BUILD
                marketAcceptedGeneration = MarketContributor.Compatible(responseJson) ? permit.Session!.Generation : "";
                personalAccepted.Clear();
                if (permit.Origin == PersonalSyncPolicy.Origin) foreach (var resource in PersonalSyncPolicy.Resources)
                    if (PersonalSyncPolicy.Compatible(responseJson, resource)) personalAccepted.Add(resource);
                travelAccepted = permit.Origin == TravelSyncPolicy.Origin && TravelSyncPolicy.Compatible(responseJson);
                if (!travelAccepted) ClearTravelPending();
#endif
                nextRetainerPresenceUtc = DateTime.UtcNow.Add(RetainerPresencePolicy.NextSuccessDelay(Random.Shared.Next(-5, 6)));
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit)) return;
                ClearRetainerServerAcceptance(); presenceFailureCount++;
#if GILLIONS_TEST_BUILD
                travelAccepted = false; ClearTravelPending();
#endif
                nextRetainerPresenceUtc = DateTime.UtcNow.Add(RetainerPresencePolicy.NextFailureDelay(presenceFailureCount, Random.Shared.Next(-5, 6)));
            });
            log.Debug("Gillions presence will retry ({Type}).", error.GetType().Name);
        } finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => { if (!disposed) presenceInFlight = false; });
        }
    }

    private void ClearRetainerServerAcceptance() {
        retainerUploadServerSupported = false;
#if GILLIONS_TEST_BUILD
        personalAccepted.Clear();
#endif
    }

    private void SendCurrentRetainerPresence(ulong contentId, DateTime now, SyncRequestMode mode = SyncRequestMode.Automatic) {
        if (!HasPairedSession || presenceInFlight) return;
        var permit = CapturePermit(mode);
        var presence = RetainerPresencePolicy.CreateNative(new(contentId.ToString(), CurrentState.CharacterName, CurrentState.CharacterWorld), now, RetainerClient, PluginVersion);
        ClearRetainerServerAcceptance(); presenceInFlight = true;
        _ = SendRetainerPresenceAsync(permit, presence);
    }

    private void OnFrameworkUpdate(IFramework frameworkInstance) {
        if (disposed) return;
        try { UpdateOwnedState(); }
        catch (Exception error) { log.Warning("Gillions background observation paused ({Type}).", error.GetType().Name); }
        finally { FlushConfigurationSave(); PublishUiState(); }
    }

    private void UpdateOwnedState() {
        var now = DateTime.UtcNow;
        RefreshSessionContext(); MaintainTransientState(now);
        partyFinderContributor.Tick(now);
#if GILLIONS_TEST_BUILD
        var huntRevision = huntLocal.SemanticRevision;
        var huntRawRevision = huntLocal.RawRevision;
        huntLocal.Tick(now); // Three-second bounded read; semantic changes bypass only routine upload cadence.
        var huntChanged = huntLocal.SemanticRevision != huntRevision;
        if (huntChanged || huntRawRevision != huntLocal.RawRevision) RecordDiagnostic(huntLocal.LastDiagnostic);
        dashboardLocal.Tick(now); // Independent, off-by-default; one bounded source group per due check.
        travelLocal.Tick(now); // Independent default-OFF, latest-only RAM, bounded 15-second read.
        TickTravelSync(now); // Also clears invalid/expired pending state while unpaired.
        if (now >= nextMarketMaintenanceUtc) {
            nextMarketMaintenanceUtc = now.AddMilliseconds(250);
            marketContributor.RefreshSession(CaptureMarketSession());
            marketContributor.Tick(now); // Managed upload maintenance only, not market searches.
        }
#endif
        if (!HasPairedSession || activeOwnedState is null || !clientState.IsLoggedIn) return;
#if GILLIONS_TEST_BUILD
        TickPersonalSync(now, prompt: huntChanged);
#endif
        var contentId = activeRetainerCharacterContentId;
        var state = CurrentState;
        var websitePollingEnabled = configuration.EnableItemLinkRequests;
#if GILLIONS_TEST_BUILD
        websitePollingEnabled |= configuration.AutomaticallyShowHuntMap && configuration.ActiveSession?.Origin == HuntMapPolicy.Origin;
#endif
        if (ItemLinkPollPolicy.ShouldPoll(websitePollingEnabled, true, configuration.DeviceToken, itemLinkPollInFlight, now, nextItemLinkPollUtc)) {
            nextItemLinkPollUtc = now.AddSeconds(ItemLinkPollIntervalSeconds);
#if GILLIONS_TEST_BUILD
            _ = PollWebsiteRequestsAsync();
#else
            _ = PollItemLinkRequestsAsync();
#endif
        }
        if (pairedClientHydration.TryBeginCharacterSync(syncInFlight)) {
            _ = SyncAutomaticallyAsync([PairedClientHydrationState.CharacterResource], SyncRequestMode.Hydration);
            return;
        }
        if (pairedClientHydration.TryBeginPresence(presenceInFlight)) {
            SendCurrentRetainerPresence(contentId, now, SyncRequestMode.Hydration);
            return;
        }
        if (!configuration.AutomaticSync) return;
        if (!presenceInFlight && now >= nextRetainerPresenceUtc) SendCurrentRetainerPresence(contentId, now);
        var retainerWindowActive = DirectGameSnapshotCollector.IsRetainerVentureWindowActive();
        var ventureChanged = false;
        if (retainerWindowActive || now >= nextRetainerVentureResultCaptureUtc) {
            nextRetainerVentureResultCaptureUtc = now.AddMilliseconds(NormalVentureResultCaptureIntervalMilliseconds);
            ventureChanged = CaptureRetainerResult(state, out var resultProbeStatus);
            if (resultProbeStatus != lastRetainerVentureResultProbeStatus) {
                if (resultProbeStatus != "inactive") RecordDiagnostic($"Retainer result observation: {resultProbeStatus}.");
                lastRetainerVentureResultProbeStatus = resultProbeStatus;
            }
            if (ventureChanged) RequestConfigurationSave();
        }
        if (now >= nextRetainerVentureRosterCaptureUtc) {
            nextRetainerVentureRosterCaptureUtc = now.AddMilliseconds(retainerWindowActive
                ? ActiveVentureRosterCaptureIntervalMilliseconds : NormalVentureRosterCaptureIntervalMilliseconds);
            ventureChanged |= DirectGameSnapshotCollector.CaptureRetainerVentureRosterAndGear(state.RetainerState);
            savePolicy.Refresh();
        }
        if (ventureChanged) { QueueRetainerUpload(now); RequestConfigurationSave(); }
        if (now >= nextGilLedgerPollUtc || (gilLedgerDirty && now >= nextGilLedgerFlushUtc)) {
            nextGilLedgerPollUtc = now.AddMilliseconds(GilLedgerPollIntervalMilliseconds);
            CaptureOwnedGilLedgerChange();
        }
        if (now >= nextRetainerListingCaptureUtc) {
            nextRetainerListingCaptureUtc = now.AddSeconds(RetainerCaptureIntervalSeconds);
            if (DirectGameSnapshotCollector.CaptureLoadedRetainerListings()) nextInventorySyncUtc = now.AddMilliseconds(250);
        }
        if (syncInFlight) return;
        // A due ordinary resource keeps its cadence even during repeated Retainer
        // changes. Every sync also drains its captured owner's queued Gil events.
        if (now >= nextAutomaticSyncUtc) {
            nextAutomaticSyncUtc = now.AddSeconds(AutomaticSyncIntervalSeconds);
            _ = SyncAutomaticallyAsync([NextAutomaticScope()]);
            return;
        }
        if (now >= nextInventorySyncUtc) {
            nextInventorySyncUtc = DateTime.MaxValue;
            _ = SyncAutomaticallyAsync(["inventory"]);
            return;
        }
        if (retainerUploadServerSupported && now >= nextRetainerUploadUtc) {
            nextRetainerUploadUtc = now.AddSeconds(AutomaticSyncIntervalSeconds);
            _ = SyncAutomaticallyAsync([RetainerClientPolicy.ResourceType]);
            return;
        }
        if (state.PendingGilLedgerEvents.Count > 0 && nextGilLedgerUploadUtc == DateTime.MaxValue) nextGilLedgerUploadUtc = now;
        if (now >= nextGilLedgerUploadUtc) {
            nextGilLedgerUploadUtc = DateTime.MaxValue;
            _ = SyncAutomaticallyAsync([]);
        }
    }

    private void QueueRetainerUpload(DateTime now) {
        var due = now.AddMilliseconds(250);
        if (nextRetainerUploadUtc > due) nextRetainerUploadUtc = due;
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> _) {
        if (disposed || !framework.IsInFrameworkUpdateThread || !clientState.IsLoggedIn || !configuration.AutomaticSync || !HasPairedSession) return;
        RefreshSessionContext();
        if (activeOwnedState is null) return;
        // The event itself is a copied native state notification. We take the
        // balance once the game has finished its short update burst so a single
        // action becomes one ledger row rather than slot-level noise.
        gilLedgerDirty = true;
        nextGilLedgerFlushUtc = DateTime.UtcNow.AddMilliseconds(750);
        if (configuration.AutomaticSync && !string.IsNullOrWhiteSpace(configuration.DeviceToken))
            nextInventorySyncUtc = DateTime.UtcNow.AddMilliseconds(InventoryChangeDebounceMilliseconds);
    }

    private async Task PollItemLinkRequestsAsync() {
        SyncRequestPermit? permit = null;
        ItemLinkRequestProcessor? processor = null;
        var ownsPoll = false;
        try {
            permit = await framework.RunOnFrameworkThread(() => {
                if (disposed
#if !GILLIONS_TEST_BUILD
                    || itemLinkPollInFlight
#endif
                    ) throw new OperationCanceledException();
                var result = CapturePermit(SyncRequestMode.ItemLink);
                itemLinkPollInFlight = true; ownsPoll = true; processor = itemLinkRequestProcessor;
                return result;
            });
#if GILLIONS_TEST_BUILD
            var pfAllowed = await framework.RunOnFrameworkThread(() => PartyFinderLinksPermitted(permit));
            using var pollRequest = Request("/api/game-sync/item-links/poll", permit, new {
                capability = "native_item_link", pluginVersion = PluginVersion,
                nativeRequests = new { contract = PartyFinderLinkPolicy.Contract, contractVersion = 1,
                    capabilities = pfAllowed ? new[] { PartyFinderLinkPolicy.Capability } : Array.Empty<string>() }
            });
#else
            using var pollRequest = Request("/api/game-sync/item-links/poll", permit, new { capability = "native_item_link", pluginVersion = PluginVersion });
#endif
#if GILLIONS_TEST_BUILD
            using var pollResponse = pfAllowed
                ? await SendNativePartyFinderAsync(pollRequest, permit)
                : await SendAsync(pollRequest, permit);
#else
            using var pollResponse = await SendAsync(pollRequest, permit);
#endif
            if (pollResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented) {
                await CommitAsync(permit, _ => nextItemLinkPollUtc = DateTime.UtcNow.AddMinutes(UnsupportedItemLinkRetryMinutes));
                return;
            }
            if (pollResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
                await CommitAsync(permit, _ => nextItemLinkPollUtc = DateTime.UtcNow.AddMinutes(5));
                return;
            }
            await EnsureSuccessfulResponse(pollResponse, permit.Cancellation);
            var pollJson = await SyncResponsePolicy.ReadAsync(pollResponse.Content, permit.Cancellation);
#if GILLIONS_TEST_BUILD
            using var pollDocument = JsonDocument.Parse(pollJson);
            if (pollDocument.RootElement.TryGetProperty("request", out var typedRequest) && typedRequest.ValueKind == JsonValueKind.Object
                && typedRequest.TryGetProperty("requestType", out var requestType)
                && !(requestType.ValueKind == JsonValueKind.String && requestType.GetString() == "item")) {
                if (requestType.ValueKind == JsonValueKind.String && requestType.GetString() == PartyFinderLinkPolicy.RequestType) {
                    var pfRequest = PartyFinderLinkPolicy.Parse(pollDocument.RootElement);
                    var pfProcessor = await framework.RunOnFrameworkThread(() => { RequirePermit(permit); return partyFinderLinkProcessor; });
                    var pfResult = await pfProcessor.ProcessAsync(pfRequest, () => DateTime.UtcNow,
                        () => !permit.Cancellation.IsCancellationRequested,
                        r => ConsumePartyFinderLinkAsync(permit, r),
                        r => framework.RunOnFrameworkThread(() => {
                            RequirePermit(permit);
                            if (!PartyFinderLinksPermitted(permit) || !PartyFinderLinkPolicy.Valid(r, DateTime.UtcNow)) throw new OperationCanceledException();
                            chatGui.Print(NativePartyFinderLinkFactory.Create(r), "Gillions");
                        }));
                    await CommitAsync(permit, _ => { partyFinderLinkStatus = pfResult; RecordDiagnostic($"Party Finder link: {pfResult}."); });
                } else await CommitAsync(permit, _ => RecordDiagnostic("Website request type unsupported; no action."));
                return;
            }
            await CommitAsync(permit, _ => {
                if (configuration.EnablePartyFinderLinkRequests) partyFinderLinkStatus = "Awaiting compatible Site native PF request contract; existing item links remain available.";
            });
#endif
            var response = JsonSerializer.Deserialize<ItemLinkPollResponse>(pollJson, SyncResponsePolicy.Options);
            if (response?.Ok != true) throw new InvalidOperationException("Invalid item-link response.");
            if (response.Request is null) return;
            var result = await processor!.ProcessAsync(response.Request, DateTime.UtcNow,
                itemId => ResolveItemNameAsync(itemId, permit),
                request => ConsumeItemLinkRequestAsync(permit, request),
                link => framework.RunOnFrameworkThread(() => {
                    RequirePermit(permit);
                    if (DateTime.UtcNow >= response.Request.ExpiresAtUtc) throw new OperationCanceledException();
                    chatGui.Print(link, "Gillions");
                }));
            await CommitAsync(permit, _ => RecordDiagnostic($"Item-link request: {result}."));
        } catch (Exception error) {
            if (!disposed && permit is not null) await framework.RunOnFrameworkThread(() => {
                if (PermitIsCurrent(permit)) nextItemLinkPollUtc = DateTime.UtcNow.AddSeconds(30);
            });
            log.Debug("Gillions item-link request did not complete ({Type}).", error.GetType().Name);
        } finally {
#if !GILLIONS_TEST_BUILD
            if (ownsPoll && !disposed) await framework.RunOnFrameworkThread(() => { if (!disposed) itemLinkPollInFlight = false; });
#else
            _ = ownsPoll; // Testing's common request-loop owner releases the shared flight gate.
#endif
        }
    }

    private Task<string?> ResolveItemNameAsync(long itemId, SyncRequestPermit permit) => framework.RunOnFrameworkThread(() => {
        RequirePermit(permit);
        if (!NativeItemLinkFactory.IsValidItemId(itemId)) return null;
        var item = dataManager.GetExcelSheet<Item>()?.GetRowOrDefault((uint)itemId);
        return item is { RowId: > 0 } ? item.Value.Name.ExtractText() : null;
    });

#if GILLIONS_TEST_BUILD
    // One established five-second loop. Hunt advertisement and item/PF requests
    // have independent permissions, but share the same serialized flight owner.
    private async Task PollWebsiteRequestsAsync() {
        var owns = false;
        try {
            var pollHunts = await framework.RunOnFrameworkThread(() => {
                if (disposed || itemLinkPollInFlight) throw new OperationCanceledException();
                itemLinkPollInFlight = true; owns = true;
                return configuration.AutomaticallyShowHuntMap && configuration.ActiveSession?.Origin == HuntMapPolicy.Origin
                    && DateTime.UtcNow >= nextHuntMapPollUtc;
            });
            if (pollHunts) await PollHuntMapRequestAsync();
            var pollItems = await framework.RunOnFrameworkThread(() => !disposed && configuration.EnableItemLinkRequests && HasPairedSession);
            if (pollItems) await PollItemLinkRequestsAsync();
        } catch (OperationCanceledException) { }
        finally {
            if (owns && !disposed) await framework.RunOnFrameworkThread(() => { if (!disposed) itemLinkPollInFlight = false; });
        }
    }
    private bool HuntMapPermitted(SyncRequestPermit permit, CancellationToken cancellation) => !cancellation.IsCancellationRequested
        && HuntMapPolicy.Admit(configuration.AutomaticallyShowHuntMap, HasPairedSession, permit.Origin, PermitIsCurrent(permit))
        && clientState.IsLoggedIn && objects.LocalPlayer is not null
        && !marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
        && !marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
    private void ClearHuntMapRequests() {
        huntMapCancellation.Cancel(); huntMapCancellation.Dispose(); huntMapCancellation = new();
        huntMapProcessor = new(); huntMapStatus = "No active Hunt map request; local observations preserved.";
    }
    private async Task PollHuntMapRequestAsync() {
        SyncRequestPermit? permit = null;
        try {
            var context = await framework.RunOnFrameworkThread(() => {
                var p = CapturePermit(SyncRequestMode.Personal);
                var token = huntMapCancellation.Token;
                if (!HuntMapPermitted(p, token)) throw new OperationCanceledException();
                nextHuntMapPollUtc = DateTime.UtcNow.AddSeconds(ItemLinkPollIntervalSeconds);
                return (Permit: p, Token: token, Processor: huntMapProcessor);
            });
            permit = context.Permit;
            using var linked = HuntMapTransport.Deadline(permit.Cancellation, context.Token);
            var cancellation = linked.Token;
            using var request = Request("/api/game-sync/item-links/poll", permit, new { capability = HuntMapPolicy.Capability });
            using var response = await SendHuntMapAsync(request, permit, cancellation);
            if (response.StatusCode != HttpStatusCode.OK) {
                await framework.RunOnFrameworkThread(() => {
                    if (!HuntMapPermitted(permit, cancellation)) return;
                    huntMapStatus = "Hunt map unavailable or permission denied; no map opened.";
                    nextHuntMapPollUtc = DateTime.UtcNow.AddSeconds(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? 60 : 30);
                });
                return;
            }
            var json = await HuntMapTransport.ReadAsync(response.Content, cancellation);
            var parsed = HuntMapPolicy.Parse(json, DateTime.UtcNow);
            var result = await context.Processor.ProcessAsync(parsed, () => DateTime.UtcNow,
                () => framework.RunOnFrameworkThread(() => HuntMapPermitted(permit, cancellation)),
                r => ConsumeHuntMapAsync(permit, r, cancellation),
                r => framework.RunOnFrameworkThread(() => {
                    if (!HuntMapPermitted(permit, cancellation) || !HuntMapPolicy.Valid(r, DateTime.UtcNow)) return false;
                    // Supported public API, human-readable map XY. No native
                    // offsets, callbacks, teleport, movement or targeting.
                    var opened = huntMapGui.OpenMapWithMapLink(new MapLinkPayload(r.TerritoryId, r.MapId, r.MapX, r.MapY));
                    if (opened) chatGui.Print(r.Guidance, "Gillions");
                    return opened;
                }));
            await framework.RunOnFrameworkThread(() => {
                if (HuntMapPermitted(permit, cancellation)) huntMapStatus = result;
            });
        } catch (Exception error) {
            if (!disposed && permit is not null) await framework.RunOnFrameworkThread(() => {
                if (PermitIsCurrent(permit)) { huntMapStatus = "Hunt map request interrupted; no retry of a consumed action."; nextHuntMapPollUtc = DateTime.UtcNow.AddSeconds(30); }
            });
            log.Debug("Gillions Hunt map request did not complete ({Type}).", error.GetType().Name);
        }
    }
    private async Task<HttpResponseMessage> SendHuntMapAsync(HttpRequestMessage request, SyncRequestPermit permit, CancellationToken cancellation) {
        Task<HttpResponseMessage>? pending = null;
        await framework.RunOnFrameworkThread(() => {
            if (!HuntMapPermitted(permit, cancellation)) throw new OperationCanceledException();
            pending = personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        });
        return await pending!;
    }
    private async Task<bool> ConsumeHuntMapAsync(SyncRequestPermit permit, HuntMapRequest r, CancellationToken cancellation) {
        var catalogValid = await framework.RunOnFrameworkThread(() => {
            if (!HuntMapPermitted(permit, cancellation) || !HuntMapPolicy.Valid(r, DateTime.UtcNow)) return false;
            var map = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRowOrDefault(r.MapId);
            return map is { RowId: > 0 } && map.Value.SizeFactor > 0 && map.Value.TerritoryType.RowId == r.TerritoryId
                && dataManager.GetExcelSheet<TerritoryType>().HasRow(r.TerritoryId)
                && dataManager.GetExcelSheet<MobHuntTarget>().HasRow(r.TargetId)
                && (r.FateId is null || dataManager.GetExcelSheet<Fate>().HasRow(r.FateId.Value));
        });
        if (!catalogValid) return false;
        // Parent bounds the whole poll/consume turn; known claim expiry may
        // shorten it, never extend it or retain a stale claim while reading.
        using var claimDeadline = HuntMapTransport.Deadline(cancellation, CancellationToken.None, r.ExpiresAtUtc);
        cancellation = claimDeadline.Token;
        using var request = Request("/api/game-sync/item-links/consume", permit, new {
            requestType = HuntMapPolicy.RequestType, capability = HuntMapPolicy.Capability,
            requestId = r.RequestId, claimToken = r.ClaimToken, revision = r.Revision,
        });
        using var response = await SendHuntMapAsync(request, permit, cancellation);
        return response.StatusCode == HttpStatusCode.OK && HuntMapPolicy.Consumed(await HuntMapTransport.ReadAsync(response.Content, cancellation));
    }
    private bool PartyFinderLinksPermitted(SyncRequestPermit permit) => PermitIsCurrent(permit)
        && configuration.EnableItemLinkRequests && configuration.EnablePartyFinderLinkRequests
        && permit.Origin == GillionsPartyFinderContributor.ApprovedTestingOrigin;
    private async Task<bool> ConsumePartyFinderLinkAsync(SyncRequestPermit permit, PartyFinderLinkRequest request) {
        if (!await framework.RunOnFrameworkThread(() => PartyFinderLinksPermitted(permit) && PartyFinderLinkPolicy.Valid(request, DateTime.UtcNow))) return false;
        using var consumeRequest = Request("/api/game-sync/item-links/consume", permit, new {
            requestId = request.RequestId, claimToken = request.ClaimToken, requestType = PartyFinderLinkPolicy.RequestType,
            listingKey = request.ListingKey, contract = PartyFinderLinkPolicy.Contract
        });
        using var response = await SendNativePartyFinderAsync(consumeRequest, permit);
        if (!response.IsSuccessStatusCode) return false;
        using var document = JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(response.Content, permit.Cancellation));
        var root = document.RootElement;
        return root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True
            && root.TryGetProperty("consumed", out var consumed) && consumed.ValueKind == JsonValueKind.True
            && root.TryGetProperty("contract", out var contract) && contract.GetString() == PartyFinderLinkPolicy.Contract
            && root.TryGetProperty("requestType", out var type) && type.GetString() == PartyFinderLinkPolicy.RequestType
            && root.TryGetProperty("requestId", out var id) && id.GetString() == request.RequestId;
    }
    // Same authenticated item-request channel, but PF-capable polls/consumes
    // must never follow redirects or forward a claim body to another origin.
    private async Task<HttpResponseMessage> SendNativePartyFinderAsync(HttpRequestMessage request, SyncRequestPermit permit) {
        Task<HttpResponseMessage>? pending = null;
        await framework.RunOnFrameworkThread(() => {
            if (!PartyFinderLinksPermitted(permit)) throw new OperationCanceledException();
            pending = partyFinderHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, permit.Cancellation);
        });
        return await pending!;
    }
#endif
    private async Task<bool> ConsumeItemLinkRequestAsync(SyncRequestPermit permit, ItemLinkRequest request) {
        if (DateTime.UtcNow >= request.ExpiresAtUtc) return false;
        using var consumeRequest = Request("/api/game-sync/item-links/consume", permit, new { requestId = request.RequestId, claimToken = request.ClaimToken });
        using var consumeResponse = await SendAsync(consumeRequest, permit);
        if (!consumeResponse.IsSuccessStatusCode) return false;
        using var document = JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(consumeResponse.Content, permit.Cancellation));
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True
            && root.TryGetProperty("consumed", out var consumed) && consumed.ValueKind == JsonValueKind.True;
    }

    private void OnLogMessage(ILogMessage message) {
        if (!CanCaptureLedgerEvidence() || message.ParameterCount > 8
            || message.LogMessageId is not (1605 or 952 or 1603 or 1606 or 1607 or 3231 or 10452 or 533 or 732 or 733 or 1385 or 734 or 1687 or 1688 or 736 or 737 or 550)) return;
        // Never retain raw message text, entity names, or string parameters.
        // IDs and integer arguments are sufficient to map known transaction
        // contracts during beta validation without collecting chat content.
        var values = new List<int>();
        for (var index = 0; index < Math.Min(message.ParameterCount, 8); index++) {
            if (message.TryGetIntParameter(index, out var value)) values.Add(value);
        }
        recentGilLedgerLogs.Add(new GilLedgerLogEvidence(DateTime.UtcNow, message.LogMessageId, values.ToArray()));
        TransientEvidencePolicy.Prune(recentGilLedgerLogs, DateTime.UtcNow, TimeSpan.FromSeconds(5), entry => entry.ObservedAtUtc);
    }

    private void OnChatMessage(IHandleableChatMessage message) {
        if (!CanCaptureLedgerEvidence() || message.LogKind is not (XivChatType.SystemMessage or XivChatType.RetainerSale)) return;
        // Some itemized vendor messages do not arrive with usable LogMessage
        // integer parameters. Keep only the structured sale facts needed to
        // correlate the message with the native gil balance; never retain raw
        // chat text or upload it.
        if (message.OriginalMessage.ByteLength > 4096) return;
        var match = VendorSaleChatRegex.Match(message.OriginalMessage.ExtractText());
        if (!match.Success) return;
        var quantityToken = match.Groups["quantity"].Value;
        var quantity = quantityToken.Equals("a", StringComparison.OrdinalIgnoreCase) || quantityToken.Equals("an", StringComparison.OrdinalIgnoreCase)
            ? 1
            : int.TryParse(quantityToken.Replace(",", ""), out var parsedQuantity) ? parsedQuantity : 0;
        if (quantity is <= 0 or > 9999) return;
        if (!long.TryParse(match.Groups["amount"].Value.Replace(",", ""), out var amount) || amount is <= 0 or > 999999999) return;
        var itemId = SeString.Parse(message.OriginalMessage.Data.Span).Payloads.OfType<ItemPayload>().FirstOrDefault()?.ItemId;
        if (itemId is null or 0 or > int.MaxValue) return;
        var retainer = message.LogKind == XivChatType.RetainerSale ? NativeInventoryCollector.FindLoadedRetainerItem(itemId.Value) : null;
        recentGilLedgerChat.Add(new GilLedgerChatEvidence(DateTime.UtcNow, itemId > 0 ? (int)itemId : null, quantity, amount, retainer?.RetainerId, retainer?.RetainerName));
        TransientEvidencePolicy.Prune(recentGilLedgerChat, DateTime.UtcNow, TimeSpan.FromSeconds(5), entry => entry.ObservedAtUtc);
    }

    private GilLedgerEvent CreateGilLedgerEvent(long gilDelta, string kind, string confidence, int? itemId, int? itemQuantity, string? retainerId, string? retainerName, uint? logMessageId, int[] logIntegerParameters) => new(
        Guid.NewGuid().ToString("N"), DateTime.UtcNow, gilDelta, kind, confidence, itemId, itemQuantity, retainerId, retainerName, logMessageId, logIntegerParameters,
        objects.LocalPlayer?.Name.TextValue ?? "", objects.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "");

    private void CaptureGilLedgerChange() {
        if (!clientState.IsLoggedIn) {
            lastObservedGil = null;
            lastObservedRetainerId = null;
            lastObservedRetainerGil = null;
            pendingRetainerBalance = null;
            pendingRetainerBalanceSinceUtc = DateTime.MinValue;
            recentRetainerWithdrawals.Clear();
            gilLedgerDirty = false;
            return;
        }

        var observedRetainerBalance = DirectGameSnapshotCollector.ReadActiveRetainerGil();
        RetainerBalanceRead? retainerBalance = null;
        if (observedRetainerBalance is null) {
            lastObservedRetainerId = null;
            lastObservedRetainerGil = null;
            pendingRetainerBalance = null;
            pendingRetainerBalanceSinceUtc = DateTime.MinValue;
        }
        else if (pendingRetainerBalance is null
                 || pendingRetainerBalance.RetainerId != observedRetainerBalance.RetainerId
                 || pendingRetainerBalance.Gil != observedRetainerBalance.Gil) {
            pendingRetainerBalance = observedRetainerBalance;
            pendingRetainerBalanceSinceUtc = DateTime.UtcNow;
        }
        else if (DateTime.UtcNow >= pendingRetainerBalanceSinceUtc.AddMilliseconds(RetainerBalanceStabilityMilliseconds)) {
            retainerBalance = observedRetainerBalance;
        }

        ObserveStableRetainerBalance(retainerBalance);
        if (FlushExpiredRetainerGilReceipts() || FlushExpiredRetainerGilDeposits()) {
            RequestConfigurationSave();
            QueueGilLedgerUpload();
        }
        var currentGil = NativeInventoryCollector.ReadGil();
        if (currentGil is null) return;
        if (lastObservedGil is null) { lastObservedGil = currentGil; return; }
        // Gil changes are not guaranteed to raise InventoryChangedRaw (the
        // vendor-sale reports reproduced in 0.0.17/0.0.18 are one example).
        // Poll the already-resident native balance so the ledger does not
        // depend on an unrelated inventory notification.
        if (currentGil.Value != lastObservedGil.Value && !gilLedgerDirty) {
            gilLedgerDirty = true;
            nextGilLedgerFlushUtc = DateTime.UtcNow.AddMilliseconds(750);
        }
        if (FlushRetainerChatEvidence()) RequestConfigurationSave();
        if (!gilLedgerDirty || DateTime.UtcNow < nextGilLedgerFlushUtc) return;
        gilLedgerDirty = false;
        var delta = currentGil.Value - lastObservedGil.Value;
        lastObservedGil = currentGil;
        if (delta == 0) return;
        var recent = recentGilLedgerLogs.LastOrDefault(entry => entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-3));
        var recentChatSale = recentGilLedgerChat.LastOrDefault(entry => entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-3));
        if (delta < 0 && recent?.LogMessageId == 737) {
            QueueRetainerGilDeposit(CreateGilLedgerEvent(delta, "unclassified", "inferred", null, null, null, null, recent.LogMessageId, recent.IntegerParameters));
            RequestConfigurationSave();
            RecordDiagnostic($"Pending retainer Gil deposit: {delta:#,##0} gil; awaiting a matching retainer balance increase.");
            return;
        }
        CurrentState.PendingGilLedgerEvents ??= [];
        if (delta > 0 && recent?.LogMessageId == 736) {
            var confirmedRetainerSales = ConfirmPendingRetainerSales(delta, null);
            if (confirmedRetainerSales.Count > 0) {
                foreach (var sale in confirmedRetainerSales) CurrentState.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(
                    sale.Amount, "retainer_sale", "confirmed", sale.ItemId, sale.ItemQuantity, sale.RetainerId, sale.RetainerName, recent.LogMessageId, recent.IntegerParameters));
                RequestConfigurationSave();
                QueueGilLedgerUpload();
                RecordDiagnostic($"Gil ledger: +{delta:#,##0} gil; confirmed/retainer_sale; items={confirmedRetainerSales.Count}; retainer={confirmedRetainerSales[0].RetainerName ?? "unknown"}.");
                return;
            }
            QueueRetainerGilReceipt(CreateGilLedgerEvent(delta, "retainer_gil_receipt", "inferred", null, null, null, null, recent.LogMessageId, recent.IntegerParameters));
            RequestConfigurationSave();
            RecordDiagnostic($"Pending retainer Gil receipt: +{delta:#,##0} gil; awaiting a matching retainer balance withdrawal.");
            return;
        }
        var classification = ClassifyGilLedgerEvent(delta, recent, recentChatSale);
        CurrentState.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(
            delta, classification.Kind, classification.Confidence, classification.ItemId, classification.ItemQuantity,
            classification.RetainerId, classification.RetainerName, recent?.LogMessageId, recent?.IntegerParameters ?? []));
        if (recentChatSale?.IsRetainerSale == true) emittedRetainerChatEvidence[recentChatSale.EvidenceId] = DateTime.UtcNow;
        RequestConfigurationSave();
        QueueGilLedgerUpload();
        RecordDiagnostic($"Gil ledger: {delta:+#,##0;-#,##0} gil; {classification.Confidence}/{classification.Kind}{(classification.ItemId is null ? "" : $"; item={classification.ItemId} x{classification.ItemQuantity}")}{(recent is null ? "" : $"; log {recent.LogMessageId}, ints=[{string.Join(",", recent.IntegerParameters)}]")}{(recentChatSale is null ? "" : "; chat=vendor_sale")}.");
    }

    // Native stabilization stays in CaptureGilLedgerChange; this consumes only the stable read.
    private void ObserveStableRetainerBalance(RetainerBalanceRead? retainerBalance) {
        if (retainerBalance is not null
            && (CurrentState.RetainerGilBaselinesNeedingRefresh ??= new(StringComparer.Ordinal)).Remove(retainerBalance.RetainerId)) {
            // Retain old values through the gap, but never use them to infer a missed transfer.
            (CurrentState.RetainerGilBalances ??= new(StringComparer.Ordinal))[retainerBalance.RetainerId] = retainerBalance.Gil;
            lastObservedRetainerId = retainerBalance.RetainerId;
            lastObservedRetainerGil = retainerBalance.Gil;
            RequestConfigurationSave();
            return;
        }
        if (retainerBalance is not null && lastObservedRetainerId != retainerBalance.RetainerId) {
            CurrentState.RetainerGilBalances ??= new Dictionary<string, long>(StringComparer.Ordinal);
            if (CurrentState.RetainerGilBalances.TryGetValue(retainerBalance.RetainerId, out var priorGil)) {
                if (priorGil != retainerBalance.Gil) RecordRetainerBalanceChange(retainerBalance, retainerBalance.Gil - priorGil);
            } else {
                CurrentState.RetainerGilBalances[retainerBalance.RetainerId] = retainerBalance.Gil;
                RequestConfigurationSave();
            }
            lastObservedRetainerId = retainerBalance.RetainerId;
            lastObservedRetainerGil = retainerBalance.Gil;
        }
        else if (retainerBalance is not null && lastObservedRetainerGil is not null && retainerBalance.Gil > lastObservedRetainerGil.Value) {
            var retainerDelta = retainerBalance.Gil - lastObservedRetainerGil.Value;
            lastObservedRetainerGil = retainerBalance.Gil;
            var confirmedRetainerDeposit = RecordRetainerBalanceChange(retainerBalance, retainerDelta);
            var confirmedRetainerSales = confirmedRetainerDeposit ? new List<PendingRetainerSale>() : ConfirmPendingRetainerSales(retainerDelta, retainerBalance.RetainerId);
            if (confirmedRetainerSales.Count > 0) {
                CurrentState.PendingGilLedgerEvents ??= [];
                foreach (var sale in confirmedRetainerSales) CurrentState.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(sale.Amount, "retainer_sale", "confirmed", sale.ItemId, sale.ItemQuantity, sale.RetainerId, sale.RetainerName, null, []));
                RequestConfigurationSave();
                QueueGilLedgerUpload();
                RecordDiagnostic($"Retainer gil ledger: +{retainerDelta:#,##0} gil; confirmed/retainer_sale; retainer={retainerBalance.RetainerName}; town={retainerBalance.Town}; items={confirmedRetainerSales.Count}.");
            }
        }
        else if (retainerBalance is not null && lastObservedRetainerGil is not null && retainerBalance.Gil < lastObservedRetainerGil.Value) {
            var retainerDelta = retainerBalance.Gil - lastObservedRetainerGil.Value;
            lastObservedRetainerGil = retainerBalance.Gil;
            RecordRetainerBalanceChange(retainerBalance, retainerDelta);
        }
    }

    private bool RecordRetainerBalanceChange(RetainerBalanceRead retainer, long delta) {
        if (delta == 0) return false;
        CurrentState.RetainerGilBalances ??= new Dictionary<string, long>(StringComparer.Ordinal);
        CurrentState.RetainerGilBalances[retainer.RetainerId] = retainer.Gil;
        var confirmedDeposit = false;
        if (delta > 0) confirmedDeposit = ConfirmPendingRetainerGilDeposits(retainer, delta);
        if (delta < 0) {
            recentRetainerWithdrawals.Add(new RetainerWithdrawalObservation(DateTime.UtcNow, -delta, retainer.RetainerId, retainer.RetainerName));
            TransientEvidencePolicy.Prune(recentRetainerWithdrawals, DateTime.UtcNow, TimeSpan.FromSeconds(RetainerReceiptCorrelationSeconds), entry => entry.ObservedAtUtc);
            ConfirmPendingRetainerGilReceipts(retainer, -delta);
        }
        RequestConfigurationSave();
        RecordDiagnostic($"Retainer balance: {delta:+#,##0;-#,##0} gil; retainer={retainer.RetainerName}; current={retainer.Gil:#,##0}; retained locally for receipt correlation.");
        return confirmedDeposit;
    }

    private void QueueRetainerGilReceipt(GilLedgerEvent receipt) {
        CurrentState.PendingRetainerGilReceipts ??= [];
        var withdrawal = recentRetainerWithdrawals.LastOrDefault(entry => entry.Amount == receipt.GilDelta && entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (withdrawal is not null) {
            QueueGilLedgerEvent(receipt with { RetainerId = withdrawal.RetainerId, RetainerName = withdrawal.RetainerName });
            QueueGilLedgerUpload();
            return;
        }
        CurrentState.PendingRetainerGilReceipts.Add(receipt);
    }

    private void ConfirmPendingRetainerGilReceipts(RetainerBalanceRead retainer, long amount) {
        if (CurrentState.PendingRetainerGilReceipts is not { Count: > 0 }) return;
        var receipt = CurrentState.PendingRetainerGilReceipts.LastOrDefault(entry => entry.GilDelta == amount && entry.OccurredAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (receipt is null) return;
        CurrentState.PendingRetainerGilReceipts.Remove(receipt);
        QueueGilLedgerEvent(receipt with { RetainerId = retainer.RetainerId, RetainerName = retainer.RetainerName });
        QueueGilLedgerUpload();
        RecordDiagnostic($"Gil ledger: +{amount:#,##0} gil; inferred/retainer_gil_receipt; retainer={retainer.RetainerName}.");
    }

    private bool FlushExpiredRetainerGilReceipts() {
        if (CurrentState.PendingRetainerGilReceipts is not { Count: > 0 }) return false;
        var cutoff = DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds);
        var expired = CurrentState.PendingRetainerGilReceipts.Where(entry => entry.OccurredAtUtc <= cutoff).ToArray();
        if (expired.Length == 0) return false;
        CurrentState.PendingRetainerGilReceipts.RemoveAll(entry => entry.OccurredAtUtc <= cutoff);
        foreach (var receipt in expired) QueueGilLedgerEvent(receipt);
        return true;
    }

    private void QueueRetainerGilDeposit(GilLedgerEvent deposit) {
        CurrentState.PendingRetainerGilDeposits ??= [];
        CurrentState.PendingRetainerGilDeposits.Add(deposit);
    }

    private bool ConfirmPendingRetainerGilDeposits(RetainerBalanceRead retainer, long amount) {
        if (CurrentState.PendingRetainerGilDeposits is not { Count: > 0 }) return false;
        var deposit = CurrentState.PendingRetainerGilDeposits.LastOrDefault(entry => -entry.GilDelta == amount && entry.OccurredAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (deposit is null) return false;
        CurrentState.PendingRetainerGilDeposits.Remove(deposit);
        QueueGilLedgerEvent(deposit with { Kind = "retainer_gil_deposit", RetainerId = retainer.RetainerId, RetainerName = retainer.RetainerName });
        QueueGilLedgerUpload();
        RecordDiagnostic($"Gil ledger: -{amount:#,##0} gil; inferred/retainer_gil_deposit; retainer={retainer.RetainerName}.");
        return true;
    }

    private bool FlushExpiredRetainerGilDeposits() {
        if (CurrentState.PendingRetainerGilDeposits is not { Count: > 0 }) return false;
        var cutoff = DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds);
        var expired = CurrentState.PendingRetainerGilDeposits.Where(entry => entry.OccurredAtUtc <= cutoff).ToArray();
        if (expired.Length == 0) return false;
        CurrentState.PendingRetainerGilDeposits.RemoveAll(entry => entry.OccurredAtUtc <= cutoff);
        foreach (var deposit in expired) QueueGilLedgerEvent(deposit);
        return true;
    }

    private void QueueGilLedgerEvent(GilLedgerEvent entry) {
        CurrentState.PendingGilLedgerEvents ??= [];
        CurrentState.PendingGilLedgerEvents.Add(entry);
    }

    private static GilLedgerClassification ClassifyGilLedgerEvent(long gilDelta, GilLedgerLogEvidence? evidence, GilLedgerChatEvidence? chatSale) {
        // These contracts were verified through the beta build with a merchant
        // purchase and sale. Keep the amount comparison so an unrelated log
        // sharing the same ID cannot be promoted solely by its timing.
        if (evidence?.IntegerParameters is { Length: >= 1 } values) {
            if (evidence.LogMessageId == 1605 && gilDelta > 0 && gilDelta == values[0])
                return new GilLedgerClassification("quest_or_leve_reward", "confirmed", null, null);
            if (evidence.LogMessageId == 952 && gilDelta > 0)
                return new GilLedgerClassification("quest_or_leve_reward", "confirmed", null, null);
            if ((evidence.LogMessageId == 1603 || evidence.LogMessageId == 1606 || evidence.LogMessageId == 1607) && gilDelta > 0)
                return new GilLedgerClassification("quest_or_leve_reward", "inferred", null, null);
            // Allied society reputation progress and allied-society quest-unlock
            // messages occur with the resulting quest reward. They prove the broad
            // activity but do not include the exact Gil amount, so retain inferred
            // confidence rather than claiming the amount came from the message.
            if ((evidence.LogMessageId == 3231 || evidence.LogMessageId == 10452) && gilDelta > 0)
                return new GilLedgerClassification("quest_or_leve_reward", "inferred", null, null);
            if ((evidence.LogMessageId == 533 || evidence.LogMessageId == 732 || evidence.LogMessageId == 733) && gilDelta < 0 && values.Length == 3 && values.All(value => value == 0))
                return new GilLedgerClassification("teleport", "confirmed", null, null);
            if (evidence.LogMessageId == 1385 && gilDelta < 0)
                return new GilLedgerClassification("repair", "confirmed", null, null);
            if (values.Length < 2) return new GilLedgerClassification("unclassified", "inferred", null, null);
            var normalizedItemId = NormalizeLedgerItemId(values[0]);
            var itemId = normalizedItemId > 0 ? normalizedItemId : (int?)null;
            var quantity = values[1] is > 0 and <= 9999 ? values[1] : (int?)null;
            if (evidence.LogMessageId == 734 && itemId is not null && quantity is not null && gilDelta < 0)
                return new GilLedgerClassification("market_purchase", "confirmed", itemId, quantity);
            if (values.Length >= 3) {
                var amount = values[2];
                if (evidence.LogMessageId == 1687 && amount > 0 && itemId is not null && quantity is not null && gilDelta == -amount)
                    return new GilLedgerClassification("vendor_purchase", "confirmed", itemId, quantity);
                if (evidence.LogMessageId == 1688 && amount > 0 && itemId is not null && quantity is not null && gilDelta == amount)
                    return new GilLedgerClassification("vendor_sale", "confirmed", itemId, quantity);
            }
        }
        if (evidence?.LogMessageId == 736 && gilDelta > 0)
            return new GilLedgerClassification("retainer_gil_receipt", "inferred", null, null);
        if (chatSale is not null && gilDelta == chatSale.Amount)
            return new GilLedgerClassification(chatSale.IsRetainerSale ? "retainer_sale" : "vendor_sale", "confirmed", chatSale.ItemId, chatSale.ItemQuantity, chatSale.RetainerId, chatSale.RetainerName);
        return new GilLedgerClassification("unclassified", "inferred", null, null);
    }

    private bool FlushRetainerChatEvidence() {
        var changed = false;
        var cutoff = DateTime.UtcNow.AddSeconds(-1.5);
        foreach (var evidence in recentGilLedgerChat.Where(entry => entry.IsRetainerSale && entry.ObservedAtUtc <= cutoff && !emittedRetainerChatEvidence.ContainsKey(entry.EvidenceId)).ToArray()) {
            CurrentState.PendingRetainerSales ??= [];
            if (!CurrentState.PendingRetainerSales.Any(sale => sale.SaleId == evidence.EvidenceId)) {
                CurrentState.PendingRetainerSales.Add(new PendingRetainerSale(
                    evidence.EvidenceId, evidence.ObservedAtUtc, evidence.Amount, evidence.ItemId, evidence.ItemQuantity, evidence.RetainerId, evidence.RetainerName));
                changed = true;
            }
            emittedRetainerChatEvidence[evidence.EvidenceId] = DateTime.UtcNow;
            RecordDiagnostic($"Pending retainer sale: +{evidence.Amount:#,##0} gil{(evidence.ItemId is null ? "" : $"; item={evidence.ItemId} x{evidence.ItemQuantity}")}{(evidence.RetainerName is null ? "" : $"; retainer={evidence.RetainerName}")}.");
        }
        return changed;
    }

    private List<PendingRetainerSale> ConfirmPendingRetainerSales(long withdrawalAmount, string? retainerId) {
        CurrentState.PendingRetainerSales ??= [];
        return GilLedgerPolicy.Confirm(CurrentState.PendingRetainerSales, withdrawalAmount, retainerId);
    }

    private static readonly Regex VendorSaleChatRegex = new(
        @"^You sell (?<quantity>[0-9,]+|a|an) .+? for (?<amount>[0-9,]+) gil\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Log-message item parameters encode high-quality items by adding one
    // million. Ledger links must use the base catalog ID; quality is not a
    // separate item page.
    private static int NormalizeLedgerItemId(int itemId) => itemId is >= 1_000_000 and < 2_000_000 ? itemId - 1_000_000 : itemId;

    private string NextAutomaticScope() {
        var scopes = SyncScopes;
        if (automaticScopeIndex >= scopes.Length) automaticScopeIndex = 0;
        var scope = scopes[automaticScopeIndex];
        automaticScopeIndex = (automaticScopeIndex + 1) % scopes.Length;
        return scope;
    }

    private void QueueGilLedgerUpload() {
        if (configuration.AutomaticSync && !string.IsNullOrWhiteSpace(configuration.DeviceToken))
            nextGilLedgerUploadUtc = DateTime.UtcNow.AddMilliseconds(250);
    }

    private async Task SyncAutomaticallyAsync(IEnumerable<string> scopes, SyncRequestMode mode = SyncRequestMode.Automatic) {
        await SyncAsync(force: false, background: true, scopes: scopes, mode: mode);
    }

    private async Task SyncAsync(bool force = true, bool background = false, IEnumerable<string>? scopes = null, SyncRequestMode mode = SyncRequestMode.Manual) {
        CapturedSnapshotBatch? captured = null;
        SyncRequestPermit? operationPermit = null;
        var ownsSync = false;
        try {
            captured = await framework.RunOnFrameworkThread(() => {
                var captureStopwatch = Stopwatch.StartNew();
#if GILLIONS_TEST_BUILD
                // Manual personal refresh is independent of ordinary-sync flight
                // and opt-out. It never overrides a failure backoff or permissions.
                if (!disposed && force && mode == SyncRequestMode.Manual) {
                    RefreshSessionContext();
                    if (PersonalEnabled("hunt_bills")) {
                        huntLocal.Tick(DateTime.UtcNow, force: true);
                        RecordDiagnostic(huntLocal.LastDiagnostic);
                    }
                    if (HasPairedSession && activeOwnedState is not null && clientState.IsLoggedIn)
                        TickPersonalSync(DateTime.UtcNow, prompt: true);
                }
#endif
                if (disposed || syncInFlight) throw new OperationCanceledException();
                var permit = CapturePermit(mode);
                operationPermit = permit;
                syncInFlight = true; ownsSync = true;
                var state = CurrentState;
                var selectedScopes = (scopes ?? RetainerClientPolicy.BuildSyncScopes(SyncScopes, retainerUploadServerSupported))
                    .Where(scope => retainerUploadServerSupported || scope != RetainerClientPolicy.ResourceType).ToArray();
                // An empty Gil flush and unrelated resources do no Retainer work.
                if (selectedScopes.Contains(RetainerClientPolicy.ResourceType, StringComparer.Ordinal)) {
                    if (CaptureRetainerResult(state, out _)) RequestConfigurationSave();
                    var now = DateTime.UtcNow;
                    if (now >= nextRetainerVentureRosterCaptureUtc || !background) {
                        if (DirectGameSnapshotCollector.CaptureRetainerVentureRosterAndGear(state.RetainerState)) RequestConfigurationSave();
                        nextRetainerVentureRosterCaptureUtc = now.AddMilliseconds(NormalVentureRosterCaptureIntervalMilliseconds);
                    }
                    if (DirectGameSnapshotCollector.CaptureRetainerInventoryCoverage(state.RetainerState)) RequestConfigurationSave();
                    savePolicy.Refresh();
                }
                var snapshots = DirectGameSnapshotCollector.Collect(pluginInterface, clientState, objects, dataManager, unlockState,
                    state.RetainerGilBalances, state.RetainerState, selectedScopes).ToArray();
                if (!background && selectedScopes.Contains("shared_fates", StringComparer.Ordinal)) RecordDiagnostic(SharedFateCollector.LastAttemptDiagnostic);
                var batch = new CapturedSnapshotBatch(state.CharacterName, state.CharacterWorld, permit, snapshots,
                    state.PendingGilLedgerEvents.Select(entry => entry with { LogIntegerParameters = entry.LogIntegerParameters.ToArray() }).ToArray(),
                    state.GilLedgerSessionId, new(state.LastPayloadHashes, StringComparer.Ordinal),
                    new(state.LastInventoryComponentHashes, StringComparer.Ordinal), IsDiagnosticRecording);
                captureStopwatch.Stop();
                if (batch.RecordDiagnostics) RecordDiagnostic($"Native collection [{string.Join(",", selectedScopes)}]: {captureStopwatch.Elapsed.TotalMilliseconds:N2} ms on framework thread; {snapshots.Length} snapshots.");
                return batch;
            });
            var snapshots = captured.Snapshots;
            // Managed preparation receives only copied data and no live config.
            var preparedSnapshots = await Task.Run(() => snapshots.Select(snapshot => PrepareSnapshot(snapshot, captured.RecordDiagnostics)).ToArray(), captured.Permit.Cancellation);
            var submitted = 0;
            foreach (var snapshot in preparedSnapshots) {
                if (!force && snapshot.SentResultFingerprints.Count == 0 && captured.PayloadHashes.TryGetValue(snapshot.ResourceType, out var previousHash)
                    && previousHash == snapshot.PayloadHash) continue;
                var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                using var request = SnapshotRequest("/api/game-sync/sync", captured.Permit, snapshot.ResourceType, nonce, snapshot.PayloadUtf8);
                using var response = await SendAsync(request, captured.Permit);
                await EnsureSuccessfulResponse(response, captured.Permit.Cancellation);
                var receipt = await SyncResponsePolicy.ReadAsync(response.Content, captured.Permit.Cancellation);
                string[] acceptedEventIds = [];
                if (snapshot.ResourceType == RetainerClientPolicy.ResourceType) {
                    if (!RetainerAcknowledgementPolicy.TryParseExact(receipt, snapshot.SentResultFingerprints.Keys, out acceptedEventIds))
                        throw new InvalidOperationException("Invalid Retainer receipt.");
                } else if (!SyncResponsePolicy.IsSnapshotReceipt(receipt, snapshot.ResourceType, snapshot.AllowEmptySnapshotReceipt))
                    throw new InvalidOperationException("Invalid sync receipt.");
                await CommitAsync(captured.Permit, state => {
                    var retired = 0;
                    if (snapshot.ResourceType == RetainerClientPolicy.ResourceType) {
                        var pendingBefore = state.RetainerState.PendingResultEvents.Count;
                        RetainerVentureSnapshotPolicy.AcknowledgeResults(state.RetainerState, acceptedEventIds, snapshot.SentResultFingerprints);
                        retired = pendingBefore - state.RetainerState.PendingResultEvents.Count;
                        if (state.RetainerState.PendingResultEvents.Count > 0) {
                            if (acceptedEventIds.Length < snapshot.SentResultFingerprints.Count)
                                nextRetainerUploadUtc = DateTime.UtcNow.AddSeconds(AutomaticFailureRetrySeconds);
                            else QueueRetainerUpload(DateTime.UtcNow);
                        }
                    }
                    state.LastPayloadHashes[snapshot.ResourceType] = snapshot.PayloadHash;
                    if (snapshot.InventoryComponentHashes is not null) state.LastInventoryComponentHashes = snapshot.InventoryComponentHashes;
                    state.LastSyncUtc = DateTime.UtcNow;
                    AfterAcknowledgedDrain(retired);
                    if (captured.RecordDiagnostics) RecordDiagnostic($"Uploaded {snapshot.ResourceType}: HTTP {(int)response.StatusCode}; {snapshot.PayloadUtf8.Length:N0} bytes.");
                });
                submitted++;
            }
            foreach (var events in captured.GilEvents.Chunk(200)) {
                var payload = GilLedgerPolicy.PreparePayload(captured.CharacterName, captured.CharacterWorld, captured.GilSessionId, events);
                var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                using var request = SnapshotRequest("/api/game-sync/sync", captured.Permit, "gil_ledger", nonce, payload);
                using var response = await SendAsync(request, captured.Permit);
                await EnsureSuccessfulResponse(response, captured.Permit.Cancellation);
                if (!SyncResponsePolicy.IsSnapshotReceipt(await SyncResponsePolicy.ReadAsync(response.Content, captured.Permit.Cancellation), "gil_ledger"))
                    throw new InvalidOperationException("Invalid Gil receipt.");
                await CommitAsync(captured.Permit, state => {
                    var retired = GilLedgerPolicy.Acknowledge(state.PendingGilLedgerEvents, events);
                    state.LastSyncUtc = DateTime.UtcNow; AfterAcknowledgedDrain(retired);
                    RecordDiagnostic($"Uploaded Gil records: {events.Length}; HTTP {(int)response.StatusCode}.");
                });
                submitted++;
            }
            await CommitAsync(captured.Permit, _ => {
                if (submitted > 0 && !string.IsNullOrEmpty(configuration.SyncBlockedCode)) {
                    configuration.SyncBlockedCode = ""; configuration.SyncBlockedMessage = ""; RequestConfigurationSave();
                }
                if (!background) settingsMessage = submitted > 0 ? "Sync completed." : "Your supported data is already current.";
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (disposed || operationPermit is not null && !PermitIsCurrent(operationPermit)) return;
                if (error is GillionsSyncRejectedException rejection && IsAccountAccessBlocked(rejection.Code)) {
                    configuration.AutomaticSync = false; configuration.SyncBlockedCode = rejection.Code;
                    configuration.SyncBlockedMessage = OperationFailureMessage(error); RequestConfigurationSave(); RefreshSessionContext();
                } else if (background && ownsSync) {
                    if (activeOwnedState?.PendingGilLedgerEvents.Count > 0) nextGilLedgerUploadUtc = DateTime.UtcNow.AddSeconds(AutomaticFailureRetrySeconds);
                    // A Retainer retry always schedules that resource explicitly.
                    if (activeOwnedState?.RetainerState.PendingResultEvents.Count > 0 || scopes?.Contains(RetainerClientPolicy.ResourceType) == true)
                        nextRetainerUploadUtc = DateTime.UtcNow.AddSeconds(AutomaticFailureRetrySeconds);
                }
                if (!background || error is GillionsSyncRejectedException) settingsMessage = OperationFailureMessage(error);
                RecordDiagnostic($"Sync did not complete ({error.GetType().Name}); pending records were preserved.");
            });
            log.Debug("Gillions sync did not complete ({Type}).", error.GetType().Name);
        } finally {
            if (ownsSync && !disposed) await framework.RunOnFrameworkThread(() => { if (!disposed) syncInFlight = false; });
        }
    }

    private void QueueUiAction(System.Action action) {
        if (disposed) return;
        _ = framework.RunOnFrameworkThread(() => {
            if (!disposed) { action(); uiRefreshPolicy.Reset(); }
        });
    }

    private void PublishUiState() {
        if (disposed) return;
        var now = DateTime.UtcNow;
        if (!uiRefreshPolicy.ShouldRefresh(settingsVisible, now)) return;
        if (dataDetailsExpanded && now >= nextUiDetailsUtc) {
            nextUiDetailsUtc = now.AddSeconds(1);
            uiBudget = evidenceBudget.Measure(configuration.OwnedCharacters.Values);
            uiAvailability = NativeInventoryCollector.GetAvailabilityStatus();
        }
        var usage = dataDetailsExpanded ? uiBudget : null;
        string[] diagnosticLines = [];
        if (diagnosticsExpanded) lock (diagnosticsLock) diagnosticLines = diagnostics.ToArray();
        uiState = new PluginUiSnapshot(
            PluginWindowModel.Create(HasPairedSession, configuration.PairingRequired, clientState.IsLoggedIn,
                configuration.AutomaticSync, syncInFlight, configuration.CoverageGap.Paused,
                configuration.CoverageGap.StartedAtUtc is not null, configuration.SyncBlockedCode),
            HasPairedSession, pairingInFlight, configuration.AutomaticSync, configuration.EnableItemLinkRequests,
            PartyFinderContributionEnabled,
            configuration.ActiveSession?.Origin ?? "", activeOwnedState?.LastSyncUtc, settingsMessage,
            configuration.LastReadChangelogVersion, retainerUploadServerSupported,
            dataDetailsExpanded ? uiAvailability : "", usage,
            IsDiagnosticRecording, diagnosticRecordingUntilUtc, diagnosticLines);
    }

    private void DrawSettings() {
        if (!settingsVisible || disposed) { dataDetailsExpanded = false; diagnosticsExpanded = false; return; }
        var view = uiState;
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(470, 0), ImGuiCond.FirstUseEver);
        var windowOpen = settingsVisible;
        var contentVisible = ImGui.Begin("Gillions Game Sync", ref windowOpen);
        settingsVisible = windowOpen;
        if (!contentVisible) { dataDetailsExpanded = false; diagnosticsExpanded = false; ImGui.End(); return; }
        ImGui.Text(view.Model.Connection);
        ImGui.TextWrapped(view.Model.Status);
        if (view.Model.Warning is not null) { ImGui.PushTextWrapPos(0); ImGui.TextColored(new System.Numerics.Vector4(1f, .7f, .4f, 1f), view.Model.Warning); ImGui.PopTextWrapPos(); }
        if (!view.Paired) DrawPairingControls(view);
        ImGui.Separator();
        var automaticSync = view.Automatic;
        if (ImGui.Checkbox("Automatic sync", ref automaticSync)) QueueUiAction(() => {
            configuration.AutomaticSync = automaticSync; RefreshSessionContext(); RequestConfigurationSave();
        });
        ImGui.BeginDisabled(!view.Model.CanSync);
        if (ImGui.Button("Sync now")) _ = SyncAsync();
        ImGui.EndDisabled();
        var enableItemLinks = view.ItemLinks;
#if GILLIONS_TEST_BUILD
        const string websiteRequestLabel = "Allow website 'Open in FFXIV' requests";
#else
        const string websiteRequestLabel = "Allow website 'Link in game' requests";
#endif
        if (ImGui.Checkbox(websiteRequestLabel, ref enableItemLinks)) QueueUiAction(() => {
            configuration.EnableItemLinkRequests = enableItemLinks; RefreshSessionContext(); RequestConfigurationSave();
        });
#if GILLIONS_TEST_BUILD
        var pfLinks = configuration.EnablePartyFinderLinkRequests;
        if (ImGui.Checkbox("Include Party Finder native links (final in-game click)", ref pfLinks)) QueueUiAction(() => {
            configuration.EnablePartyFinderLinkRequests = pfLinks; RefreshSessionContext(); RequestConfigurationSave();
        });
        ImGui.TextWrapped("New action class: separately OFF for existing and new users. Secure TEST only; compatible Site request contract required. Never joins or applies. Item links retain their existing preference.");
        ImGui.TextWrapped(partyFinderLinkStatus);
        var huntMapGuidance = configuration.AutomaticallyShowHuntMap;
        if (ImGui.Checkbox("Automatically show my current Hunt in FFXIV", ref huntMapGuidance)) QueueUiAction(() => {
            configuration.AutomaticallyShowHuntMap = huntMapGuidance; RefreshSessionContext(); RequestConfigurationSave();
        });
        ImGui.TextWrapped("Separate default-OFF map permission. Pair with secure TEST, then enable Hunt guidance and select this device on Site. Public Hunt areas are references, not sightings. No remote teleport. Site Show in FFXIV can retry a missed map; v1 does not support Next possible location.");
        ImGui.TextWrapped(huntMapStatus);
#endif
        ImGui.Separator();
        var enablePartyFinderContributions = view.PartyFinderContributions;
        if (ImGui.Checkbox("Contribute public Party Finder listings", ref enablePartyFinderContributions)) QueueUiAction(() => {
#if GILLIONS_TEST_BUILD
            configuration.EnableGillionsPartyFinderContributions = enablePartyFinderContributions;
#else
            configuration.EnablePartyFinderContributions = enablePartyFinderContributions;
#endif
            RequestConfigurationSave();
            partyFinderContributor.SetEnabled(enablePartyFinderContributions);
        });
#if GILLIONS_TEST_BUILD
        ImGui.TextWrapped("Off by default. Pair on the approved secure TEST origin as Testing and explicitly grant public Party Finder contribution permission, then opt in here. Public listing names/descriptions, owner ID lower bits, worlds, duties, jobs and slots go to TEST Gillions HTTPS, not xivpf.com. No production fallback. Automatic sync is independent. No active game queries are made.");
        ImGui.TextWrapped("The paired-device credential authenticates only to Gillions. No chat, Square Enix credentials, diagnostics or unrelated local data is submitted. Reporter identity is private; listing owners may be other players. Disable to cancel and clear unsent observations.");
        if (partyFinderContributor is GillionsPartyFinderContributor intake) ImGui.TextWrapped(intake.Status);
#else
        ImGui.TextWrapped("Off by default and independent of Gillions pairing or sync. When enabled, public listing names and descriptions, owner ID lower bits, worlds, duty/settings, jobs and slots are batched and sent directly to xivpf.com. They never pass through Gillions.");
        ImGui.TextWrapped("No chat, Gillions account or device credential, Square Enix credential, diagnostic, or unrelated local data is included.");
#endif
        ImGui.TextWrapped("Data provided by xivpf.com — https://xivpf.com");
        if (ImGui.Button("Open xivpf.com")) Util.OpenLink("https://xivpf.com");
        ImGui.Separator();
        if (!string.IsNullOrWhiteSpace(view.Message)) ImGui.TextWrapped(view.Message);
        if (view.LastSync is { } lastSync) ImGui.TextDisabled($"Last sync: {lastSync.ToLocalTime():g}");
        if (showPairingDetails) { ImGui.SetNextItemOpen(true, ImGuiCond.Always); showPairingDetails = false; }
        if (ImGui.CollapsingHeader("Connection details")) {
            ImGui.TextWrapped("A successful pair connects this plugin to the HTTPS server below. Editing this address takes effect only when you pair again.");
            if (ImGui.InputText("Server address", ref uiServerAddress, 256)) {
                var address = uiServerAddress;
                QueueUiAction(() => { configuration.ServerUrl = address; RequestConfigurationSave(); });
            }
            if (view.Paired) {
                ImGui.TextWrapped($"Connected to {view.Origin}");
                ImGui.TextWrapped("Pairing again starts a new local record set. Earlier pending records remain saved and inactive; they are not moved to the new account.");
                DrawPairingControls(view);
                if (ImGui.Button("Disconnect")) QueueUiAction(() => {
                    configuration.PairingRequired = true; configuration.ActiveSession = null;
                    configuration.DeviceToken = ""; configuration.DeviceId = ""; configuration.PairingCode = "";
                    ResetSessionContext(); RequestConfigurationSave(); settingsMessage = "Disconnected. Saved history remains on this PC.";
                });
            }
        }
        if (ImGui.CollapsingHeader("What changed")) {
            foreach (var entry in CurrentChangelog) ImGui.BulletText(entry);
            if (view.ReadChangelogVersion != PluginVersion && ImGui.Button("Mark as read")) QueueUiAction(() => {
                configuration.LastReadChangelogVersion = PluginVersion; RequestConfigurationSave();
            });
        }
        dataDetailsExpanded = ImGui.CollapsingHeader("Data and status");
        if (dataDetailsExpanded) {
            ImGui.TextWrapped("Automatic sync checks supported data one category at a time every 30 seconds. Inventory changes and queued records upload promptly. Gil checks keep their two-second fallback and short change delay.");
            ImGui.TextWrapped("Pairing and paired startup load character details and send presence once, even with Automatic sync off. Website item links use their separate switch. No gameplay controls are used.");
            ImGui.TextWrapped(view.Availability);
            ImGui.TextWrapped(view.RetainerSupported ? "Retainer observations can upload." : "Retainer observations wait for server compatibility confirmation.");
            if (view.Budget is { } usage) ImGui.TextWrapped($"Pending records across paired sessions and characters: {usage.Records:N0} / 10,000; {usage.Bytes:N0} / 8,388,608 serialized bytes.");
            ImGui.TextWrapped("Older records with unknown ownership remain inactive in local configuration and are outside this new storage limit. Do not share your plugin configuration: it includes a credential and private gameplay history.");
        }
#if GILLIONS_TEST_BUILD
        var huntSync = configuration.SyncPersonalHunts;
        if (ImGui.Checkbox("Sync my private Hunt bills to shared TEST", ref huntSync)) QueueUiAction(() => SetPersonalSync("hunt_bills", huntSync));
        var submarineSync = configuration.SyncPersonalSubmarines;
        if (ImGui.Checkbox("Sync my private submarine state to shared TEST", ref submarineSync)) QueueUiAction(() => SetPersonalSync("submarine_personal", submarineSync));
        ImGui.TextWrapped("Both OFF by default. Requires local retention ON and fresh Testing pairing at https://test.gillions.app with the matching private permission. Never sent to production. Submarines include private names, workshop scope and activity times—not public community results. Disabling sync preserves retained state and all unrelated features.");
        ImGui.TextWrapped(personalStatus);
        if (ImGui.Button("Beastmaster local test")) beastmasterLocal.Show();
        if (ImGui.Button("Submarine voyage retention")) submarineLocal.Show();
        if (ImGui.Button("My Hunt Bills local test")) huntLocal.Show();
        if (ImGui.Button("Private daily / weekly facts")) dashboardLocal.Show();
        var routingLocation = configuration.ShareHuntRoutingLocation;
        if (ImGui.Checkbox("Share my current location for Hunt route recommendations",ref routingLocation)) QueueUiAction(() => {
            configuration.ShareHuntRoutingLocation=routingLocation; travelLocal.SetEnabled(routingLocation);
            ClearTravelPending(); nextRetainerPresenceUtc = DateTime.MinValue; RequestConfigurationSave();
        });
        ImGui.TextWrapped("OFF by default. Private rounded current location and naturally viewed public teleports, latest-only RAM (45s). Only HTTPS test.gillions.app with a compatible paired Testing device and its separate travel permission. OFF cancels pending location uploads; Site expires its last context within 45s. Hunt and all unrelated sync are preserved. No movement history.");
        ImGui.TextWrapped(travelStatus);
        if (ImGui.Button("Private Hunt routing context")) travelLocal.Show();
        ImGui.Separator();
        var marketEnabled = marketContributor.Enabled;
        if (ImGui.Checkbox("Contribute observed market data to Gillions", ref marketEnabled)) QueueUiAction(() => {
            configuration.ContributeObservedMarketData = marketEnabled;
            marketContributor.SetEnabled(marketEnabled); RequestConfigurationSave();
        });
        ImGui.TextWrapped("On by default. Gillions may use market listings and recent sales you naturally view to improve shared market information, without names or player/retainer identities in those observations. No active scanning. Existing pairing authenticates transport; this is not anonymous to the server. Independent of Automatic sync and Dalamud's own contribution setting.");
        ImGui.TextWrapped(marketContributor.Status);
#endif
        DrawDiagnostics(view);
        ImGui.End();
    }

    private void DrawPairingControls(PluginUiSnapshot view) {
        ImGui.TextWrapped(SyncOrigin.TryNormalize(uiServerAddress, out var pairingOrigin)
            ? $"Pair with {pairingOrigin}"
            : "Enter a valid HTTPS server address under Connection details.");
        ImGui.InputText("Pairing code", ref uiPairingCode, 256, ImGuiInputTextFlags.Password);
        ImGui.BeginDisabled(view.Pairing || string.IsNullOrWhiteSpace(uiPairingCode) || !SyncOrigin.TryNormalize(uiServerAddress, out _));
        if (ImGui.Button(view.Pairing ? "Connectingâ€¦" : "Pair this device")) {
            var code = uiPairingCode.Trim(); var address = uiServerAddress; uiPairingCode = "";
            QueueUiAction(() => { configuration.PairingCode = code; configuration.ServerUrl = address; _ = PairAsync(); });
        }
        ImGui.EndDisabled();
    }

    private void DrawDiagnostics(PluginUiSnapshot view) {
        ImGui.Separator();
        diagnosticsExpanded = ImGui.CollapsingHeader("Diagnostics");
        if (!diagnosticsExpanded) return;
#if GILLIONS_TEST_BUILD
        ImGui.TextWrapped("Testing diagnostics record automatically. Diagnostic entries stay local and may include gameplay details. Review copied reports before sharing.");
#else
        ImGui.TextWrapped("Diagnostic recording is off by default and stays on this PC. It never uploads logs, chat text, credentials, or device identifiers. Start it only when reproducing a sync problem; review copied gameplay details before sharing.");
        if (!view.Recording) {
            if (ImGui.Button("Start 10-minute diagnostic recording")) QueueUiAction(() => {
                lock (diagnosticsLock) diagnostics.Clear();
                diagnosticRecordingUntilUtc = DateTime.UtcNow.AddMinutes(10);
                RecordDiagnostic("Manual diagnostic recording started.");
            });
        } else {
            ImGui.TextDisabled($"Recording: {Math.Max(1, (int)Math.Ceiling((view.RecordingUntil - DateTime.UtcNow).TotalMinutes))} minute(s) remaining");
            if (ImGui.Button("Stop diagnostic recording")) QueueUiAction(() => {
                RecordDiagnostic("Manual diagnostic recording stopped."); diagnosticRecordingUntilUtc = DateTime.MinValue;
            });
        }
#endif
        if (view.Diagnostics.Length == 0) { ImGui.TextDisabled("No diagnostic entries recorded."); return; }
        if (ImGui.Button("Copy diagnostic report")) {
#if GILLIONS_TEST_BUILD
            const string channel = "Testing";
#else
            const string channel = "Public";
#endif
            ImGui.SetClipboardText($"Gillions Game Sync {PluginVersion} ({channel})\n" + string.Join("\n", view.Diagnostics));
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear diagnostics")) QueueUiAction(() => { lock (diagnosticsLock) diagnostics.Clear(); });
        foreach (var line in view.Diagnostics) ImGui.TextWrapped(line);
    }

    private bool HasPairedSession => SyncOwnershipPolicy.IsBoundSession(configuration.ActiveSession,
        configuration.PairingRequired, configuration.DeviceId, configuration.DeviceToken);
    private OwnedCharacterState CurrentState => activeOwnedState ?? throw new InvalidOperationException("Pair and log into a character before syncing.");

    private void RequestConfigurationSave() => savePolicy.RequestDurable();
    private void FlushConfigurationSave(bool receivedAuthorizationDenial = false, bool force = false) {
        if (force && disposed) throw new InvalidOperationException("Configuration persistence is unavailable after disposal.");
        var now = DateTime.UtcNow;
        if ((!disposed || receivedAuthorizationDenial) && (force || savePolicy.ShouldSave(now))) {
            configuration.Save(pluginInterface);
            savePolicy.Saved(now);
        }
    }

    private void ClearTransientState() {
        lastObservedGil = null; lastObservedRetainerId = null; lastObservedRetainerGil = null;
        pendingRetainerBalance = null; pendingRetainerBalanceSinceUtc = DateTime.MinValue;
        recentRetainerWithdrawals.Clear(); recentGilLedgerLogs.Clear(); recentGilLedgerChat.Clear(); emittedRetainerChatEvidence.Clear();
        DirectGameSnapshotCollector.ClearTransientState(); itemLinkRequestProcessor = new();
#if GILLIONS_TEST_BUILD
        partyFinderLinkProcessor = new();
#endif
        lock (diagnosticsLock) diagnostics.Clear();
        uiState = PluginUiSnapshot.Empty; uiBudget = null; uiAvailability = ""; nextUiDetailsUtc = DateTime.MinValue;
        uiRefreshPolicy.Reset();
        gilLedgerDirty = false; nextInventorySyncUtc = DateTime.MaxValue; nextGilLedgerUploadUtc = DateTime.MaxValue;
    }

    private void OnLogin() { logoutPending = false; ResetSessionContext(); }
    private void OnLogout(int type, int code) { logoutPending = true; ResetSessionContext(); }
    private void ResetSessionContext() {
        if (disposed) return;
        requestLifetime.Invalidate(); ClearTransientState(); ClearRetainerServerAcceptance();
#if GILLIONS_TEST_BUILD
        travelAccepted = false; travelBinding = ""; ClearTravelPending(); travelLocal?.ClearSession();
        ClearHuntMapRequests();
        foreach (var cancellation in personalCancellation.Values) { cancellation.Cancel(); cancellation.Dispose(); }
        personalCancellation.Clear(); nextPersonalUtc = DateTime.MinValue; personalRetryUtc = DateTime.MinValue;
#endif
        activeOwnedState = null; activeRetainerCharacterContentId = 0; activeGeneration = "";
        nextRetainerPresenceUtc = DateTime.MinValue; nextRetainerUploadUtc = DateTime.MinValue;
    }

    private void RefreshSessionContext() {
        if (disposed) return;
        var contentId = clientState.IsLoggedIn && !logoutPending ? ReadLocalContentId() : 0;
        var generation = HasPairedSession ? configuration.ActiveSession!.Generation : "";
        if (contentId != activeRetainerCharacterContentId || generation != activeGeneration) {
            ResetSessionContext();
            activeRetainerCharacterContentId = contentId; activeGeneration = generation;
#if GILLIONS_TEST_BUILD
            if (contentId != 0 && HasPairedSession) travelBinding = TravelBinding(requestLifetime.Capture(SyncRequestMode.Personal,
                contentId,configuration.ActiveSession,configuration.ActiveSession!.Origin,configuration.DeviceToken));
#endif
            nextAutomaticSyncUtc = DateTime.MinValue; nextGilLedgerPollUtc = DateTime.MinValue;
            nextRetainerListingCaptureUtc = DateTime.MinValue;
            nextRetainerVentureResultCaptureUtc = DateTime.MinValue; nextRetainerVentureRosterCaptureUtc = DateTime.MinValue;
            presenceFailureCount = 0;
        }
        if (activeOwnedState is null && contentId != 0 && HasPairedSession) {
            var name = objects.LocalPlayer?.Name.TextValue ?? "";
            var world = objects.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(world)) {
                activeOwnedState = SyncOwnershipPolicy.GetCharacter(configuration.OwnedCharacters, configuration.ActiveSession!, contentId);
                activeOwnedState.CharacterName = name; activeOwnedState.CharacterWorld = world;
            }
        }
        if (observedAutomaticSync != configuration.AutomaticSync) {
            observedAutomaticSync = configuration.AutomaticSync;
            requestLifetime.InvalidateAutomatic(); ClearTransientState();
            if (observedAutomaticSync) nextAutomaticSyncUtc = DateTime.MinValue;
        }
        if (observedItemLinks != configuration.EnableItemLinkRequests) {
            observedItemLinks = configuration.EnableItemLinkRequests;
            requestLifetime.InvalidateItemLinks();
        }
#if GILLIONS_TEST_BUILD
        if (observedHuntMapGuidance != configuration.AutomaticallyShowHuntMap) {
            observedHuntMapGuidance = configuration.AutomaticallyShowHuntMap;
            ClearHuntMapRequests(); nextItemLinkPollUtc = DateTime.MinValue;
        }
        if (observedPartyFinderLinks != configuration.EnablePartyFinderLinkRequests) {
            observedPartyFinderLinks = configuration.EnablePartyFinderLinkRequests;
            requestLifetime.InvalidateItemLinks();
        }
#endif
    }

    private SyncRequestPermit CapturePermit(SyncRequestMode mode) {
        RefreshSessionContext();
        if (!HasPairedSession || activeOwnedState is null) throw new InvalidOperationException("Pair and log into a character before syncing.");
        var permit = requestLifetime.Capture(mode, activeRetainerCharacterContentId, configuration.ActiveSession,
            configuration.ActiveSession!.Origin, configuration.DeviceToken);
        RequirePermit(permit);
        return permit;
    }
    private bool PartyFinderContributionEnabled {
        get {
#if GILLIONS_TEST_BUILD
            return configuration.EnableGillionsPartyFinderContributions;
#else
            return configuration.EnablePartyFinderContributions;
#endif
        }
    }
    private GillionsPartyFinderSession? CapturePartyFinderSession() {
#if GILLIONS_TEST_BUILD
        if (disposed || !framework.IsInFrameworkUpdateThread || !PartyFinderContributionEnabled || !clientState.IsLoggedIn) return null;
        RefreshSessionContext();
        if (!HasPairedSession || activeOwnedState is null || configuration.ActiveSession!.Origin != GillionsPartyFinderContributor.ApprovedTestingOrigin) return null;
        var permit = CapturePermit(SyncRequestMode.Manual);
        var authorizationGeneration = permit.Session!.Generation;
        return new GillionsPartyFinderSession($"{authorizationGeneration}:{permit.Epoch}:{permit.ContentId}", authorizationGeneration,
            configuration.GillionsPartyFinderBlockedGeneration == authorizationGeneration, permit.Cancellation,
            () => framework.RunOnFrameworkThread(() => {
                // A denial belongs to the captured enrollment, not its character or
                // request epoch. Never apply an old enrollment's denial to a new one.
                if (configuration.ActiveSession?.Generation != authorizationGeneration) return;
                configuration.GillionsPartyFinderBlockedGeneration = authorizationGeneration;
                // Security state must survive an immediate reload; do not coalesce.
                RequestConfigurationSave();
                FlushConfigurationSave(receivedAuthorizationDenial: true);
            }),
            async (body, cancellation) => {
                using var request = new HttpRequestMessage(HttpMethod.Post, GillionsPartyFinderContributor.SessionEndpoint(permit.Origin)) {
                    Content = new ByteArrayContent(body)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", permit.Token);
                request.Headers.UserAgent.ParseAdd($"{RetainerClient.ProductName}/{PluginVersion}");
                await framework.RunOnFrameworkThread(() => {
                    RequirePermit(permit);
                    if (!PartyFinderContributionEnabled) throw new OperationCanceledException();
                });
                cancellation.ThrowIfCancellationRequested();
                return await partyFinderHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            });
#else
        return null;
#endif
    }
#if GILLIONS_TEST_BUILD
    private MarketContributionSession? CaptureMarketSession() {
        if (disposed || !framework.IsInFrameworkUpdateThread || !configuration.ContributeObservedMarketData
            || !clientState.IsLoggedIn || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return null;
        RefreshSessionContext();
        if (!HasPairedSession || activeOwnedState is null || marketAcceptedGeneration != configuration.ActiveSession!.Generation) return null;
        var world = objects.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (world is 0 or > ushort.MaxValue) return null;
        var permit = CapturePermit(SyncRequestMode.Manual);
        var generation = permit.Session!.Generation;
        return new MarketContributionSession($"{generation}:{permit.Epoch}:{world}", generation,
            configuration.GillionsMarketBlockedGeneration == generation, (ushort)world, permit.Cancellation,
            () => framework.RunOnFrameworkThread(() => {
                if (configuration.ActiveSession?.Generation != generation) return;
                configuration.GillionsMarketBlockedGeneration = generation;
                RequestConfigurationSave(); FlushConfigurationSave(receivedAuthorizationDenial: true);
            }),
            async (body, cancellation) => {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(permit.Origin), MarketContributor.Path)) {
                    Content = new ByteArrayContent(body)
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", permit.Token);
                request.Headers.UserAgent.ParseAdd($"{RetainerClient.ProductName}/{PluginVersion}");
                await framework.RunOnFrameworkThread(() => {
                    RequirePermit(permit);
                    if (!configuration.ContributeObservedMarketData || marketAcceptedGeneration != generation
                        || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
                        || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]
                        || objects.LocalPlayer?.CurrentWorld.RowId != world) throw new OperationCanceledException();
                });
                cancellation.ThrowIfCancellationRequested();
                return await marketHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            });
    }
#endif
    private bool PermitIsCurrent(SyncRequestPermit permit) {
        if (disposed) return false;
        RefreshSessionContext();
        return (permit.Mode == SyncRequestMode.Pair || HasPairedSession) && requestLifetime.Accepts(permit,
            activeRetainerCharacterContentId, configuration.ActiveSession, configuration.DeviceToken,
            configuration.AutomaticSync, configuration.EnableItemLinkRequests);
    }
    private void RequirePermit(SyncRequestPermit permit) {
        if (!PermitIsCurrent(permit)) throw new OperationCanceledException("The connection or sync settings changed.");
    }
#if GILLIONS_TEST_BUILD
    private Task<HttpResponseMessage> SendPresenceAsync(HttpRequestMessage request, SyncRequestPermit permit) {
        if (permit.Origin == TravelSyncPolicy.Origin) return SendAsync(request, permit, personalHttp);
        return SendAsync(request, permit);
    }
#endif
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, SyncRequestPermit permit
#if GILLIONS_TEST_BUILD
        , HttpClient? client = null
#endif
    ) {
        if (disposed) throw new OperationCanceledException();
        Task<HttpResponseMessage>? pending = null;
        await framework.RunOnFrameworkThread(() => {
            RequirePermit(permit);
#if GILLIONS_TEST_BUILD
            pending = (client ?? http).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, permit.Cancellation);
#else
            pending = http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, permit.Cancellation);
#endif
        });
        return await pending!;
    }
#if GILLIONS_TEST_BUILD
    private void ClearTravelPending() => travelSync.Clear();
    private string TravelBinding(SyncRequestPermit permit) => $"{permit.Session!.Generation}:{permit.Epoch}:{permit.ContentId}";
    private bool TravelAdmitted() => TravelSyncPolicy.Admit(true, travelLocal.Available(activeRetainerCharacterContentId),
        configuration.ShareHuntRoutingLocation, HasPairedSession && activeOwnedState is not null,
        configuration.ActiveSession?.Origin ?? "", travelAccepted);
    private void TickTravelSync(DateTime now) {
        var admitted = TravelAdmitted();
        var binding = travelBinding;
        var due = travelSync.Maintain(binding,admitted,now);
        if (!configuration.ShareHuntRoutingLocation) { travelStatus = "Travel sync OFF; volatile preparation cleared. Unrelated sync unchanged."; return; }
        if (HasPairedSession && activeOwnedState is not null && configuration.ActiveSession!.Origin == TravelSyncPolicy.Origin
            && !presenceInFlight && now >= nextRetainerPresenceUtc)
            SendCurrentRetainerPresence(activeRetainerCharacterContentId,now,SyncRequestMode.Personal);
        if (!admitted) { travelStatus = "Travel waiting for a current supported character, HTTPS TEST pairing and its separate travel grant/capability. No location sent."; return; }
        if (!due || travelInFlight) return;
        var prepared = travelSync.Prepare(binding,travelLocal.Current(activeRetainerCharacterContentId,now),now);
        if (prepared is null) return;
        var permit = CapturePermit(SyncRequestMode.Personal);
        if (binding != TravelBinding(permit)) { ClearTravelPending(); return; }
        travelSync.Dispatched(now); travelInFlight = true;
        var featureToken = prepared.Lifetime.Token;
        _ = Task.Run(() => SendTravelAsync(permit,prepared,featureToken));
    }
    private async Task SendTravelAsync(SyncRequestPermit permit, TravelPrepared prepared, CancellationToken featureToken) {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation,featureToken);
        var token = cancellation.Token;
        try {
            using var request = SnapshotRequest("/api/game-sync/sync",permit,TravelSyncPolicy.Resource,prepared.Nonce,Encoding.UTF8.GetBytes(prepared.Payload));
            request.Headers.UserAgent.Clear(); request.Headers.UserAgent.ParseAdd($"GillionsGameSyncTest/{PluginVersion}");
            request.Headers.Add("X-Gillions-Personal-Contract",TravelSyncPolicy.Contract);
            request.Headers.Add("X-Gillions-Personal-Resource",TravelSyncPolicy.Resource);
            request.Headers.Add("X-Gillions-Personal-Capability",TravelSyncPolicy.Capability);
            Task<HttpResponseMessage>? pending = null;
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit); token.ThrowIfCancellationRequested();
                if (!TravelAdmitted() || !ReferenceEquals(travelSync.Pending,prepared)
                    || TravelBinding(permit) != prepared.Binding || !prepared.Fresh(DateTime.UtcNow)) throw new OperationCanceledException();
                pending = personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            });
            using var response = await pending!;
            int status = (int)response.StatusCode;
            // Never read error bodies: they may reflect location or identifiers.
            bool receipt = status is 200 or 201 && TravelSyncPolicy.Receipt(status,await SyncResponsePolicy.ReadAsync(response.Content,token));
            double? retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds
                ?? (response.Headers.RetryAfter?.Date is { } date ? (date-DateTimeOffset.UtcNow).TotalSeconds : null);
            await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || token.IsCancellationRequested || !TravelAdmitted()
                    || !ReferenceEquals(travelSync.Pending,prepared)) return;
                if (!prepared.Fresh(DateTime.UtcNow)) { ClearTravelPending(); travelStatus="Travel observation expired; volatile preparation cleared."; return; }
                travelSync.Complete(prepared,receipt,status,retryAfter,DateTime.UtcNow);
                if (receipt) { travelStatus = "Travel context accepted by TEST; freshness remains observation-based (45s)."; RecordDiagnostic("Travel context accepted by TEST."); }
                else if (TravelSyncPolicy.Terminal(status)) { travelAccepted=false; ClearTravelPending(); travelStatus=$"Travel stopped: HTTP {status}; waiting for compatible TEST acknowledgement. No insecure fallback."; }
                else travelStatus = "Travel HTTPS receipt unavailable; bounded retry of the same volatile nonce/body before expiry.";
            });
        } catch (Exception) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || token.IsCancellationRequested || !ReferenceEquals(travelSync.Pending,prepared)) return;
                travelSync.Complete(prepared,false,0,null,DateTime.UtcNow);
                travelStatus = "Travel HTTPS send/receipt unavailable; bounded retry before expiry. Unrelated sync unchanged.";
            });
        } finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => travelInFlight=false);
        }
    }
    private bool PersonalEnabled(string resource) => resource == "hunt_bills"
        ? configuration.SyncPersonalHunts && configuration.HuntBills.LocalRetentionEnabled
        : resource == "submarine_personal" && configuration.SyncPersonalSubmarines && configuration.SubmarineVoyages.LocalRetentionEnabled;
    private void SetPersonalSync(string resource, bool enabled) {
        if (resource == "hunt_bills") configuration.SyncPersonalHunts = enabled;
        else configuration.SyncPersonalSubmarines = enabled;
        if (personalCancellation.Remove(resource, out var previous)) { previous.Cancel(); previous.Dispose(); }
        nextPersonalUtc = DateTime.MinValue; nextRetainerPresenceUtc = DateTime.MinValue;
        personalStatus = enabled ? "Awaiting compatible TEST permission and positive current-character observations."
            : "Private resource sync OFF; retained/pending state preserved. Ordinary sync unchanged.";
        RequestConfigurationSave();
    }
    private void TickPersonalSync(DateTime now, bool prompt = false) {
        foreach (var resource in PersonalSyncPolicy.Resources) {
            var enabled = PersonalEnabled(resource);
            var before = resource == "hunt_bills" ? observedPersonalHunts : observedPersonalSubmarines;
            if (before && !enabled && personalCancellation.Remove(resource, out var canceled)) { canceled.Cancel(); canceled.Dispose(); }
            if (resource == "hunt_bills") observedPersonalHunts = enabled; else observedPersonalSubmarines = enabled;
        }
        if (!PersonalSyncPolicy.Due(now, nextPersonalUtc, personalRetryUtc, personalInFlight, prompt,
            configuration.SyncPersonalHunts || configuration.SyncPersonalSubmarines)) return;
        nextPersonalUtc = now.AddSeconds(5);
        if (configuration.ActiveSession!.Origin != PersonalSyncPolicy.Origin) {
            personalStatus = "Private sync requires pairing at https://test.gillions.app; no personal data sent. Ordinary sync unchanged."; return;
        }
        // Presence remains useful with ordinary automatic sync OFF, but never
        // grants missing server pairing permissions or changes the paired origin.
        if (!presenceInFlight && now >= nextRetainerPresenceUtc) SendCurrentRetainerPresence(activeRetainerCharacterContentId, now, SyncRequestMode.Personal);
        if (!PersonalSyncPolicy.Valid(configuration.PersonalSync)) {
            personalStatus = "Private prepared state invalid/oversized; preserved and fail-closed. Ordinary sync unchanged."; return;
        }
        var owner = PersonalSyncPolicy.Owner(configuration.ActiveSession.Generation, HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId));
        foreach (var resource in PersonalSyncPolicy.Resources) {
            if (!PersonalEnabled(resource) || !personalAccepted.Contains(resource)) continue;
            string? payload;
            try {
                payload = resource == "hunt_bills"
                    ? new HuntBillRetentionPolicy(configuration.HuntBills).PreparePrivateExport(HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId))
                    : submarineLocal.PersonalPayload(activeRetainerCharacterContentId);
            } catch (InvalidOperationException) { continue; }
            // No negative/empty replacement. Submarines wait for verified
            // workshop evidence in this character's session, including reload.
            if (payload is null) continue;
            var prior = configuration.PersonalSync.Prepared.SingleOrDefault(p => p.OwnerKey == owner && p.Resource == resource);
            var prepared = PersonalSyncPolicy.Prepare(configuration.PersonalSync, owner, resource, payload);
            if (prepared is null) { personalStatus = "Private preparation capacity/validation gate: existing pending state preserved; no new admission."; continue; }
            if (!ReferenceEquals(prior, prepared)) {
                RequestConfigurationSave();
                RecordDiagnostic($"Private {resource}: new semantic snapshot prepared; immutable nonce/body, no identifiers logged.");
            }
            if (!PersonalSyncPolicy.CanSend(true, true, configuration.ActiveSession.Origin, true, prepared)) continue;
            // A prior in-memory entry might be a preparation whose save failed.
            // Require actual successful persistence, not save scheduling, before
            // every first/retry dispatch. Preserve its exact nonce/body on failure.
            if (!PersonalSyncPolicy.PersistBeforeSend(() => FlushConfigurationSave(force: true))) {
                personalStatus = "Private snapshot save unavailable; no dispatch. Exact in-memory nonce/body preserved; durable local state not erased.";
                personalRetryUtc = now.AddSeconds(60); nextPersonalUtc = personalRetryUtc; return;
            }
            var permit = CapturePermit(SyncRequestMode.Personal);
            if (!personalCancellation.TryGetValue(resource, out var featureCancellation)) {
                featureCancellation = new CancellationTokenSource(); personalCancellation.Add(resource, featureCancellation);
            }
            personalInFlight = true;
            var featureToken = featureCancellation.Token;
            _ = Task.Run(() => SendPersonalAsync(permit, prepared, featureToken));
            return;
        }
        personalStatus = "Private sync waiting for explicit server permission/new positive observations, or retained state already acknowledged. No empty replacement.";
    }
    private async Task SendPersonalAsync(SyncRequestPermit permit, PersonalPreparedSnapshot prepared, CancellationToken featureToken) {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation, featureToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30)); // Bound headers AND receipt; no indefinitely held private flight.
        var token = cancellation.Token;
        try {
            using var request = SnapshotRequest("/api/game-sync/sync", permit, prepared.Resource, prepared.Nonce, Encoding.UTF8.GetBytes(prepared.Payload));
            request.Headers.Add("X-Gillions-Personal-Contract", PersonalSyncPolicy.Contract);
            Task<HttpResponseMessage>? pending = null;
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit); token.ThrowIfCancellationRequested();
                if (!PersonalEnabled(prepared.Resource) || permit.Origin != PersonalSyncPolicy.Origin || !personalAccepted.Contains(prepared.Resource)) throw new OperationCanceledException();
                pending = personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            });
            using var response = await pending!;
            // Status controls stop/retry independently of an HTML/empty/broken
            // error body. Only success needs bounded receipt JSON validation.
            var terminal = PersonalSyncPolicy.TerminalStatus((int)response.StatusCode);
            var receipt = await PersonalSyncPolicy.ReadReceiptAsync((int)response.StatusCode,
                () => SyncResponsePolicy.ReadAsync(response.Content, token));
            await framework.RunOnFrameworkThread(() => {
                var disposition = PersonalSyncPolicy.Disposition(PermitIsCurrent(permit), permit.Cancellation.IsCancellationRequested,
                    featureToken.IsCancellationRequested, PersonalEnabled(prepared.Resource), receipt, terminal);
                if (disposition == PersonalResponseDisposition.Canceled) return;
                if (disposition == PersonalResponseDisposition.Acknowledged) {
                    prepared.Acknowledged = true; personalFailures = 0; personalRetryUtc = DateTime.MinValue;
                    nextPersonalUtc = DateTime.MinValue; // Drain a newer semantic successor immediately after this receipt.
                    personalStatus = $"{prepared.Resource}: TEST receipt accepted. Positive retained observations only; current ownership/reset/completion claims remain limited.";
                    RecordDiagnostic($"Uploaded private {prepared.Resource} to shared TEST: HTTP {(int)response.StatusCode}; valid receipt. No live correctness claim.");
                } else if (disposition == PersonalResponseDisposition.Blocked) {
                    prepared.Blocked = true;
                    personalStatus = $"{prepared.Resource}: HTTP {(int)response.StatusCode}; private snapshot stopped/preserved. Ordinary sync unchanged.";
                } else {
                    personalFailures++; personalRetryUtc = DateTime.UtcNow.AddSeconds(PersonalSyncPolicy.RetrySeconds(personalFailures)); nextPersonalUtc = personalRetryUtc;
                    personalStatus = $"{prepared.Resource}: bounded retry; same nonce/payload preserved. Ordinary sync unchanged.";
                }
                RequestConfigurationSave();
            });
        } catch (Exception) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || featureToken.IsCancellationRequested || permit.Cancellation.IsCancellationRequested) return;
                personalFailures++; personalRetryUtc = DateTime.UtcNow.AddSeconds(PersonalSyncPolicy.RetrySeconds(personalFailures)); nextPersonalUtc = personalRetryUtc;
                personalStatus = "Private HTTPS send/receipt unavailable; pending nonce/payload retained, bounded backoff. No TLS fallback; ordinary sync unchanged.";
            });
        } finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => { personalInFlight = false; });
        }
    }
#endif
    private async Task CommitAsync(SyncRequestPermit permit, Action<OwnedCharacterState> change) {
        if (disposed) throw new OperationCanceledException();
        await framework.RunOnFrameworkThread(() => { RequirePermit(permit); change(CurrentState); });
    }
    private static string OperationFailureMessage(Exception error) => SyncErrorPolicy.Message(error);

    private void MaintainTransientState(DateTime now) {
        if (now < nextTransientMaintenanceUtc) return;
        nextTransientMaintenanceUtc = now.AddSeconds(1);
        TransientEvidencePolicy.Prune(recentGilLedgerLogs, now, TimeSpan.FromSeconds(5), entry => entry.ObservedAtUtc);
        TransientEvidencePolicy.Prune(recentGilLedgerChat, now, TimeSpan.FromSeconds(5), entry => entry.ObservedAtUtc);
        TransientEvidencePolicy.Prune(recentRetainerWithdrawals, now, TimeSpan.FromSeconds(5), entry => entry.ObservedAtUtc);
        TransientEvidencePolicy.Prune(emittedRetainerChatEvidence, now, TimeSpan.FromSeconds(5), entry => entry);
        NativeInventoryCollector.MaintainTransientState(now);
    }
    private bool CanCaptureLedgerEvidence() {
        if (disposed || !framework.IsInFrameworkUpdateThread || !clientState.IsLoggedIn || !configuration.AutomaticSync || !HasPairedSession) return false;
        RefreshSessionContext();
        return activeOwnedState is not null && !configuration.CoverageGap.Paused;
    }

    private bool CaptureRetainerResult(OwnedCharacterState state, out string status) {
        if (configuration.CoverageGap.Paused) { status = "recording_paused"; return false; }
        var result = DirectGameSnapshotCollector.ReadChangedRetainerResult(state.RetainerState, DateTime.UtcNow, out status);
        if (result is null) return false;
        var checkpoint = new DurableEvidenceCheckpoint(state);
        var changed = RetainerVentureSnapshotPolicy.AddPendingResult(state.RetainerState, result);
        if (changed && !evidenceBudget.AcceptOrRestore(configuration.OwnedCharacters.Values, checkpoint, configuration.CoverageGap, DateTime.UtcNow)) {
            RequestConfigurationSave(); return false;
        }
        return changed;
    }
    private void CaptureOwnedGilLedgerChange() {
        var state = CurrentState;
        if (configuration.CoverageGap.Paused) {
            lastObservedGil = NativeInventoryCollector.ReadGil();
            var balance = DirectGameSnapshotCollector.ReadActiveRetainerGil();
            lastObservedRetainerId = balance?.RetainerId; lastObservedRetainerGil = balance?.Gil;
            gilLedgerDirty = false;
            return;
        }
        var checkpoint = new DurableEvidenceCheckpoint(state);
        CaptureGilLedgerChange();
        if (!evidenceBudget.AcceptOrRestore(configuration.OwnedCharacters.Values, checkpoint, configuration.CoverageGap, DateTime.UtcNow)) {
            // Keep observing balances while paused; no fabricated catch-up delta.
            pendingRetainerBalance = null; recentRetainerWithdrawals.Clear();
            RequestConfigurationSave();
        }
    }
    private void AfterAcknowledgedDrain(int retiredRecords) {
        if (retiredRecords > 0 && evidenceBudget.ResumeAfterDrain(configuration.OwnedCharacters.Values, configuration.CoverageGap, DateTime.UtcNow)) {
            // Backpressure affected every partition, including retainers not currently loaded.
            // Persist invalidation alongside recovery; keep historical values and pending evidence intact.
            foreach (var state in configuration.OwnedCharacters.Values) {
                state.RetainerGilBalances ??= new(StringComparer.Ordinal);
                state.RetainerGilBaselinesNeedingRefresh ??= new(StringComparer.Ordinal);
                state.RetainerGilBaselinesNeedingRefresh.UnionWith(state.RetainerGilBalances.Keys);
            }
            ClearTransientState(); DirectGameSnapshotCollector.ResetResultView();
        }
        RequestConfigurationSave();
    }

    private HttpRequestMessage Request(string path, SyncRequestPermit permit, object body) {
        var request = new HttpRequestMessage(HttpMethod.Post, permit.Origin + path) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", permit.Token);
        request.Headers.UserAgent.ParseAdd($"{RetainerClient.ProductName}/{PluginVersion}");
        return request;
    }

    private HttpRequestMessage SnapshotRequest(string path, SyncRequestPermit permit, string resourceType, string nonce, byte[] payloadUtf8) {
        var buffer = new ArrayBufferWriter<byte>(payloadUtf8.Length + 256);
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            writer.WriteString("resourceType", resourceType);
            writer.WriteString("nonce", nonce);
            writer.WritePropertyName("payload");
            writer.WriteRawValue(payloadUtf8, skipInputValidation: true);
            writer.WriteEndObject();
        }
        var content = new ByteArrayContent(buffer.WrittenSpan.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        var request = new HttpRequestMessage(HttpMethod.Post, permit.Origin + path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", permit.Token);
        request.Headers.UserAgent.ParseAdd($"{RetainerClient.ProductName}/{PluginVersion}");
        return request;
    }

    private static bool IsAccountAccessBlocked(string code) => code is "TRIAL_EXPIRED" or "ACCOUNT_DISABLED";

    private static Task EnsureSuccessfulResponse(HttpResponseMessage response, CancellationToken cancellation = default) =>
        SyncResponsePolicy.EnsureSuccessfulAsync(response, cancellation);

    private PreparedSnapshot PrepareSnapshot(GameSnapshot snapshot, bool recordDiagnostics) {
        var stopwatch = Stopwatch.StartNew();
        var payloadUtf8 = JsonSerializer.SerializeToUtf8Bytes(snapshot.Payload);
        using var document = JsonDocument.Parse(payloadUtf8);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeJson(document.RootElement))));
        var inventoryComponents = snapshot.ResourceType == "inventory" ? GetInventoryComponentHashes(document.RootElement) : null;
        var description = recordDiagnostics ? DescribePayload(document.RootElement, payloadUtf8.Length) : "";
        var inventoryDelta = ".";
        stopwatch.Stop();
        return new PreparedSnapshot(snapshot.ResourceType, payloadUtf8, payloadHash, inventoryComponents, description, inventoryDelta, stopwatch.Elapsed.TotalMilliseconds,
            snapshot.ResourceType == "retainer_ventures"
                ? document.RootElement.GetProperty("resultEvents").EnumerateArray().ToDictionary(entry => entry.GetProperty("eventId").GetString()!, entry => entry.GetProperty("payloadFingerprint").GetString()!, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal),
            snapshot.ResourceType == "glamour_plates" && document.RootElement.TryGetProperty("plates", out var plates)
                && plates.ValueKind == JsonValueKind.Array && plates.GetArrayLength() == 0);
    }

    private static Dictionary<string, string> GetInventoryComponentHashes(JsonElement root) {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "items", "armoireItems", "armoireObserved", "retainerListings", "retainerListingsObserved" }) if (root.TryGetProperty(name, out var value)) result[name] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeJson(value))));
        return result;
    }

    private static string CanonicalizeJson(JsonElement element) => element.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => JsonSerializer.Serialize(property.Name) + ":" + CanonicalizeJson(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalizeJson).OrderBy(value => value, StringComparer.Ordinal)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()), JsonValueKind.Number => element.GetRawText(), JsonValueKind.True => "true", JsonValueKind.False => "false", JsonValueKind.Null => "null", _ => element.GetRawText(),
    };

    private bool IsDiagnosticRecording {
        get {
#if GILLIONS_TEST_BUILD
            return true;
#else
            return DateTime.UtcNow < diagnosticRecordingUntilUtc;
#endif
        }
    }

    private void RecordDiagnostic(string message) {
        if (!IsDiagnosticRecording) return;
        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        lock (diagnosticsLock) {
            diagnostics.Insert(0, line);
            if (diagnostics.Count > 40) diagnostics.RemoveRange(40, diagnostics.Count - 40);
        }
        log.Information("[Gillions diagnostics] {Message}", message);
    }

    private static string DescribePayload(JsonElement root, int bytes) {
        var parts = new List<string> { $"{bytes:N0} B" };
        foreach (var name in new[] { "items", "armoireItems", "retainerListings", "retainerBags", "ids", "completedQuestIds", "alliedSocieties", "tabs", "cards", "minions", "mounts", "bardings", "emotes", "orchestrions", "fashions", "blueMageSpells", "sightseeingLogIds", "aetherCurrentIds", "portraitBackgrounds", "portraitConditions", "portraitDecorations", "portraitFacials", "portraitFrames", "portraitPoses", "masterRecipeBookIds", "folkloreBookIds", "jobs", "craftingRecipeIds", "gatheringLogIds" }) {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array) parts.Add($"{name}={value.GetArrayLength()}");
        }
        if (root.TryGetProperty("eligibleCount", out var eligible) && eligible.TryGetInt32(out var eligibleCount)) parts.Add($"eligible={eligibleCount}");
        if (root.TryGetProperty("verifiedCount", out var verified) && verified.TryGetInt32(out var verifiedCount)) parts.Add($"verified={verifiedCount}");
        if (root.TryGetProperty("excludedByIdRangeCount", out var excluded) && excluded.TryGetInt32(out var excludedCount)) parts.Add($"excludedByIdRange={excludedCount}");
        if (root.TryGetProperty("ready", out var ready) && ready.ValueKind is JsonValueKind.True or JsonValueKind.False) parts.Add($"ready={ready.GetBoolean()}");
        if (root.TryGetProperty("armoireObserved", out var armoireObserved) && armoireObserved.ValueKind is JsonValueKind.True or JsonValueKind.False) parts.Add($"armoireObserved={armoireObserved.GetBoolean()}");
        return string.Join(", ", parts);
    }

    public void Dispose() {
        if (disposed) return;
#if GILLIONS_TEST_BUILD
        beastmasterLocal.Dispose();
        submarineLocal.Dispose();
        huntLocal.Dispose();
        dashboardLocal.Dispose();
        travelLocal.Dispose();
        huntMapCancellation.Cancel(); huntMapCancellation.Dispose();
        marketSource.Dispose(); marketContributor.Dispose(); marketHttp.Dispose();
#endif
        if (framework.IsInFrameworkUpdateThread) FlushConfigurationSave();
        disposed = true; requestLifetime.Dispose(); ClearTransientState();
        clientState.Login -= OnLogin; clientState.Logout -= OnLogout;
        chatGui.ChatMessage -= OnChatMessage;
        chatGui.LogMessage -= OnLogMessage;
        gameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
        framework.Update -= OnFrameworkUpdate;
#if GILLIONS_TEST_BUILD
        foreach (var cancellation in personalCancellation.Values) { cancellation.Cancel(); cancellation.Dispose(); }
        personalCancellation.Clear(); personalHttp.Dispose();
#endif
        pluginInterface.UiBuilder.Draw -= DrawSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        commands.RemoveHandler(CommandName);
        partyFinderContributor.Dispose();
        partyFinderHttp.Dispose();
        http.Dispose();
    }
}

internal sealed record PreparedSnapshot(
    string ResourceType,
    byte[] PayloadUtf8,
    string PayloadHash,
    Dictionary<string, string>? InventoryComponentHashes,
    string Description,
    string InventoryDelta,
    double PreparationMilliseconds,
    IReadOnlyDictionary<string, string> SentResultFingerprints,
    bool AllowEmptySnapshotReceipt);

internal sealed record CapturedSnapshotBatch(string CharacterName, string CharacterWorld, SyncRequestPermit Permit,
    GameSnapshot[] Snapshots, GilLedgerEvent[] GilEvents, string GilSessionId, Dictionary<string, string> PayloadHashes,
    Dictionary<string, string> InventoryHashes, bool RecordDiagnostics);

[Newtonsoft.Json.JsonConverter(typeof(LegacyPlanConfigurationConverter))]
public sealed class PluginConfiguration : IPluginConfiguration {
#if GILLIONS_TEST_BUILD
    public SubmarineVoyageRetention SubmarineVoyages { get; set; } = new();
    public HuntBillRetention HuntBills { get; set; } = new();
    public DashboardRetention DashboardFacts { get; set; } = new();
    public PersonalSyncState PersonalSync { get; set; } = new();
    public bool SyncPersonalHunts { get; set; }
    public bool SyncPersonalSubmarines { get; set; }
    public bool ShareHuntRoutingLocation { get; set; }
    public bool ContributeObservedMarketData { get; set; } = true;
    // Enrollment stop only, never market payload or reporter identity.
    public string GillionsMarketBlockedGeneration { get; set; } = "";
#endif
    public int Version { get; set; } = 1;
    public PairedSession? ActiveSession { get; set; }
    public bool PairingRequired { get; set; }
    public Dictionary<string, OwnedCharacterState> OwnedCharacters { get; set; } = new(StringComparer.Ordinal);
    public EvidenceCoverageGap CoverageGap { get; set; } = new();
    public string ServerUrl { get; set; } = GillionsEndpoints.DefaultServerUrl;
    public string PairingCode { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceToken { get; set; } = "";
    public bool AutomaticSync { get; set; } = true;
    public bool EnableItemLinkRequests { get; set; } = true;
#if GILLIONS_TEST_BUILD
    public bool EnablePartyFinderLinkRequests { get; set; }
    public bool AutomaticallyShowHuntMap { get; set; }
#endif
    public bool EnablePartyFinderContributions { get; set; } = false;
    public bool EnableGillionsPartyFinderContributions { get; set; } = false;
    // Non-secret enrollment metadata only; no listing, response, or credential.
    public string GillionsPartyFinderBlockedGeneration { get; set; } = "";
    public bool EnableAutoRetainerVenturePlans { get; set; } = false;
    // Legacy records are inert JSON so unknown or malformed members survive
    // Dalamud's normal config load/save without retaining executable plan types.
    [Newtonsoft.Json.JsonConverter(typeof(LegacyPlanDataConverter))]
    public Newtonsoft.Json.Linq.JToken? AutoRetainerVenturePlanBackups { get; set; } = new Newtonsoft.Json.Linq.JObject();
    [Newtonsoft.Json.JsonConverter(typeof(LegacyPlanDataConverter))]
    public Newtonsoft.Json.Linq.JToken? AutoRetainerPlanOwnershipStates { get; set; } = new Newtonsoft.Json.Linq.JObject();
    public Dictionary<string, string> LastPayloadHashes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> LastInventoryComponentHashes { get; set; } = new(StringComparer.Ordinal);
    public DateTime? LastSyncUtc { get; set; }
    public string SyncBlockedCode { get; set; } = "";
    public string SyncBlockedMessage { get; set; } = "";
    public string LastReadChangelogVersion { get; set; } = "";
    public string GilLedgerSessionId { get; set; } = Guid.NewGuid().ToString("N");
    public List<GilLedgerEvent> PendingGilLedgerEvents { get; set; } = [];
    public List<GilLedgerEvent> PendingRetainerGilReceipts { get; set; } = [];
    public List<GilLedgerEvent> PendingRetainerGilDeposits { get; set; } = [];
    public List<PendingRetainerSale> PendingRetainerSales { get; set; } = [];
    public Dictionary<string, long> RetainerGilBalances { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, RetainerVentureLocalState> RetainerVentureStates { get; set; } = new(StringComparer.Ordinal);
    // Retained only so upgrading from 1.0.25 never assigns an unscoped legacy
    // observation to the wrong character. Phase 3 reads only the partitioned map.
    public RetainerVentureLocalState RetainerVentureState { get; set; } = new();
    public bool UseCompiledDefaultServerUrl(string compiledDefault) {
        if (!PublicUrlConfiguration.TryUseCompiledDefault(ServerUrl, compiledDefault, out var serverUrl)) return false;
        ServerUrl = serverUrl;
        return true;
    }
    public void Save(IDalamudPluginInterface pluginInterface) => pluginInterface.SavePluginConfig(this);
}

public sealed record EnrollmentResponse(bool ok, string device_id, string token);
public sealed record GameSnapshot(string ResourceType, object Payload, string[]? AcknowledgedEventIds = null);
internal sealed record RetainerWithdrawalObservation(DateTime ObservedAtUtc, long Amount, string RetainerId, string RetainerName);
internal sealed record GilLedgerLogEvidence(DateTime ObservedAtUtc, uint LogMessageId, int[] IntegerParameters);
internal sealed class GilLedgerChatEvidence {
    public GilLedgerChatEvidence(DateTime observedAtUtc, int? itemId, int itemQuantity, long amount, string? retainerId, string? retainerName) { EvidenceId = Guid.NewGuid().ToString("N"); ObservedAtUtc = observedAtUtc; ItemId = itemId; ItemQuantity = itemQuantity; Amount = amount; RetainerId = retainerId; RetainerName = retainerName; }
    public string EvidenceId { get; }
    public DateTime ObservedAtUtc { get; }
    public int? ItemId { get; }
    public int ItemQuantity { get; }
    public long Amount { get; }
    public string? RetainerId { get; }
    public string? RetainerName { get; }
    public bool IsRetainerSale => !string.IsNullOrWhiteSpace(RetainerId);
}
internal sealed record GilLedgerClassification(string Kind, string Confidence, int? ItemId, int? ItemQuantity, string? RetainerId = null, string? RetainerName = null);

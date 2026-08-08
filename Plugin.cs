using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Configuration;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace GillionsGameSync;

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
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly PluginConfiguration configuration;
    private string settingsMessage = "";
    // A paired background sync must never interrupt login with its settings
    // window. Open it explicitly from Dalamud configuration when needed.
    private bool settingsVisible;
    private DateTime nextAutomaticSyncUtc = DateTime.MinValue;
    private DateTime nextRetainerListingCaptureUtc = DateTime.MinValue;
    private DateTime nextGilLedgerPollUtc = DateTime.MinValue;
    private DateTime nextGilLedgerUploadUtc = DateTime.MaxValue;
    private DateTime nextInventorySyncUtc = DateTime.MaxValue;
    private DateTime nextGilLedgerFlushUtc = DateTime.MinValue;
    private long? lastObservedGil;
    private string? lastObservedRetainerId;
    private long? lastObservedRetainerGil;
    private RetainerBalanceRead? pendingRetainerBalance;
    private DateTime pendingRetainerBalanceSinceUtc = DateTime.MinValue;
    private readonly List<RetainerWithdrawalObservation> recentRetainerWithdrawals = [];
    private bool gilLedgerDirty;
    private readonly List<GilLedgerLogEvidence> recentGilLedgerLogs = [];
    private readonly List<GilLedgerChatEvidence> recentGilLedgerChat = [];
    private readonly HashSet<string> emittedRetainerChatEvidence = new(StringComparer.Ordinal);
    private bool syncInFlight;
    private int automaticScopeIndex;
#if GILLIONS_TEST_BUILD
    private readonly object diagnosticsLock = new();
    private readonly List<string> diagnostics = [];
    private readonly Dictionary<string, string> lastObservedPayloadHashes = new(StringComparer.Ordinal);
    private Dictionary<string, int> lastInventoryRecords = new(StringComparer.Ordinal);
#endif
    private static readonly string PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "1.0.4";
    private static readonly string[] SyncScopes = ["inventory", "currencies", "achievements", "collectibles", "character", "quest_journal", "glamour_plates"];
    private static readonly string[] CurrentChangelog = [
        "Testing performance: Payload serialization and hashing now run off the game framework thread after native data has been safely copied.",
        "Testing performance: Each snapshot is serialized once, static game-data catalogs are cached, and successful sync state is saved once per batch.",
        "Performance: Idle native reads stop completely when automatic sync is off or the client is not paired.",
        "Performance: Retainer capture is idle outside the retainer interface and checks only every 5 seconds while a retainer is open.",
        "Performance: Automatic sync rotates one data category every 30 seconds; inventory changes and Gil Ledger entries remain prompt.",
        "Fix: Local plugin settings are saved only when ledger state changes, never once per game frame.",
        "Fix: Current Game Plates now remain available after you leave the glamour dresser. Gillions keeps your last valid snapshot until the game provides a newer one.",
        "Improvement: Glamour Plates, inventory, currencies, collections, character progress, and your session Gil Ledger all sync directly from the game client.",
        "Improvement: Sync remains compact and change-aware, uploading a category only when its local data changes.",
        "Testing: Quest Journal sync includes only verified one-time normal quest completion IDs; repeatable, tribal, levequest, and active journal state are excluded.",
        "Your Gillions Saved Plates remain permanent references until you choose to delete them.",
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

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IClientState clientState, IObjectTable objects, IFramework framework, IDataManager dataManager, IUnlockState unlockState, IGameInventory gameInventory, IChatGui chatGui, IPluginLog log) {
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
        commands.AddHandler("/gillionssync", new CommandInfo(OnCommand) { HelpMessage = "Pair or sync your selected Gillions data." });
        pluginInterface.UiBuilder.Draw += DrawSettings;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        framework.Update += OnFrameworkUpdate;
        gameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
        chatGui.LogMessage += OnLogMessage;
        chatGui.ChatMessage += OnChatMessage;
    }

    private void OnCommand(string command, string arguments) {
        _ = arguments.Trim().Equals("pair", StringComparison.OrdinalIgnoreCase) ? PairAsync() : SyncAsync();
    }

    private void OpenSettings() => settingsVisible = true;

    // Pairing code is entered locally by the user in the plugin configuration UI.
    // It is single-use; Gillions returns a revocable per-device credential.
    private async Task PairAsync() {
        if (string.IsNullOrWhiteSpace(configuration.PairingCode)) throw new InvalidOperationException("Enter a one-time pairing code in plugin settings first.");
        var machineId = GetMachineId();
        var body = new { name = Environment.MachineName, machineId, version = PluginVersion };
        using var request = Request("/api/game-sync/enroll", configuration.PairingCode, body);
        using var response = await http.SendAsync(request);
        await EnsureSuccessfulResponse(response);
        var enrolled = await JsonSerializer.DeserializeAsync<EnrollmentResponse>(await response.Content.ReadAsStreamAsync()) ?? throw new InvalidOperationException("Gillions did not return a device credential.");
        configuration.DeviceToken = enrolled.token;
        configuration.DeviceId = enrolled.device_id;
        configuration.PairingCode = "";
        configuration.SyncBlockedCode = "";
        configuration.SyncBlockedMessage = "";
        await SaveConfigurationAsync();
        settingsMessage = "Paired successfully. You can now sync your Gillions data.";
        log.Information("Gillions Game Sync paired successfully.");
    }

    private void OnFrameworkUpdate(IFramework frameworkInstance) {
        var now = DateTime.UtcNow;
        if (!clientState.IsLoggedIn) {
            lastObservedGil = null;
            lastObservedRetainerId = null;
            lastObservedRetainerGil = null;
            gilLedgerDirty = false;
            nextInventorySyncUtc = DateTime.MaxValue;
            return;
        }
        // An unpaired client, or one whose owner has disabled automatic sync,
        // must have no recurring native-memory work. Manual Sync remains
        // available and performs its one explicit collection on demand.
        var backgroundSyncEnabled = configuration.AutomaticSync && !string.IsNullOrWhiteSpace(configuration.DeviceToken);
        if (!backgroundSyncEnabled) {
            // Resume from a fresh baseline if the owner turns automatic sync
            // back on; do not turn time spent opted out into one large ledger
            // transaction.
            lastObservedGil = null;
            lastObservedRetainerId = null;
            lastObservedRetainerGil = null;
            pendingRetainerBalance = null;
            pendingRetainerBalanceSinceUtc = DateTime.MinValue;
            gilLedgerDirty = false;
            nextInventorySyncUtc = DateTime.MaxValue;
            return;
        }
        if (now >= nextGilLedgerPollUtc || (gilLedgerDirty && now >= nextGilLedgerFlushUtc)) {
            nextGilLedgerPollUtc = now.AddMilliseconds(GilLedgerPollIntervalMilliseconds);
            CaptureGilLedgerChange();
        }
        if (now >= nextRetainerListingCaptureUtc) {
            nextRetainerListingCaptureUtc = now.AddSeconds(RetainerCaptureIntervalSeconds);
            if (DirectGameSnapshotCollector.CaptureLoadedRetainerListings()
                && backgroundSyncEnabled)
                nextInventorySyncUtc = now.AddMilliseconds(250);
        }
        if (syncInFlight) return;
        if (configuration.PendingGilLedgerEvents is { Count: > 0 } && nextGilLedgerUploadUtc == DateTime.MaxValue)
            nextGilLedgerUploadUtc = now;
        if (now >= nextGilLedgerUploadUtc) {
            nextGilLedgerUploadUtc = DateTime.MaxValue;
            _ = SyncAutomaticallyAsync([]);
            return;
        }
        if (now >= nextInventorySyncUtc) {
            nextInventorySyncUtc = DateTime.MaxValue;
            nextAutomaticSyncUtc = now.AddSeconds(AutomaticSyncIntervalSeconds);
            _ = SyncAutomaticallyAsync(["inventory"]);
            return;
        }
        if (now < nextAutomaticSyncUtc) return;
        nextAutomaticSyncUtc = now.AddSeconds(AutomaticSyncIntervalSeconds);
        _ = SyncAutomaticallyAsync([NextAutomaticScope()]);
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> _) {
        // The event itself is a copied native state notification. We take the
        // balance once the game has finished its short update burst so a single
        // action becomes one ledger row rather than slot-level noise.
        gilLedgerDirty = true;
        nextGilLedgerFlushUtc = DateTime.UtcNow.AddMilliseconds(750);
        if (configuration.AutomaticSync && !string.IsNullOrWhiteSpace(configuration.DeviceToken))
            nextInventorySyncUtc = DateTime.UtcNow.AddMilliseconds(InventoryChangeDebounceMilliseconds);
    }

    private void OnLogMessage(ILogMessage message) {
        // Never retain raw message text, entity names, or string parameters.
        // IDs and integer arguments are sufficient to map known transaction
        // contracts during beta validation without collecting chat content.
        var values = new List<int>();
        for (var index = 0; index < Math.Min(message.ParameterCount, 8); index++) {
            if (message.TryGetIntParameter(index, out var value)) values.Add(value);
        }
        recentGilLedgerLogs.Add(new GilLedgerLogEvidence(DateTime.UtcNow, message.LogMessageId, values.ToArray()));
        recentGilLedgerLogs.RemoveAll(entry => entry.ObservedAtUtc < DateTime.UtcNow.AddSeconds(-5));
    }

    private void OnChatMessage(IHandleableChatMessage message) {
        // Some itemized vendor messages do not arrive with usable LogMessage
        // integer parameters. Keep only the structured sale facts needed to
        // correlate the message with the native gil balance; never retain raw
        // chat text or upload it.
        var match = VendorSaleChatRegex.Match(message.Message.TextValue);
        if (!match.Success) return;
        var quantityToken = match.Groups["quantity"].Value;
        var quantity = quantityToken.Equals("a", StringComparison.OrdinalIgnoreCase) || quantityToken.Equals("an", StringComparison.OrdinalIgnoreCase)
            ? 1
            : int.TryParse(quantityToken.Replace(",", ""), out var parsedQuantity) ? parsedQuantity : 0;
        if (quantity <= 0) return;
        if (!long.TryParse(match.Groups["amount"].Value.Replace(",", ""), out var amount) || amount <= 0) return;
        var itemId = message.Message.Payloads.OfType<ItemPayload>().FirstOrDefault()?.ItemId;
        var retainer = itemId > 0 ? NativeInventoryCollector.FindLoadedRetainerItem(itemId.Value) : null;
        recentGilLedgerChat.Add(new GilLedgerChatEvidence(DateTime.UtcNow, itemId > 0 ? (int)itemId : null, quantity, amount, retainer?.RetainerId, retainer?.RetainerName));
        recentGilLedgerChat.RemoveAll(entry => entry.ObservedAtUtc < DateTime.UtcNow.AddSeconds(-5));
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

        if (retainerBalance is not null && lastObservedRetainerId != retainerBalance.RetainerId) {
            configuration.RetainerGilBalances ??= new Dictionary<string, long>(StringComparer.Ordinal);
            if (configuration.RetainerGilBalances.TryGetValue(retainerBalance.RetainerId, out var priorGil) && priorGil != retainerBalance.Gil)
                RecordRetainerBalanceChange(retainerBalance, retainerBalance.Gil - priorGil);
            else {
                configuration.RetainerGilBalances[retainerBalance.RetainerId] = retainerBalance.Gil;
                configuration.Save(pluginInterface);
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
                configuration.PendingGilLedgerEvents ??= [];
                foreach (var sale in confirmedRetainerSales) configuration.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(sale.Amount, "retainer_sale", "confirmed", sale.ItemId, sale.ItemQuantity, sale.RetainerId, sale.RetainerName, null, []));
                configuration.Save(pluginInterface);
                QueueGilLedgerUpload();
#if GILLIONS_TEST_BUILD
                RecordDiagnostic($"Retainer gil ledger: +{retainerDelta:#,##0} gil; confirmed/retainer_sale; retainer={retainerBalance.RetainerName}; town={retainerBalance.Town}; items={confirmedRetainerSales.Count}.");
#endif
            }
        }
        else if (retainerBalance is not null && lastObservedRetainerGil is not null && retainerBalance.Gil < lastObservedRetainerGil.Value) {
            var retainerDelta = retainerBalance.Gil - lastObservedRetainerGil.Value;
            lastObservedRetainerGil = retainerBalance.Gil;
            RecordRetainerBalanceChange(retainerBalance, retainerDelta);
        }
        if (FlushExpiredRetainerGilReceipts() || FlushExpiredRetainerGilDeposits()) {
            configuration.Save(pluginInterface);
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
        if (FlushRetainerChatEvidence()) configuration.Save(pluginInterface);
        if (!gilLedgerDirty || DateTime.UtcNow < nextGilLedgerFlushUtc) return;
        gilLedgerDirty = false;
        var delta = currentGil.Value - lastObservedGil.Value;
        lastObservedGil = currentGil;
        if (delta == 0) return;
        var recent = recentGilLedgerLogs.LastOrDefault(entry => entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-3));
        var recentChatSale = recentGilLedgerChat.LastOrDefault(entry => entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-3));
        if (delta < 0 && recent?.LogMessageId == 737) {
            QueueRetainerGilDeposit(CreateGilLedgerEvent(delta, "unclassified", "inferred", null, null, null, null, recent.LogMessageId, recent.IntegerParameters));
            configuration.Save(pluginInterface);
#if GILLIONS_TEST_BUILD
            RecordDiagnostic($"Pending retainer Gil deposit: {delta:#,##0} gil; awaiting a matching retainer balance increase.");
#endif
            return;
        }
        configuration.PendingGilLedgerEvents ??= [];
        if (delta > 0 && recent?.LogMessageId == 736) {
            var confirmedRetainerSales = ConfirmPendingRetainerSales(delta, null);
            if (confirmedRetainerSales.Count > 0) {
                foreach (var sale in confirmedRetainerSales) configuration.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(
                    sale.Amount, "retainer_sale", "confirmed", sale.ItemId, sale.ItemQuantity, sale.RetainerId, sale.RetainerName, recent.LogMessageId, recent.IntegerParameters));
                configuration.Save(pluginInterface);
                QueueGilLedgerUpload();
#if GILLIONS_TEST_BUILD
                RecordDiagnostic($"Gil ledger: +{delta:#,##0} gil; confirmed/retainer_sale; items={confirmedRetainerSales.Count}; retainer={confirmedRetainerSales[0].RetainerName ?? "unknown"}.");
#endif
                return;
            }
            QueueRetainerGilReceipt(CreateGilLedgerEvent(delta, "retainer_gil_receipt", "inferred", null, null, null, null, recent.LogMessageId, recent.IntegerParameters));
            configuration.Save(pluginInterface);
#if GILLIONS_TEST_BUILD
            RecordDiagnostic($"Pending retainer Gil receipt: +{delta:#,##0} gil; awaiting a matching retainer balance withdrawal.");
#endif
            return;
        }
        var classification = ClassifyGilLedgerEvent(delta, recent, recentChatSale);
        configuration.PendingGilLedgerEvents.Add(CreateGilLedgerEvent(
            delta, classification.Kind, classification.Confidence, classification.ItemId, classification.ItemQuantity,
            classification.RetainerId, classification.RetainerName, recent?.LogMessageId, recent?.IntegerParameters ?? []));
        if (recentChatSale?.IsRetainerSale == true) emittedRetainerChatEvidence.Add(recentChatSale.EvidenceId);
        if (configuration.PendingGilLedgerEvents.Count > 200) configuration.PendingGilLedgerEvents.RemoveRange(0, configuration.PendingGilLedgerEvents.Count - 200);
        configuration.Save(pluginInterface);
        QueueGilLedgerUpload();
#if GILLIONS_TEST_BUILD
        RecordDiagnostic($"Gil ledger: {delta:+#,##0;-#,##0} gil; {classification.Confidence}/{classification.Kind}{(classification.ItemId is null ? "" : $"; item={classification.ItemId} x{classification.ItemQuantity}")}{(recent is null ? "" : $"; log {recent.LogMessageId}, ints=[{string.Join(",", recent.IntegerParameters)}]")}{(recentChatSale is null ? "" : "; chat=vendor_sale")}.");
#endif
    }

    private bool RecordRetainerBalanceChange(RetainerBalanceRead retainer, long delta) {
        if (delta == 0) return false;
        configuration.RetainerGilBalances ??= new Dictionary<string, long>(StringComparer.Ordinal);
        configuration.RetainerGilBalances[retainer.RetainerId] = retainer.Gil;
        var confirmedDeposit = false;
        if (delta > 0) confirmedDeposit = ConfirmPendingRetainerGilDeposits(retainer, delta);
        if (delta < 0) {
            recentRetainerWithdrawals.Add(new RetainerWithdrawalObservation(DateTime.UtcNow, -delta, retainer.RetainerId, retainer.RetainerName));
            recentRetainerWithdrawals.RemoveAll(entry => entry.ObservedAtUtc < DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
            ConfirmPendingRetainerGilReceipts(retainer, -delta);
        }
        configuration.Save(pluginInterface);
#if GILLIONS_TEST_BUILD
        RecordDiagnostic($"Retainer balance: {delta:+#,##0;-#,##0} gil; retainer={retainer.RetainerName}; current={retainer.Gil:#,##0}; retained locally for receipt correlation.");
#endif
        return confirmedDeposit;
    }

    private void QueueRetainerGilReceipt(GilLedgerEvent receipt) {
        configuration.PendingRetainerGilReceipts ??= [];
        var withdrawal = recentRetainerWithdrawals.LastOrDefault(entry => entry.Amount == receipt.GilDelta && entry.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (withdrawal is not null) {
            QueueGilLedgerEvent(receipt with { RetainerId = withdrawal.RetainerId, RetainerName = withdrawal.RetainerName });
            QueueGilLedgerUpload();
            return;
        }
        configuration.PendingRetainerGilReceipts.Add(receipt);
        if (configuration.PendingRetainerGilReceipts.Count > 20) configuration.PendingRetainerGilReceipts.RemoveRange(0, configuration.PendingRetainerGilReceipts.Count - 20);
    }

    private void ConfirmPendingRetainerGilReceipts(RetainerBalanceRead retainer, long amount) {
        if (configuration.PendingRetainerGilReceipts is not { Count: > 0 }) return;
        var receipt = configuration.PendingRetainerGilReceipts.LastOrDefault(entry => entry.GilDelta == amount && entry.OccurredAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (receipt is null) return;
        configuration.PendingRetainerGilReceipts.Remove(receipt);
        QueueGilLedgerEvent(receipt with { RetainerId = retainer.RetainerId, RetainerName = retainer.RetainerName });
        QueueGilLedgerUpload();
#if GILLIONS_TEST_BUILD
        RecordDiagnostic($"Gil ledger: +{amount:#,##0} gil; inferred/retainer_gil_receipt; retainer={retainer.RetainerName}.");
#endif
    }

    private bool FlushExpiredRetainerGilReceipts() {
        if (configuration.PendingRetainerGilReceipts is not { Count: > 0 }) return false;
        var cutoff = DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds);
        var expired = configuration.PendingRetainerGilReceipts.Where(entry => entry.OccurredAtUtc <= cutoff).ToArray();
        if (expired.Length == 0) return false;
        configuration.PendingRetainerGilReceipts.RemoveAll(entry => entry.OccurredAtUtc <= cutoff);
        foreach (var receipt in expired) QueueGilLedgerEvent(receipt);
        return true;
    }

    private void QueueRetainerGilDeposit(GilLedgerEvent deposit) {
        configuration.PendingRetainerGilDeposits ??= [];
        configuration.PendingRetainerGilDeposits.Add(deposit);
        if (configuration.PendingRetainerGilDeposits.Count > 20) configuration.PendingRetainerGilDeposits.RemoveRange(0, configuration.PendingRetainerGilDeposits.Count - 20);
    }

    private bool ConfirmPendingRetainerGilDeposits(RetainerBalanceRead retainer, long amount) {
        if (configuration.PendingRetainerGilDeposits is not { Count: > 0 }) return false;
        var deposit = configuration.PendingRetainerGilDeposits.LastOrDefault(entry => -entry.GilDelta == amount && entry.OccurredAtUtc >= DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds));
        if (deposit is null) return false;
        configuration.PendingRetainerGilDeposits.Remove(deposit);
        QueueGilLedgerEvent(deposit with { Kind = "retainer_gil_deposit", RetainerId = retainer.RetainerId, RetainerName = retainer.RetainerName });
        QueueGilLedgerUpload();
#if GILLIONS_TEST_BUILD
        RecordDiagnostic($"Gil ledger: -{amount:#,##0} gil; inferred/retainer_gil_deposit; retainer={retainer.RetainerName}.");
#endif
        return true;
    }

    private bool FlushExpiredRetainerGilDeposits() {
        if (configuration.PendingRetainerGilDeposits is not { Count: > 0 }) return false;
        var cutoff = DateTime.UtcNow.AddSeconds(-RetainerReceiptCorrelationSeconds);
        var expired = configuration.PendingRetainerGilDeposits.Where(entry => entry.OccurredAtUtc <= cutoff).ToArray();
        if (expired.Length == 0) return false;
        configuration.PendingRetainerGilDeposits.RemoveAll(entry => entry.OccurredAtUtc <= cutoff);
        foreach (var deposit in expired) QueueGilLedgerEvent(deposit);
        return true;
    }

    private void QueueGilLedgerEvent(GilLedgerEvent entry) {
        configuration.PendingGilLedgerEvents ??= [];
        configuration.PendingGilLedgerEvents.Add(entry);
        if (configuration.PendingGilLedgerEvents.Count > 200) configuration.PendingGilLedgerEvents.RemoveRange(0, configuration.PendingGilLedgerEvents.Count - 200);
    }

    private static GilLedgerClassification ClassifyGilLedgerEvent(long gilDelta, GilLedgerLogEvidence? evidence, GilLedgerChatEvidence? chatSale) {
        // These contracts were verified through the beta build with a merchant
        // purchase and sale. Keep the amount comparison so an unrelated log
        // sharing the same ID cannot be promoted solely by its timing.
        if (evidence?.IntegerParameters is { Length: >= 1 } values) {
            if (evidence.LogMessageId == 1605 && gilDelta == values[0])
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
            if ((evidence.LogMessageId == 533 || evidence.LogMessageId == 732 || evidence.LogMessageId == 733) && gilDelta < 0 && values.All(value => value == 0))
                return new GilLedgerClassification("teleport", "confirmed", null, null);
            if (evidence.LogMessageId == 1385 && gilDelta < 0)
                return new GilLedgerClassification("repair", "confirmed", null, null);
            if (values.Length < 2) return new GilLedgerClassification("unclassified", "inferred", null, null);
            var itemId = values[0] > 0 ? NormalizeLedgerItemId(values[0]) : (int?)null;
            var quantity = values[1] > 0 ? values[1] : (int?)null;
            if (evidence.LogMessageId == 734 && itemId is not null && quantity is not null && gilDelta < 0)
                return new GilLedgerClassification("market_purchase", "confirmed", itemId, quantity);
            if (values.Length >= 3) {
                var amount = values[2];
                if (evidence.LogMessageId == 1687 && itemId is not null && quantity is not null && gilDelta == -amount)
                    return new GilLedgerClassification("vendor_purchase", "confirmed", itemId, quantity);
                if (evidence.LogMessageId == 1688 && itemId is not null && quantity is not null && gilDelta == amount)
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
        foreach (var evidence in recentGilLedgerChat.Where(entry => entry.IsRetainerSale && entry.ObservedAtUtc <= cutoff && !emittedRetainerChatEvidence.Contains(entry.EvidenceId)).ToArray()) {
            configuration.PendingRetainerSales ??= [];
            if (!configuration.PendingRetainerSales.Any(sale => sale.SaleId == evidence.EvidenceId)) {
                configuration.PendingRetainerSales.Add(new PendingRetainerSale(
                    evidence.EvidenceId, evidence.ObservedAtUtc, evidence.Amount, evidence.ItemId, evidence.ItemQuantity, evidence.RetainerId, evidence.RetainerName));
                changed = true;
            }
            emittedRetainerChatEvidence.Add(evidence.EvidenceId);
#if GILLIONS_TEST_BUILD
            RecordDiagnostic($"Pending retainer sale: +{evidence.Amount:#,##0} gil{(evidence.ItemId is null ? "" : $"; item={evidence.ItemId} x{evidence.ItemQuantity}")}{(evidence.RetainerName is null ? "" : $"; retainer={evidence.RetainerName}")}.");
#endif
        }
        if (configuration.PendingGilLedgerEvents?.Count > 200) {
            configuration.PendingGilLedgerEvents.RemoveRange(0, configuration.PendingGilLedgerEvents.Count - 200);
            changed = true;
        }
        return changed;
    }

    private List<PendingRetainerSale> ConfirmPendingRetainerSales(long withdrawalAmount, string? retainerId) {
        var pending = configuration.PendingRetainerSales ?? [];
        if (!string.IsNullOrWhiteSpace(retainerId)) pending = pending.Where(sale => StringComparer.Ordinal.Equals(sale.RetainerId, retainerId)).ToList();
        var exact = pending.Where(sale => sale.Amount == withdrawalAmount).Take(1).ToList();
        if (exact.Count == 0 && pending.Sum(sale => sale.Amount) == withdrawalAmount) exact = pending.ToList();
        if (exact.Count > 0) configuration.PendingRetainerSales = pending.Except(exact).ToList();
        return exact;
    }

    private static readonly Regex VendorSaleChatRegex = new(
        @"^You sell (?<quantity>[0-9,]+|a|an) .+? for (?<amount>[0-9,]+) gil\.?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Log-message item parameters encode high-quality items by adding one
    // million. Ledger links must use the base catalog ID; quality is not a
    // separate item page.
    private static int NormalizeLedgerItemId(int itemId) => itemId is >= 1_000_000 and < 2_000_000 ? itemId - 1_000_000 : itemId;

    private string NextAutomaticScope() {
        var scope = SyncScopes[automaticScopeIndex];
        automaticScopeIndex = (automaticScopeIndex + 1) % SyncScopes.Length;
        return scope;
    }

    private void QueueGilLedgerUpload() {
        if (configuration.AutomaticSync && !string.IsNullOrWhiteSpace(configuration.DeviceToken))
            nextGilLedgerUploadUtc = DateTime.UtcNow.AddMilliseconds(250);
    }

    private async Task SyncAutomaticallyAsync(IEnumerable<string> scopes) {
        try { await SyncAsync(force: false, background: true, scopes: scopes); }
        catch (GillionsSyncRejectedException error) when (IsAccountAccessBlocked(error.Code)) {
            log.Warning("Gillions automatic sync stopped: {Code}.", error.Code);
        }
        catch (Exception error) {
            if (configuration.PendingGilLedgerEvents is { Count: > 0 })
                nextGilLedgerUploadUtc = DateTime.UtcNow.AddSeconds(AutomaticFailureRetrySeconds);
            log.Warning(error, "Gillions automatic sync failed; it will retry after a short backoff.");
        }
    }

    private async Task SyncAsync(bool force = true, bool background = false, IEnumerable<string>? scopes = null) {
        if (string.IsNullOrWhiteSpace(configuration.DeviceToken)) throw new InvalidOperationException("Pair this plugin with Gillions before syncing.");
        if (syncInFlight) {
            if (!background) throw new InvalidOperationException("A Gillions sync is already running.");
            return;
        }
        syncInFlight = true;
        try {
            configuration.LastPayloadHashes ??= new Dictionary<string, string>(StringComparer.Ordinal);
            var submitted = 0;
            var configurationChanged = false;
#if GILLIONS_TEST_BUILD
            var collectionStopwatch = Stopwatch.StartNew();
#endif
            // Every Dalamud service and native pointer access is confined to the
            // framework thread, including identity for ledger-only uploads.
            var selectedScopes = (scopes ?? SyncScopes).ToArray();
            var captured = await framework.RunOnFrameworkThread(() => {
                if (!clientState.IsLoggedIn) throw new InvalidOperationException("Log into a character before syncing.");
                var currentName = objects.LocalPlayer?.Name.TextValue ?? "";
                var currentWorld = objects.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "";
                var snapshots = DirectGameSnapshotCollector.Collect(pluginInterface, clientState, objects, dataManager, unlockState, configuration.RetainerGilBalances, selectedScopes).ToArray();
                return new CapturedSnapshotBatch(currentName, currentWorld, snapshots);
            });
            var snapshots = captured.Snapshots;
#if GILLIONS_TEST_BUILD
            collectionStopwatch.Stop();
            var collectedLabel = snapshots.Length > 0 ? string.Join(", ", snapshots.Select(snapshot => snapshot.ResourceType)) : "queued ledger data";
            RecordDiagnostic($"Collected {collectedLabel} in {collectionStopwatch.Elapsed.TotalMilliseconds:N0} ms.");
#endif
            // All Dalamud and native-memory reads above remain on the framework
            // thread. The resulting managed snapshots are immutable, so JSON
            // preparation and hashing can run without blocking game frames.
            var preparedSnapshots = await Task.Run(() => snapshots.Select(PrepareSnapshot).ToArray());
#if GILLIONS_TEST_BUILD
            if (preparedSnapshots.Length > 0)
                RecordDiagnostic($"Prepared {string.Join(", ", preparedSnapshots.Select(snapshot => snapshot.ResourceType))} off-thread in {preparedSnapshots.Sum(snapshot => snapshot.PreparationMilliseconds):N0} ms.");
#endif
            foreach (var snapshot in preparedSnapshots) {
                var payloadHash = snapshot.PayloadHash;
#if GILLIONS_TEST_BUILD
                if (!lastObservedPayloadHashes.TryGetValue(snapshot.ResourceType, out var observedHash) || observedHash != payloadHash) {
                    RecordDiagnostic($"Collected {snapshot.ResourceType}: {snapshot.Description}; hash {payloadHash[..12]}{snapshot.InventoryDelta}");
                    lastObservedPayloadHashes[snapshot.ResourceType] = payloadHash;
                }
#endif
                if (!force && configuration.LastPayloadHashes.TryGetValue(snapshot.ResourceType, out var previousHash) && previousHash == payloadHash) continue;
                var inventoryComponents = snapshot.InventoryComponentHashes;
                if (inventoryComponents is not null && !force) {
                    var changed = inventoryComponents.Where(entry => !configuration.LastInventoryComponentHashes.TryGetValue(entry.Key, out var previous) || previous != entry.Value).Select(entry => entry.Key).ToArray();
                    log.Information("Gillions inventory fingerprint changed: {Components}.", changed.Length > 0 ? string.Join(", ", changed) : "character identity or inventory metadata");
                }
                var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+','-').Replace('/','_').TrimEnd('=');
                using var request = SnapshotRequest("/api/game-sync/sync", configuration.DeviceToken, snapshot.ResourceType, nonce, snapshot.PayloadUtf8);
#if GILLIONS_TEST_BUILD
                var uploadStopwatch = Stopwatch.StartNew();
#endif
                using var response = await http.SendAsync(request);
                await EnsureSuccessfulResponse(response);
#if GILLIONS_TEST_BUILD
                uploadStopwatch.Stop();
                RecordDiagnostic($"Uploaded {snapshot.ResourceType}: HTTP {(int)response.StatusCode}; hash {payloadHash[..12]}; network {uploadStopwatch.Elapsed.TotalMilliseconds:N0} ms.");
#endif
                configuration.LastPayloadHashes[snapshot.ResourceType] = payloadHash;
                if (inventoryComponents is not null) configuration.LastInventoryComponentHashes = inventoryComponents;
                configuration.LastSyncUtc = DateTime.UtcNow;
                configurationChanged = true;
                submitted++;
            }
            var pendingGilLedgerEvents = configuration.PendingGilLedgerEvents ?? [];
            if (pendingGilLedgerEvents.Count > 0) {
                var currentName = captured.CharacterName;
                var currentWorld = captured.CharacterWorld;
                foreach (var characterEvents in pendingGilLedgerEvents.GroupBy(entry => new { Name = string.IsNullOrWhiteSpace(entry.CharacterName) ? currentName : entry.CharacterName, World = string.IsNullOrWhiteSpace(entry.CharacterWorld) ? currentWorld : entry.CharacterWorld }).ToArray()) {
                    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+','-').Replace('/','_').TrimEnd('=');
                    var payload = new {
                        character = new { name = characterEvents.Key.Name, world = characterEvents.Key.World },
                        sessionId = configuration.GilLedgerSessionId,
                        events = characterEvents.Select(entry => new { eventId = entry.EventId, occurredAt = entry.OccurredAtUtc, gilDelta = entry.GilDelta, kind = entry.Kind, confidence = entry.Confidence, itemId = entry.ItemId, itemQuantity = entry.ItemQuantity, retainerId = entry.RetainerId, retainerName = entry.RetainerName, logMessageId = entry.LogMessageId, logIntegerParameters = entry.LogIntegerParameters }).ToArray(),
                    };
                    using var request = Request("/api/game-sync/sync", configuration.DeviceToken, new { resourceType = "gil_ledger", nonce, payload });
                    using var response = await http.SendAsync(request);
                    await EnsureSuccessfulResponse(response);
                    var uploadedEventIds = characterEvents.Select(entry => entry.EventId).ToHashSet(StringComparer.Ordinal);
                    configuration.PendingGilLedgerEvents?.RemoveAll(entry => uploadedEventIds.Contains(entry.EventId));
                    configurationChanged = true;
                    submitted++;
#if GILLIONS_TEST_BUILD
                    RecordDiagnostic($"Uploaded gil ledger: HTTP {(int)response.StatusCode}; character={characterEvents.Key.Name}; events={uploadedEventIds.Count}.");
#endif
                }
            }
            if (submitted > 0) {
                if (!string.IsNullOrWhiteSpace(configuration.SyncBlockedCode)) {
                    configuration.SyncBlockedCode = "";
                    configuration.SyncBlockedMessage = "";
                    configurationChanged = true;
                }
#if GILLIONS_TEST_BUILD
                var saveStopwatch = Stopwatch.StartNew();
#endif
                if (configurationChanged) await SaveConfigurationAsync();
#if GILLIONS_TEST_BUILD
                saveStopwatch.Stop();
                RecordDiagnostic($"Saved changed local sync state once in {saveStopwatch.Elapsed.TotalMilliseconds:N0} ms.");
#endif
                if (!background) settingsMessage = "Sync completed successfully.";
                log.Information("Gillions Game Sync submitted {Count} changed data category(s) for {Character}.", submitted, string.IsNullOrWhiteSpace(captured.CharacterName) ? "current character" : captured.CharacterName);
            } else if (!background) settingsMessage = "No changed data was found; Gillions is already current.";
        } catch (GillionsSyncRejectedException error) when (IsAccountAccessBlocked(error.Code)) {
            await MarkSyncBlockedAsync(error);
#if GILLIONS_TEST_BUILD
            RecordDiagnostic($"Sync blocked: {error.Code} — {error.Message}");
#endif
            throw;
        } catch (Exception error) {
            log.Debug(error, "Gillions sync failed.");
#if GILLIONS_TEST_BUILD
            RecordDiagnostic($"Sync failed: {error.GetType().Name} — {error.Message}");
#endif
            throw;
        } finally { syncInFlight = false; }
    }

    private void DrawSettings() {
        if (!settingsVisible) return;
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(510, 0), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Gillions Game Sync", ref settingsVisible)) { ImGui.End(); return; }
        ImGui.TextWrapped("Pair this game client with the active character selected in Gillions. Sync reads supported data already available to the game client and keeps your account current.");
        ImGui.Separator();
        var hasUnreadChangelog = !string.Equals(configuration.LastReadChangelogVersion, PluginVersion, StringComparison.Ordinal);
        ImGui.SetNextItemOpen(hasUnreadChangelog, ImGuiCond.Once);
        if (ImGui.CollapsingHeader($"What’s New — {PluginVersion}")) {
            if (hasUnreadChangelog) ImGui.TextColored(new System.Numerics.Vector4(.55f, .78f, 1f, 1f), "New since your last installed version.");
            foreach (var entry in CurrentChangelog) ImGui.BulletText(entry);
            if (hasUnreadChangelog && ImGui.Button("Mark changelog as read")) {
                configuration.LastReadChangelogVersion = PluginVersion;
                configuration.Save(pluginInterface);
            }
        }
        ImGui.Separator();
        var serverUrl = configuration.ServerUrl;
        if (ImGui.InputText("Gillions URL", ref serverUrl, 256)) configuration.ServerUrl = serverUrl.TrimEnd('/');
        var pairingCode = configuration.PairingCode;
        if (ImGui.InputText("One-time pairing code", ref pairingCode, 256)) configuration.PairingCode = pairingCode.Trim();
        if (ImGui.Button("Pair this device")) _ = RunFromSettings(PairAsync);
        ImGui.SameLine();
        if (ImGui.Button("Sync now")) _ = RunFromSettings(() => SyncAsync());
        ImGui.TextWrapped(NativeInventoryCollector.GetAvailabilityStatus());
        ImGui.Separator();
        var automaticSync = configuration.AutomaticSync;
        if (ImGui.Checkbox($"Automatically sync changed data every {AutomaticSyncIntervalSeconds} seconds", ref automaticSync)) {
            configuration.AutomaticSync = automaticSync;
            configuration.Save(pluginInterface);
        }
        ImGui.TextDisabled($"Automatic sync checks one data category every {AutomaticSyncIntervalSeconds} seconds. Inventory changes are debounced and synced promptly.");
        if (!string.IsNullOrWhiteSpace(configuration.SyncBlockedMessage)) {
            ImGui.TextColored(new System.Numerics.Vector4(1f, .5f, .5f, 1f), configuration.SyncBlockedMessage);
        }
        ImGui.TextDisabled("Gillions syncs all supported data: inventory, currencies, achievements, collectibles, character progress, and beta session ledger entries.");
        ImGui.TextDisabled("Achievements sync after the in-game Achievement list has loaded.");
#if GILLIONS_TEST_BUILD
        DrawDiagnostics();
#endif
        if (!string.IsNullOrWhiteSpace(settingsMessage)) {
            ImGui.Separator();
            ImGui.TextWrapped(settingsMessage);
        }
        ImGui.TextDisabled(configuration.LastSyncUtc is null ? "Not synced yet." : $"Last successful sync: {configuration.LastSyncUtc:O}");
        ImGui.End();
    }

    private async Task RunFromSettings(Func<Task> action) {
        try { await action(); }
        catch (Exception error) { settingsMessage = error.Message; log.Error(error, "Gillions Game Sync operation failed."); }
    }

    // HttpClient continuations do not resume on Dalamud's framework thread.
    // Saving plugin config is a Dalamud operation, so marshal it back before
    // touching the plugin interface after a pairing or sync response.
    private Task SaveConfigurationAsync() => framework.RunOnFrameworkThread(() => configuration.Save(pluginInterface));

    private HttpRequestMessage Request(string path, string token, object body) {
        var request = new HttpRequestMessage(HttpMethod.Post, configuration.ServerUrl.TrimEnd('/') + path) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd($"GillionsGameSync/{PluginVersion}");
        return request;
    }

    private HttpRequestMessage SnapshotRequest(string path, string token, string resourceType, string nonce, byte[] payloadUtf8) {
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
        var request = new HttpRequestMessage(HttpMethod.Post, configuration.ServerUrl.TrimEnd('/') + path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd($"GillionsGameSync/{PluginVersion}");
        return request;
    }

    private async Task MarkSyncBlockedAsync(GillionsSyncRejectedException error) {
        configuration.AutomaticSync = false;
        configuration.SyncBlockedCode = error.Code;
        configuration.SyncBlockedMessage = error.Message;
        settingsMessage = error.Message;
        await SaveConfigurationAsync();
    }

    private static bool IsAccountAccessBlocked(string code) => code is "TRIAL_EXPIRED" or "ACCOUNT_DISABLED";

    private static async Task EnsureSuccessfulResponse(HttpResponseMessage response) {
        if (response.IsSuccessStatusCode) return;
        var detail = (await response.Content.ReadAsStringAsync()).Trim();
        var code = ""; var message = "";
        try { using var document = JsonDocument.Parse(detail); if (document.RootElement.TryGetProperty("code", out var codeProperty)) code = codeProperty.GetString() ?? ""; if (document.RootElement.TryGetProperty("error", out var errorProperty)) message = errorProperty.GetString() ?? ""; } catch (JsonException) { }
        if (!string.IsNullOrWhiteSpace(code)) throw new GillionsSyncRejectedException(code, string.IsNullOrWhiteSpace(message) ? $"Gillions Sync was rejected ({code})." : message);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"Gillions returned {(int)response.StatusCode} ({response.ReasonPhrase})." : $"Gillions returned {(int)response.StatusCode}: {detail}");
    }

    private static string GetMachineId() {
        // The test assembly must not replace the stable device credential for
        // the same Windows user. Stable builds keep their existing identity.
        var channel = string.Equals(typeof(Plugin).Assembly.GetName().Name, "GillionsGameSyncTest", StringComparison.Ordinal) ? ":testing" : "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserDomainName + channel))).ToLowerInvariant();
    }

    private PreparedSnapshot PrepareSnapshot(GameSnapshot snapshot) {
        var stopwatch = Stopwatch.StartNew();
        var payloadUtf8 = JsonSerializer.SerializeToUtf8Bytes(snapshot.Payload);
        using var document = JsonDocument.Parse(payloadUtf8);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeJson(document.RootElement))));
        var inventoryComponents = snapshot.ResourceType == "inventory" ? GetInventoryComponentHashes(document.RootElement) : null;
#if GILLIONS_TEST_BUILD
        var description = DescribePayload(document.RootElement, payloadUtf8.Length);
        var inventoryDelta = snapshot.ResourceType == "inventory" ? DescribeInventoryDelta(document.RootElement) : ".";
#else
        var description = "";
        var inventoryDelta = ".";
#endif
        stopwatch.Stop();
        return new PreparedSnapshot(snapshot.ResourceType, payloadUtf8, payloadHash, inventoryComponents, description, inventoryDelta, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static Dictionary<string, string> GetInventoryComponentHashes(JsonElement root) {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "items", "retainerListings", "retainerListingsObserved" }) if (root.TryGetProperty(name, out var value)) result[name] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeJson(value))));
        return result;
    }

    private static string CanonicalizeJson(JsonElement element) => element.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => JsonSerializer.Serialize(property.Name) + ":" + CanonicalizeJson(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalizeJson).OrderBy(value => value, StringComparer.Ordinal)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()), JsonValueKind.Number => element.GetRawText(), JsonValueKind.True => "true", JsonValueKind.False => "false", JsonValueKind.Null => "null", _ => element.GetRawText(),
    };

#if GILLIONS_TEST_BUILD
    private void RecordDiagnostic(string message) {
        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        lock (diagnosticsLock) {
            diagnostics.Insert(0, line);
            if (diagnostics.Count > 40) diagnostics.RemoveRange(40, diagnostics.Count - 40);
        }
        log.Information("[Testing diagnostics] {Message}", message);
    }

    private static string DescribePayload(JsonElement root, int bytes) {
        var parts = new List<string> { $"{bytes:N0} B" };
        foreach (var name in new[] { "items", "retainerListings", "retainerBags", "ids", "completedQuestIds", "cards", "minions", "mounts", "bardings", "emotes", "orchestrions", "fashions", "blueMageSpells", "sightseeingLogIds", "aetherCurrentIds", "portraitBackgrounds", "portraitConditions", "portraitDecorations", "portraitFacials", "portraitFrames", "portraitPoses", "masterRecipeBookIds", "folkloreBookIds", "jobs", "craftingRecipeIds", "gatheringLogIds" }) {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array) parts.Add($"{name}={value.GetArrayLength()}");
        }
        if (root.TryGetProperty("eligibleCount", out var eligible) && eligible.TryGetInt32(out var eligibleCount)) parts.Add($"eligible={eligibleCount}");
        if (root.TryGetProperty("verifiedCount", out var verified) && verified.TryGetInt32(out var verifiedCount)) parts.Add($"verified={verifiedCount}");
        if (root.TryGetProperty("excludedByIdRangeCount", out var excluded) && excluded.TryGetInt32(out var excludedCount)) parts.Add($"excludedByIdRange={excludedCount}");
        if (root.TryGetProperty("ready", out var ready) && ready.ValueKind is JsonValueKind.True or JsonValueKind.False) parts.Add($"ready={ready.GetBoolean()}");
        return string.Join(", ", parts);
    }

    private string DescribeInventoryDelta(JsonElement root) {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return ".";
        var current = items.EnumerateArray().Select(CanonicalizeJson).GroupBy(value => value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var added = current.Where(entry => entry.Value > (lastInventoryRecords.TryGetValue(entry.Key, out var previous) ? previous : 0)).Select(entry => entry.Key).Take(3).ToArray();
        var removed = lastInventoryRecords.Where(entry => entry.Value > (current.TryGetValue(entry.Key, out var next) ? next : 0)).Select(entry => entry.Key).Take(3).ToArray();
        lastInventoryRecords = current;
        return added.Length == 0 && removed.Length == 0 ? "." : $"; inventory delta +[{string.Join(" | ", added)}] -[{string.Join(" | ", removed)}].";
    }

    private void DrawDiagnostics() {
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("Testing diagnostics")) return;
        ImGui.TextWrapped("This test build uses the native Gillions collector only. Copy this report after a manual sync, an inventory change, and opening the Mount/Minion/Achievement lists.");
        if (ImGui.Button("Copy diagnostic report")) {
            string[] snapshot;
            lock (diagnosticsLock) snapshot = diagnostics.ToArray();
            var report = $"Gillions Game Sync Testing {PluginVersion}\nCharacter: {objects.LocalPlayer?.Name.TextValue ?? "not logged in"}\n{string.Join("\n", snapshot.Reverse())}";
            ImGui.SetClipboardText(report);
        }
        if (ImGui.Button("Clear diagnostics")) lock (diagnosticsLock) diagnostics.Clear();
        string[] lines;
        lock (diagnosticsLock) lines = diagnostics.ToArray();
        foreach (var line in lines) ImGui.TextWrapped(line);
    }
#endif
    public void Dispose() { chatGui.ChatMessage -= OnChatMessage; chatGui.LogMessage -= OnLogMessage; gameInventory.InventoryChangedRaw -= OnInventoryChangedRaw; framework.Update -= OnFrameworkUpdate; pluginInterface.UiBuilder.Draw -= DrawSettings; pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings; commands.RemoveHandler("/gillionssync"); http.Dispose(); }
}

internal sealed record PreparedSnapshot(
    string ResourceType,
    byte[] PayloadUtf8,
    string PayloadHash,
    Dictionary<string, string>? InventoryComponentHashes,
    string Description,
    string InventoryDelta,
    double PreparationMilliseconds);

internal sealed record CapturedSnapshotBatch(string CharacterName, string CharacterWorld, GameSnapshot[] Snapshots);

public sealed class PluginConfiguration : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public string ServerUrl { get; set; } = "https://gillions.lanlab.one";
    public string PairingCode { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceToken { get; set; } = "";
    public bool AutomaticSync { get; set; } = true;
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
    public void Save(IDalamudPluginInterface pluginInterface) => pluginInterface.SavePluginConfig(this);
}

public sealed class GillionsSyncRejectedException : InvalidOperationException {
    public string Code { get; }
    public GillionsSyncRejectedException(string code, string message) : base(message) => Code = code;
}

public sealed record EnrollmentResponse(bool ok, string device_id, string token);
public sealed record GameSnapshot(string ResourceType, object Payload);
public sealed record GilLedgerEvent(string EventId, DateTime OccurredAtUtc, long GilDelta, string Kind, string Confidence, int? ItemId, int? ItemQuantity, string? RetainerId, string? RetainerName, uint? LogMessageId, int[] LogIntegerParameters, string? CharacterName = null, string? CharacterWorld = null);
internal sealed record RetainerWithdrawalObservation(DateTime ObservedAtUtc, long Amount, string RetainerId, string RetainerName);
public sealed record PendingRetainerSale(string SaleId, DateTime OccurredAtUtc, long Amount, int? ItemId, int ItemQuantity, string? RetainerId, string? RetainerName);
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

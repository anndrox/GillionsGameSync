"""Character-not-linked back-off wiring; no network or player state."""
from pathlib import Path

r = Path(__file__).resolve().parents[1]
plugin = (r / 'Plugin.cs').read_text(encoding='utf-8').replace('\r\n', '\n')
tick = plugin.split('private void UpdateOwnedState()', 1)[1].split('private void QueueRetainerUpload(', 1)[0]
gate = tick.index('if (syncInFlight || characterRefusals.Waiting(now)) return;')
# Local capture runs before the gate; every ordinary upload dispatch comes after it.
for capture in ['CaptureRetainerResult(', 'CaptureOwnedGilLedgerChange();', 'CaptureLoadedRetainerListings()']:
    assert tick.index(capture) < gate, capture
for dispatch in ['SyncAutomaticallyAsync([NextAutomaticScope()]);', 'inventoryUploads.TryDispatch(now)',
                 'SyncAutomaticallyAsync([RetainerClientPolicy.ResourceType]);', 'SyncAutomaticallyAsync([]);']:
    assert tick.index(dispatch) > gate, dispatch
# Presence proves the paired character, not the account link, so it never clears the wait.
presence = plugin.split('private async Task SendRetainerPresenceAsync(', 1)[1].split('private void ClearRetainerServerAcceptance()', 1)[0]
assert 'characterRefusals' not in presence
# Only a refusal on the current permit arms the wait; an accepted upload or a session change clears it.
sync = plugin.split('private async Task SyncAutomaticallyAsync(', 1)[1].split('private GameSnapshot[] CollectScopes(', 1)[0]
catch = sync.split('} catch (Exception error) {', 1)[1]
assert catch.index('!PermitIsCurrent(operationPermit)) return;') < catch.index('characterRefusals.Refused(DateTime.UtcNow);')
assert 'if (submitted > 0) characterRefusals.Reset();' in sync
reset = plugin.split('private void ResetSessionContext()', 1)[1].split('private void RefreshSessionContext()', 1)[0]
assert 'characterRefusals?.Reset();' in reset
print('Character refusal back-off wiring PASS: captures before the gate, uploads after it; presence never clears it; current-permit refusals only.')

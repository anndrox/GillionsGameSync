# Gillions Game Sync Testing 0.0.65

Manual Dalamud custom repository URL (available after publication):

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.65-testing/GillionsGameSyncTesting.json`

## Connection and consent

This successor keeps the separate `GillionsGameSyncTest` identity and ordinary
HTTPS sync, Beastmaster local view and previous performance improvements. Stable
distribution is unchanged. No Hunt collection, map flags or Beastmaster upload is
added.

The new testing Party Finder recipient is Gillions, not xivpf.com or localhost.
It uses only `https://gillions.app/api/game-sync/party-finder/contribute`. The
editable Gillions address cannot retarget it. Leave contribution disabled until
Site confirms production activation; a plugin release is not server deployment.

1. Replace the old custom repository URL with the URL above and refresh Dalamud.
   Confirm **Gillions Game Sync Testing 0.0.65.0**. Preserve configuration and
   disable the stable collector while testing ordinary sync.
2. On the main site's `/gillions-sync` page, select **Testing**, acknowledge real
   account data and separately grant public Party Finder contribution permission.
   Select your linked character and generate a fresh single-use code.
3. Open `/gillionssynctest pair`, use `https://gillions.app`, and pair with that code.
   Existing devices receive no new site permission automatically.
4. Explicitly enable **Contribute public Party Finder listings** locally. The old
   xivpf setting does not enable the new recipient. Automatic sync is independent.

## Checks and diagnostics

- Confirm ordinary sync still uploads supported data and matches the site.
- Manually browse/refresh Party Finder. Wait about ten seconds after the listing
  burst. Expect a **Gillions Party Finder accepted** diagnostic and no new game
  queries or noticeable collection hitch. This means v1 acknowledgement, not
  delivery to xivpf.com, durable history or verified listing ownership.
- Site should compare current read results, listing updates and expiry with the
  received observations. No reporter or credential should appear in read results.
- Disable contribution; unsent listings must clear and active requests cancel.
  Logout/character changes/re-pair must not carry observations into another session.
- Without site permission, expect a 403 stop. Correct permission by fresh explicit
  pairing. Revoked devices/account denial must stop contributions too. Ordinary
  supported sync retains its independent behavior.
- On 429/503, expect bounded retry/backoff, not per-frame requests. Lost-response
  retries preserve original timestamps; expired data is dropped, not refreshed.
- Beastmaster remains local-only and off unless deliberately enabled. Compare
  ownership manually; no-logout freshness still needs separate work/validation.

Open Diagnostics, clear it before one test, reproduce, then **Copy diagnostic
report**. Include actions, version, local time, automatic/contribution settings and
whether a hitch occurred. Review text before sharing. Never share configuration,
pairing codes, device tokens or a full unreviewed log. Diagnostics contain aggregate
counts/statuses, not public listing text or reporter credentials.

## Validation boundary

Managed fixtures and actual-product builds/configuration checks validate request
bounds, consent, session lifetime, exact acknowledgements, retries and compatibility.
Independent Security & Privacy review is required for the changed authenticated
recipient. Installed-game acceptance and server deployment/runtime validation are
separate evidence; this release does not deploy the server or merge main.

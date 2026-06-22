# News X and deletion fixes

## Scope

Fix three focused issues in the existing news feature:

- render a single embedded X post in React StrictMode;
- allow a deleted news URL to be published again while preserving audit history;
- remove the redundant `Premier liga` category filter button.

The profile page is explicitly outside this change and remains the next feature area.

## Design

### X embed lifecycle

`XEmbed` creates a new child mount element for every effect execution and passes that child to the X widgets API. Cleanup removes only that execution's mount element. If a stale asynchronous call completes after React StrictMode cleanup, it writes into a detached node and cannot create a second visible embed.

The existing timeout and external-link fallback remain unchanged.

### Deleted news identity

News deletion remains a soft delete. Before saving the deleted post, the service clears the fields that participate in external identity and uniqueness: `OriginalUrl`, `XEmbedUrl`, `ExternalId`, and `Fingerprint`.

The editorial audit event continues to store the complete pre-delete BSON snapshot, so moderators retain the original URL and all prior values. Duplicate lookup also explicitly excludes posts where `Obrisan` is true as defense in depth.

This avoids a production index migration and allows the current unique sparse indexes to release the URL immediately.

### News filters

The `Premier liga` category button is removed from the news filter list because the complete portal already covers the Premier League. `Sve`, `Transferi`, `FPL`, and `Klubovi` remain available. Existing records categorized as `premier_league` continue to appear under `Sve`.

## Verification

- A component regression test runs `XEmbed` under `React.StrictMode` and proves only the current mount remains visible.
- A service test proves deletion clears unique identity fields while preserving the old values in the audit payload.
- A repository test proves deleted posts are ignored during duplicate lookup.
- A filter test proves the redundant category button is absent and remaining filters still work.
- Full backend tests, frontend tests, and frontend production build must pass.

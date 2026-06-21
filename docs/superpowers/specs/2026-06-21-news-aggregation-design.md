# PLeague Hub News Aggregation Design

## Goal

Build a live Premier League news timeline that automatically publishes relevant RSS items, supports manually added X posts without a paid X API, and lets moderators and administrators publish and manage news immediately.

The first release must run without paid external services. A future official X API connector may be enabled without changing the public news model.

## Product Decisions

- Accepted external items are published automatically; there is no approval inbox.
- Imported content remains in its original language.
- Scope includes Premier League news, credible Premier League transfer targets, and FPL analysis.
- Moderator and administrator manual posts are published immediately.
- Both roles have full source-management permissions.
- Every item has one visible reliability label: `zvanicno`, `pouzdan_izvor`, `glasina`, or `fpl_analiza`.
- Users can comment on news with the same replies, votes, pinning, deletion, mute, and suspension rules used by the forum.
- The main view is a chronological live timeline with category filters.
- Clicking an item opens an internal detail page with source content, original link, and comments.
- The first detected source wins when a duplicate story is found.
- A later official duplicate upgrades an existing rumor to the official source and `zvanicno` label.

## Release Scope

### Included

- RSS and Atom feed ingestion.
- Rule-based Premier League and FPL relevance filtering.
- Automatic publishing and duplicate suppression.
- Manual X URL entries rendered as original embeds.
- Manual news authoring by moderators and administrators.
- News source administration and health state.
- Timeline filters, news details, and comments.
- Audit events for editorial changes.

### Excluded

- Paid X API access in the first release.
- Scraping X timelines or portal HTML pages.
- Automatic translation or AI summarization.
- Copying full copyrighted portal articles.
- Push, email, or browser notifications.

## Architecture

The existing `Posts` collection remains the canonical store for both discussions and news. This preserves the current Home and News integrations and allows the existing `Comments` collection to reference news IDs through `postId`.

The backend gains three focused units:

1. `NewsSourceService` manages source configuration, permissions, health, and pause state.
2. `NewsIngestionService` fetches, parses, filters, deduplicates, and persists feed entries.
3. `NewsIngestionWorker`, implemented as a hosted `BackgroundService`, schedules active sources every five minutes.

Provider implementations sit behind `INewsFeedProvider`. The first implementation supports RSS and Atom. A future official X provider can implement the same normalized-entry contract without changing controllers or the frontend.

## Persistence

### Extended Post

News posts add nullable fields while forum discussions keep their current behavior:

- `sourceId`: associated `NewsSource` for imported content.
- `originalUrl`: canonical external article or X URL.
- `externalId`: source-provided stable ID when available.
- `externalAuthor`: portal or X author shown to readers.
- `kategorija`: `premier_league`, `transferi`, `fpl`, or `klubovi`.
- `pouzdanost`: one of the four approved reliability labels.
- `fingerprint`: normalized duplicate key.
- `publishedAt`: original publication timestamp.
- `fetchedAt`: first successful ingestion timestamp.
- `imageUrl`: optional source image URL.
- `xEmbedUrl`: validated X status URL for manual embeds.
- `uvozAutomatski`: distinguishes imported and editorial content.
- `updatedAt`: last editorial or official-source promotion time.

`autorId` is required for manual posts and nullable for imported posts. Forum discussion creation continues to require an authenticated author.

### NewsSource

- `id`, `naziv`, `feedUrl`, `siteUrl`.
- `tip`: initially `rss`; reserved value `x_api` remains disabled.
- `podrazumevanaKategorija` and `podrazumevanaPouzdanost`.
- `ukljuceniPojmovi` and `iskljuceniPojmovi` for source-specific filtering.
- `aktivan`, `pauziranRazlog`, `uzastopneGreske`.
- `etag`, `lastModified`, `poslednjaProveraAt`, `poslednjiUspehAt`.
- `createdBy`, `updatedBy`, `createdAt`, `updatedAt`.

MongoDB uses indexes on `Posts(tip, publishedAt)`, `Posts(originalUrl)`, `Posts(fingerprint)`, and `NewsSources(aktivan, poslednjaProveraAt)`. Canonical external URLs and external IDs use sparse unique indexes where possible to prevent concurrent duplicate inserts.

## Ingestion Flow

1. The worker selects active, due sources.
2. The provider requests the feed with stored `ETag` and `Last-Modified` headers.
3. The response is parsed with a structured RSS/Atom parser, never string slicing.
4. Each item is normalized to title, permitted excerpt, author, canonical URL, publication time, and media URL.
5. Relevance filtering checks Premier League club names and aliases, league terms, transfer phrases tied to a Premier League club, FPL terms, and source-specific include/exclude terms.
6. The service sanitizes text and removes unsafe HTML.
7. It computes a fingerprint from the canonical URL and normalized title tokens.
8. Existing canonical URL, external ID, and fingerprint matches are treated as duplicates.
9. A duplicate is discarded unless an official source is upgrading an existing rumor. In that case the existing item receives the official source, URL, label, and update timestamp.
10. New accepted entries are persisted as visible news immediately.
11. Source health, conditional request headers, and timestamps are updated.

Filtering is deterministic and free in release one. Moderators and administrators can correct a category or reliability label after publication.

## Manual Publishing

Moderator and administrator share the same editorial permissions.

A manual article accepts title, body, category, reliability, optional image, and optional original source URL. It is published immediately and records the authenticated author.

A manual X entry accepts a valid `https://x.com/{user}/status/{id}` URL plus title, category, and reliability. The detail page renders the original X embed. If the embed cannot load or the post is removed, the stored news item remains visible with an unavailable notice and its original link.

Full third-party article text is not copied. RSS items contain the source title, the feed-provided permitted excerpt, attribution, and original link.

## API Surface

Public endpoints:

- `GET /api/news`: paginated timeline with category, reliability, source, and date filters.
- `GET /api/news/{id}`: internal detail response.
- `GET /api/news/{id}/comments`: threaded comments.
- `POST /api/news/{id}/comments`: authenticated comment creation.
- Existing comment vote routes remain shared.

Moderator and administrator endpoints:

- `POST /api/news`: create a manual article.
- `POST /api/news/x`: create a manual X item.
- `PUT /api/news/{id}`: edit category, reliability, title, or editorial content.
- `DELETE /api/news/{id}`: soft-delete an item.
- `GET /api/news/sources`: list source configuration and health.
- `POST /api/news/sources`: add an RSS source.
- `PUT /api/news/sources/{id}`: edit a source.
- `DELETE /api/news/sources/{id}`: deactivate a source while preserving its history and existing news.
- `PUT /api/news/sources/{id}/pause`: pause a source.
- `DELETE /api/news/sources/{id}/pause`: resume a source.
- `POST /api/news/sources/{id}/sync`: manually trigger one source check.

Mutation endpoints write an immutable editorial audit event containing actor, action, target, old values, new values, and timestamp.

## Frontend Experience

The main News page uses the approved live timeline:

- Header with page title and `Objavi vest` for moderators and administrators.
- Filter controls for `Sve`, `Transferi`, `FPL`, and `Klubovi`, with optional source and reliability filters.
- Chronological timeline rows showing source time, source name, reliability badge, headline, origin type, and comment count.
- Loading skeleton, empty state, retryable error state, and cursor-based incremental loading.

The detail page shows the reliability badge, exact and relative publication time, source attribution, original-language content, portal excerpt or X embed, original link, and the shared threaded comment UI.

Source management is a restrained operational table available to moderator and administrator, with health, last success, error count, pause state, edit, sync, and deactivate actions.

## Authorization and Moderation

- Guests can read news and comments.
- Registered users can comment and vote unless muted or suspended.
- Moderator and administrator can publish news, manage sources, edit labels, pin or hide news, and moderate comments.
- Existing role hierarchy still governs actions against user-authored comments: moderators cannot moderate administrator or moderator content; administrators can moderate moderator content but not another administrator.
- Automated posts have no user target and can be edited or hidden by either editorial role.

## Security and Reliability

- Feed URLs must use HTTPS.
- DNS resolution rejects loopback, link-local, private, and reserved addresses before every fetch to prevent SSRF and DNS rebinding.
- Fetches have strict connect/read timeouts, redirect limits, response-size limits, and accepted content types.
- Feed HTML is sanitized before persistence and rendering.
- One source failure never stops other sources.
- Three consecutive failures automatically pause a source and record the reason.
- Conditional requests reduce bandwidth and repeated parsing.
- A unique-index conflict is handled as a duplicate, not an ingestion failure.
- The worker supports cancellation and graceful shutdown.
- API and embed failures return Serbian user-facing messages and retain the original source link when possible.

## Testing Strategy

Backend tests cover RSS and Atom parsing, relevance rules, sanitization, canonical URLs, all duplicate keys, official rumor promotion, conditional requests, automatic pause after three failures, SSRF rejection, response limits, cancellation, role authorization, audit events, and news comment moderation.

Frontend tests cover filters, timeline ordering, all reliability badges, role-only controls, detail navigation, X fallback, loading/empty/error states, comments, and mobile overflow.

Integration verification uses a local deterministic RSS fixture server and MongoDB Docker container. It verifies idempotent repeated sync, concurrent sync duplicate prevention, source pause/resume, manual X fallback, and Swagger contracts without contacting production feeds during tests.

## Rollout

1. Add persistence contracts, indexes, repositories, and source administration.
2. Add safe RSS ingestion and deterministic filtering behind a disabled worker flag.
3. Add timeline/detail APIs and shared news comments.
4. Build the approved live timeline, detail page, editor, and source table.
5. Enable selected RSS sources one at a time after manual feed validation.
6. Keep the future X API connector disabled until credentials and a spending limit are explicitly configured.

# FootAPI Team Sync Design

## Scope

This phase imports Premier League teams and current standings from FootAPI into the existing `Teams` MongoDB collection. It does not import players, matches, team profile details, or historical standings.

## External Data

- Provider: FootAPI7 through RapidAPI
- Premier League tournament ID: `17`
- Default season ID: `96668` (`26/27`)
- Standings endpoint: `/api/tournament/17/season/{seasonId}/standings/total`

The API key remains in .NET User Secrets under `FootApi:ApiKey`. It must never be returned by the API, logged, or committed.

## Data Model

`Team` receives a nullable integer `ProviderId`, stored as `provider_id`. MongoDB continues to own `_id`; provider identifiers are external keys and never replace existing document IDs.

The first synchronization resolves teams in this order:

1. Exact `ProviderId` match.
2. Normalized team-name match for documents that do not yet have a provider ID.
3. Create a new team when neither match exists.

This preserves current MongoDB IDs and therefore preserves player links and user favorites. A unique sparse index on `provider_id` prevents duplicate provider teams.

## Updated Fields

The standings response updates `ProviderId`, name, abbreviation when available, points, and position. Existing logo URL, stadium, and founding year values are preserved because standings do not contain those profile fields. New teams use an empty logo URL and stadium plus zero founding year until the team-profile and media-caching phase fills them. Provider image URLs are not stored because browser requests to those URLs would require exposing RapidAPI credentials.

## Components

- `IFootballProvider` gains a method for retrieving tournament standings.
- `FootApiClient` sends the authenticated RapidAPI request and deserializes a small provider DTO.
- `TeamSyncService` validates the complete provider response, matches teams, and performs create or update operations through `IRepository<Team>`.
- `IntegrationsController` exposes the administrator-only synchronization command in Swagger.
- `MongoIndexInitializer` creates the unique sparse provider-ID index.

## API Contract

`POST /api/integrations/football/sync/teams?seasonId=96668`

- Authorization: JWT role `administrator`
- Success: `200 OK` with `created`, `updated`, and `skipped` counts plus tournament and season IDs
- Missing or invalid season ID: `400 Bad Request`
- Provider/configuration/response failure: `502 Bad Gateway`
- Missing or non-administrator token: `401 Unauthorized` or `403 Forbidden`

The endpoint performs a user-triggered synchronization and consumes one RapidAPI request per execution.

## Failure Behavior

The complete standings payload is fetched and validated before MongoDB writes begin. Invalid or empty standings cause the operation to fail without writes. Repository failures are surfaced as server errors; this phase does not introduce MongoDB transactions because each team upsert is independently repeatable.

## Testing

- `FootApiClient` tests cover route construction, headers, and standings deserialization.
- `TeamSyncService` tests cover provider-ID matching, first-run name matching, creation, field preservation, and invalid payload rejection.
- Endpoint tests cover authentication, authorization, success summary, and invalid season IDs.
- The complete backend test suite remains the regression gate.

## Deferred Work

Team profile enrichment, players, matches, advanced statistics, historical standings, scheduled background synchronization, and frontend sync controls remain separate phases.

# Global Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a centered header search that returns matching Premier League players and teams from MongoDB.

**Architecture:** React calls one public `GET /api/search` endpoint after a 250 ms debounce. The .NET search service queries `Players` and `Teams`, applies case-insensitive prefix matching, limits each result type, and returns one stable result DTO. External football providers remain outside this search path; their synchronized MongoDB records become searchable automatically.

**Tech Stack:** React 19, TypeScript, Axios, ASP.NET Core 10, MongoDB.Driver, Vitest, xUnit.

---

### Task 1: Backend Search Contract

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/SearchResultResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/SearchService.cs`
- Create: `backend/PLeagueHub.Api/Controllers/SearchController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test: `backend/PLeagueHub.Api.Tests/SearchEndpointsTests.cs`

- [ ] Write endpoint tests for two-character validation, case-insensitive prefix matching, mixed player/team results, and result limits.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj -c Release --filter SearchEndpointsTests` and confirm RED.
- [ ] Implement `GET /api/search?q=Erl&limit=8` with result fields `id`, `type`, `name`, `subtitle`, and `imageUrl`.
- [ ] Run the filtered tests and confirm GREEN.

### Task 2: Frontend Search Client

**Files:**
- Create: `frontend/src/services/searchApi.ts`
- Modify: `frontend/src/services/dataApi.test.ts`
- Modify: `frontend/src/types/api.ts`

- [ ] Add a failing service contract assertion for `GET /api/search` with `q` and `limit` params.
- [ ] Run `npm.cmd test -- src/services/dataApi.test.ts` and confirm RED.
- [ ] Implement the typed search client and result union.
- [ ] Run the service test and confirm GREEN.

### Task 3: Centered Header Search

**Files:**
- Create: `frontend/src/components/GlobalSearch.tsx`
- Create: `frontend/src/hooks/useDebouncedValue.ts`
- Create: `frontend/src/hooks/useDebouncedValue.test.ts`
- Modify: `frontend/src/components/Layout.tsx`

- [ ] Write and run a failing debounce behavior test.
- [ ] Implement a 250 ms debounce, request cancellation guard, loading state, keyboard escape, outside-click close, and result dropdown.
- [ ] Center the search between the brand and authentication controls on desktop; use a full-width row below them on mobile.
- [ ] Run frontend tests and production build.

### Task 4: Integration Verification

**Files:**
- No production files.

- [ ] Start MongoDB, seed data, API, and Vite.
- [ ] Verify `q=Ar` returns Arsenal and `q=Mar` returns matching seeded players when present.
- [ ] Verify Swagger exposes `/api/search` and visually inspect desktop and mobile layouts.
- [ ] Run the complete backend and frontend test suites.

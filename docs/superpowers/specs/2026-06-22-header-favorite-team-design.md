# Header Favorite Team and Profile Removal

## Goal

Remove the dedicated Profile page and move the only user preference, one favorite Premier League team, into the authenticated header. The selected team becomes part of the user's visible identity by appearing before their username in forum and news comments.

## Scope

This change includes:

- removing the Profile navigation item, route, and page;
- redirecting successful login and registration to the home page when no protected return location exists;
- allowing every authenticated user to select zero or one favorite team from the header;
- displaying the favorite team's crest beside comment authors on forum and news pages;
- replacing the registered-user role badge in comments with no badge while retaining moderator and administrator badges.

It does not add a replacement settings page, user avatars, editable email/username, activity statistics, or public profile pages.

## Data model and compatibility

The existing `favoritniTimovi` array remains in MongoDB and the public user response to avoid an unnecessary breaking migration. Its new invariant is zero or one team ID.

The existing `PUT /api/users/me/favorite-teams` contract also remains. It accepts:

- `teamIds: []` to clear the favorite team;
- `teamIds: [teamId]` to select one team;
- more than one distinct team ID returns `400 Bad Request`.

A startup migration truncates legacy arrays to their first stored ID. If that team no longer exists, display code omits the crest and the user can select a current team. Seed data is updated so the seeded fan has one favorite team. Users may change or clear their favorite at any time.

## Authenticated header

The account area keeps its current position at the upper right. Its order is:

1. favorite-team button;
2. username and secondary account text;
3. logout button.

The secondary text is:

- `Clan od dd.MM.yyyy.` for role `registrovani`;
- `Moderator` for role `moderator`;
- `Administrator` for role `administrator`.

The favorite-team button is always visible. With a favorite, it displays the team's crest. Without a favorite, it displays a deliberate white outline `Shield` icon inside the same dark square frame. It never renders an empty or broken image element.

The account area remains compact on mobile. The button and username stay visible; text truncates before controls overlap.

## Favorite-team menu

Clicking the crest or default shield opens a compact menu anchored below the account area. The menu contains:

- `Bez omiljenog kluba`;
- all available Premier League teams with crest and name;
- a radio-style selected state because only one choice is allowed.

Selecting a row saves immediately through the existing users API and closes the menu after success. During the request, choices are disabled. If saving fails, the menu remains open and shows an inline error. The menu closes on outside click or Escape.

The existing team list already loaded by `Layout` is reused. A focused `FavoriteTeamMenu` component owns open/close behavior and mutation state so `Layout` remains readable.

## Comment identity

`ForumCommentResponse` is extended with an optional favorite-team summary containing:

- team ID;
- team name;
- team logo URL.

`CommentService` resolves the first favorite team for each unique comment author and maps the summary once into responses. Forum and news comments already share this service and `ForumComment`, so the behavior remains consistent across both surfaces.

`ForumComment` renders a 20-pixel crest immediately before the username. The crest uses the existing `TeamLogo` component and exposes the team name as a tooltip. Users without a favorite team show no placeholder beside comments; the white default shield is only an interactive header control.

Registered users no longer display a `Clan` role badge in comments. Moderator and administrator badges remain visible because they communicate moderation authority.

## Profile and authentication navigation

The Profile item is removed from desktop and mobile navigation. The `/profile` route and `Profile.tsx` page are removed.

Login and registration continue to honor a protected `location.state.from` destination. When no return destination exists, both successful flows navigate to `/` instead of `/profile`.

## Error and loading behavior

- The header shows the default shield while no favorite is selected.
- Team-list loading does not block the rest of the header.
- A failed team update keeps the previous server-confirmed selection and shows an inline menu error.
- Comment rendering tolerates a missing or deleted team and simply omits the crest.
- Backend validation remains authoritative for unknown team IDs and the one-team limit.

## Verification

- Backend tests prove the API accepts zero or one team and rejects two.
- Migration tests prove legacy multi-team arrays become one-team arrays and repeated migration is safe.
- Comment service tests prove favorite-team metadata is returned and missing teams are tolerated.
- Header tests prove member-date and moderator/administrator secondary labels.
- Menu tests prove the default shield, immediate selection, clearing, failure state, outside click, and Escape behavior.
- Forum and news comment tests prove the crest appears before the username and registered users have no role badge.
- Login and registration tests prove the default redirect is `/` while protected return routes remain unchanged.
- Full backend tests, frontend tests, and frontend production build must pass.

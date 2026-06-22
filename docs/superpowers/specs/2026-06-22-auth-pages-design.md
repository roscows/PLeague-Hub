# Login and Registration Pages

## Scope

This change completes the public authentication entry flow before the profile page is redesigned. It removes development-only seed hints from login and adds a dedicated registration page with matching visual structure.

The authenticated profile layout and profile features are outside this change and will be designed next.

## Routes and navigation

- `/login` displays the login form.
- `/register` displays the registration form.
- Login links to registration with `Nemas nalog? Registruj se.`
- Registration links to login with `Vec imas nalog? Uloguj se.`
- The protected-route return location is passed between both pages so either authentication path can return the user to the original destination.
- Without a return location, successful authentication navigates to `/profile`.

## Login page

The existing card structure and PLeague Hub styling remain. The seeded admin email, seeded password, and development explanation are removed. Both inputs start empty and use appropriate browser autocomplete attributes.

User-facing labels and actions use Serbian Latin terminology: `Email ili korisnicko ime`, `Lozinka`, and `Prijavi se`.

## Registration page

The registration card uses the same header, width, spacing, fields, errors, and button treatment as login. It contains:

- korisnicko ime;
- email;
- lozinka;
- repeated password confirmation.

Client validation requires all fields, a password of at least eight characters, and matching passwords. The API remains authoritative for duplicate username/email checks and returns those errors inside the form.

During submission, controls prevent a duplicate request and show a clear pending action. A successful registration uses the existing `AuthContext.register`, which stores the JWT, loads the current user, and then navigates to the return location or `/profile`.

## Boundaries

No backend contract changes are needed. The existing `POST /api/auth/register` request already accepts `username`, `email`, and `password`, while password confirmation remains a frontend-only field.

No seed accounts are removed from the database; only their public development hint and prefilled credentials are removed from the login UI.

## Verification

- Login renders empty fields and no seed message.
- Login links to `/register` and preserves route state.
- Registration validates password length and equality before calling the API.
- Successful registration calls the existing auth context and redirects correctly.
- Registration links back to `/login` while preserving route state.
- Full frontend tests and production build pass.

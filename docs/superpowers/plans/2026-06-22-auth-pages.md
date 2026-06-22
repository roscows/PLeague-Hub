# Login and Registration Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver clean, linked login and registration pages without public seed credentials, using the existing authentication API and redirect behavior.

**Architecture:** Add a small shared card shell for consistent auth presentation while keeping login and registration form state independent. Registration uses the existing `AuthContext.register`; route state is passed through both links so protected-route redirects survive switching forms.

**Tech Stack:** React 19, TypeScript, React Router, Tailwind CSS, Axios, Vitest, Testing Library

---

### Task 1: Clean login flow

**Files:**
- Create: `frontend/src/components/auth/AuthCard.tsx`
- Modify: `frontend/src/pages/Login.tsx`
- Test: `frontend/src/pages/Login.test.tsx`

- [ ] **Step 1: Write the failing login test**

Render `Login` inside a `MemoryRouter` with route state `{ from: { pathname: '/forum/topic-1' } }`. Assert that email/username and password inputs are empty, the seed explanation is absent, the submit button is named `Prijavi se`, and the `Registruj se` link targets `/register` while preserving the same state.

- [ ] **Step 2: Run the test and verify RED**

```powershell
npm.cmd test -- --run src/pages/Login.test.tsx
```

Expected: failure because credentials are prefilled, seed copy is visible, and no registration link exists.

- [ ] **Step 3: Add the shared auth card**

Create `AuthCard` with this public interface:

```tsx
interface AuthCardProps {
  eyebrow: string;
  title: string;
  children: ReactNode;
  footer: ReactNode;
}

export function AuthCard({ eyebrow, title, children, footer }: AuthCardProps) {
  return (
    <section className="mx-auto max-w-md overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <header className="bg-ink px-6 py-5 text-white">
        <p className="text-[10px] font-bold uppercase text-red-400">{eyebrow}</p>
        <h1 className="mt-1 text-xl font-extrabold">{title}</h1>
      </header>
      <div className="p-6">{children}</div>
      <footer className="border-t border-slate-200 bg-slate-50 px-6 py-4 text-center text-sm text-slate-600">{footer}</footer>
    </section>
  );
}
```

- [ ] **Step 4: Update login behavior and copy**

Initialize both fields with empty strings, remove the seed paragraph, add `autoComplete="username"` and `autoComplete="current-password"`, rename labels/actions to Serbian Latin, and render this footer:

```tsx
<span>
  Nemas nalog?{' '}
  <Link className="font-bold text-brand hover:underline" state={location.state} to="/register">
    Registruj se
  </Link>
</span>
```

Keep the existing successful redirect calculation unchanged.

- [ ] **Step 5: Run the login test and verify GREEN**

Run the command from Step 2. Expected: all login assertions pass.

### Task 2: Registration flow

**Files:**
- Create: `frontend/src/pages/Register.tsx`
- Modify: `frontend/src/App.tsx`
- Test: `frontend/src/pages/Register.test.tsx`

- [ ] **Step 1: Write failing registration tests**

Add tests that require:

```tsx
expect(screen.getByLabelText('Korisnicko ime')).toBeRequired();
expect(screen.getByLabelText('Email')).toBeRequired();
expect(screen.getByLabelText('Lozinka')).toBeRequired();
expect(screen.getByLabelText('Ponovi lozinku')).toBeRequired();
```

Submit mismatched passwords and assert `register` is not called and `Lozinke se ne podudaraju.` is shown. Submit a password shorter than eight characters and assert `Lozinka mora imati najmanje 8 karaktera.`. Submit valid data and assert:

```tsx
expect(register).toHaveBeenCalledWith({
  username: 'marko',
  email: 'marko@example.com',
  password: 'PLeague123!'
});
```

Also assert navigation returns to the route from location state, and that `Uloguj se` links to `/login` with the same state.

- [ ] **Step 2: Run the registration test and verify RED**

```powershell
npm.cmd test -- --run src/pages/Register.test.tsx
```

Expected: failure because the page and route do not exist.

- [ ] **Step 3: Implement the registration page**

Create controlled state for `username`, `email`, `password`, `confirmPassword`, `error`, and `pending`. On submit:

```tsx
if (password.length < 8) {
  setError('Lozinka mora imati najmanje 8 karaktera.');
  return;
}
if (password !== confirmPassword) {
  setError('Lozinke se ne podudaraju.');
  return;
}

setPending(true);
try {
  await register({ username: username.trim(), email: email.trim(), password });
  const redirectTo = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/profile';
  navigate(redirectTo, { replace: true });
} catch (requestError) {
  setError(getApiErrorMessage(requestError, 'Registracija nije uspela. Proveri podatke.'));
} finally {
  setPending(false);
}
```

Use `AuthCard`, required inputs, browser autocomplete values `username`, `email`, and `new-password`, and disable the submit button while `pending`. The footer links to `/login` and passes `location.state`.

- [ ] **Step 4: Register the route**

Import `Register` in `App.tsx` and add:

```tsx
<Route path="register" element={<Register />} />
```

beside the public login route.

- [ ] **Step 5: Run focused auth tests and verify GREEN**

```powershell
npm.cmd test -- --run src/pages/Login.test.tsx src/pages/Register.test.tsx
```

Expected: login and registration tests pass.

- [ ] **Step 6: Commit production files only**

```powershell
git add frontend/src/components/auth/AuthCard.tsx frontend/src/pages/Login.tsx frontend/src/pages/Register.tsx frontend/src/App.tsx
git commit -m "Add linked login and registration pages"
```

### Task 3: Full verification and delivery

**Files:**
- No production file changes expected

- [ ] **Step 1: Run full frontend verification**

```powershell
npm.cmd test -- --run
npm.cmd run build
git diff --check
git status --short
```

Expected: all frontend tests pass, production build succeeds, no whitespace errors, and ignored test files remain absent from Git status.

- [ ] **Step 2: Verify routing and public rendering**

Open `/login` and `/register` through the running frontend. Verify empty login fields, both cross-links, registration validation, no horizontal overflow at desktop and mobile widths, and no seed copy.

- [ ] **Step 3: Push master**

```powershell
git push origin master
```

Expected: local `HEAD` equals `origin/master`.

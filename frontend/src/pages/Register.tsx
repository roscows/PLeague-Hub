import { FormEvent, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AuthCard } from '../components/auth/AuthCard';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';

export function Register() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

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
      const redirectTo = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';
      navigate(redirectTo, { replace: true });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Registracija nije uspela. Proveri podatke.'));
    } finally {
      setPending(false);
    }
  }

  const fieldClass = 'mt-1 w-full rounded-md border border-slate-300 px-3 py-2 outline-none focus:border-brand';

  return (
    <AuthCard
      eyebrow="PLeague Hub nalog"
      title="Registracija"
      footer={<span>Vec imas nalog? <Link className="font-bold text-brand hover:underline" state={location.state} to="/login">Uloguj se</Link></span>}
    >
      <form className="space-y-4" onSubmit={handleSubmit}>
        <label className="block text-sm">
          Korisnicko ime
          <input autoComplete="username" className={fieldClass} onChange={(event) => setUsername(event.target.value)} required value={username} />
        </label>
        <label className="block text-sm">
          Email
          <input autoComplete="email" className={fieldClass} onChange={(event) => setEmail(event.target.value)} required type="email" value={email} />
        </label>
        <label className="block text-sm">
          Lozinka
          <input autoComplete="new-password" className={fieldClass} onChange={(event) => setPassword(event.target.value)} required type="password" value={password} />
        </label>
        <label className="block text-sm">
          Ponovi lozinku
          <input autoComplete="new-password" className={fieldClass} onChange={(event) => setConfirmPassword(event.target.value)} required type="password" value={confirmPassword} />
        </label>
        {error && <p aria-live="polite" className="text-sm text-red-600">{error}</p>}
        <button className="w-full rounded-md bg-brand px-4 py-2 font-bold text-white disabled:cursor-not-allowed disabled:opacity-60" disabled={pending} type="submit">
          {pending ? 'Registracija...' : 'Registruj se'}
        </button>
      </form>
    </AuthCard>
  );
}

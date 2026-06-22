import { FormEvent, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AuthCard } from '../components/auth/AuthCard';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';

export function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [emailOrUsername, setEmailOrUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    try {
      await login(emailOrUsername, password);
      const redirectTo = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/';
      navigate(redirectTo, { replace: true });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Login nije uspeo. Proveri podatke.'));
    }
  }

  return (
    <AuthCard
      eyebrow="PLeague Hub nalog"
      title="Prijava"
      footer={<span>Nemas nalog? <Link className="font-bold text-brand hover:underline" state={location.state} to="/register">Registruj se</Link></span>}
    >
      <form className="space-y-4" onSubmit={handleSubmit}>
        <label className="block text-sm">
          Email ili korisnicko ime
          <input
            autoComplete="username"
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            required
            value={emailOrUsername}
            onChange={(event) => setEmailOrUsername(event.target.value)}
          />
        </label>
        <label className="block text-sm">
          Lozinka
          <input
            autoComplete="current-password"
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            required
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button className="w-full rounded-md bg-brand px-4 py-2 font-bold text-white" type="submit">
          Prijavi se
        </button>
      </form>
    </AuthCard>
  );
}

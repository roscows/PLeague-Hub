import { FormEvent, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';

export function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [emailOrUsername, setEmailOrUsername] = useState('admin@pleaguehub.local');
  const [password, setPassword] = useState('PLeague123!');
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    try {
      await login(emailOrUsername, password);
      const redirectTo = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '/profile';
      navigate(redirectTo, { replace: true });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Login nije uspeo. Proveri podatke.'));
    }
  }

  return (
    <section className="mx-auto max-w-md overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="bg-ink px-6 py-5 text-white">
        <p className="text-[10px] font-bold uppercase text-red-400">PLeague Hub nalog</p>
        <h1 className="mt-1 text-xl font-extrabold">Prijava</h1>
      </div>
      <div className="p-6">
      <p className="mt-1 text-sm text-slate-500">Seed admin podaci su unapred popunjeni za brzu proveru.</p>
      <form className="mt-5 space-y-4" onSubmit={handleSubmit}>
        <label className="block text-sm">
          Email ili username
          <input
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            value={emailOrUsername}
            onChange={(event) => setEmailOrUsername(event.target.value)}
          />
        </label>
        <label className="block text-sm">
          Password
          <input
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button className="w-full rounded-md bg-brand px-4 py-2 font-bold text-white" type="submit">
          Login
        </button>
      </form>
      </div>
    </section>
  );
}

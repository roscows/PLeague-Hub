import { FormEvent, useEffect, useState } from 'react';
import { getApiErrorMessage } from '../services/apiError';
import { teamsApi } from '../services/teamsApi';
import { usersApi } from '../services/usersApi';
import { useAuth } from '../contexts/AuthContext';
import type { Team } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

export function Profile() {
  const { user, refreshProfile } = useAuth();
  const [teams, setTeams] = useState<Team[]>([]);
  const [selectedTeamIds, setSelectedTeamIds] = useState<string[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    teamsApi.list().then(setTeams).catch((requestError) => {
      setError(getApiErrorMessage(requestError, 'Timovi trenutno nisu dostupni.'));
    });
  }, []);

  useEffect(() => {
    setSelectedTeamIds(user?.favoritniTimovi ?? []);
  }, [user]);

  function toggleTeam(teamId: string) {
    setSelectedTeamIds((current) =>
      current.includes(teamId) ? current.filter((id) => id !== teamId) : [...current, teamId]
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);

    try {
      await usersApi.updateFavoriteTeams(selectedTeamIds);
      await refreshProfile();
      setMessage('Omiljeni timovi su sacuvani.');
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Omiljeni timovi nisu sacuvani.'));
    }
  }

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <p className="text-[10px] font-bold uppercase text-brand">Korisnicki nalog</p>
        <h1 className="mt-1 text-xl font-extrabold">Profil</h1>
        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-slate-500">Username</dt>
            <dd className="font-medium">{user?.username}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Email</dt>
            <dd className="font-medium">{user?.email}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Uloga</dt>
            <dd className="font-medium">{user?.uloga}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Status</dt>
            <dd className="font-medium">{user?.aktivan ? 'Aktivan' : 'Suspendovan'}</dd>
          </div>
        </dl>
      </section>

      <form className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm" onSubmit={handleSubmit}>
        <h2 className="text-lg font-extrabold">Omiljeni timovi</h2>
        <div className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {teams.map((team) => (
            <label key={team.id} className="flex items-center gap-3 rounded-md border border-slate-200 p-3 text-sm hover:bg-slate-50">
              <input
                type="checkbox"
                checked={selectedTeamIds.includes(team.id)}
                onChange={() => toggleTeam(team.id)}
              />
              <TeamLogo className="size-7" logoUrl={team.logoUrl} name={team.naziv} />
              <span className="font-semibold">{team.naziv}</span>
            </label>
          ))}
        </div>
        {message && <p className="mt-3 text-sm text-pitch">{message}</p>}
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
        <button className="mt-4 rounded-md bg-brand px-4 py-2 text-sm font-bold text-white" type="submit">
          Sacuvaj
        </button>
      </form>
    </div>
  );
}

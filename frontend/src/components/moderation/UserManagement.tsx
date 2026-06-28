import { Search, ShieldCheck, UserCog, Users } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { ModerationModal } from '../forum/ModerationModal';
import { getApiErrorMessage } from '../../services/apiError';
import { moderationApi } from '../../services/moderationApi';
import { panelApi } from '../../services/panelApi';
import { useAuth } from '../../contexts/AuthContext';
import type { ModerationState, PanelUser, Role } from '../../types/api';

function roleLabel(role: string): string {
  if (role === 'administrator') return 'Administrator';
  if (role === 'moderator') return 'Moderator';
  return 'Registrovani';
}

interface ModerationTarget {
  id: string;
  username: string;
  role: Role;
  state: ModerationState | null;
}

export function UserManagement() {
  const { hasRole } = useAuth();
  const isAdmin = hasRole('administrator');
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<PanelUser[]>([]);
  const [searched, setSearched] = useState(false);
  const [staff, setStaff] = useState<PanelUser[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [moderationTarget, setModerationTarget] = useState<ModerationTarget | null>(null);

  async function loadStaff() {
    try {
      setStaff(await panelApi.searchUsers('', true));
    } catch {
      /* tiho */
    }
  }

  useEffect(() => {
    loadStaff();
  }, []);

  async function search(event: FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      setResults(await panelApi.searchUsers(query.trim()));
      setSearched(true);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Pretraga korisnika nije uspela.'));
    }
  }

  async function changeRole(user: PanelUser, uloga: 'registrovani' | 'moderator') {
    setError(null);
    try {
      await panelApi.changeRole(user.id, uloga);
      setResults((current) => current.map((item) => (item.id === user.id ? { ...item, uloga } : item)));
      await loadStaff();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Uloga nije promenjena.'));
    }
  }

  async function moderate(user: PanelUser) {
    setError(null);
    try {
      const state = await moderationApi.getUserState(user.id);
      setModerationTarget({ id: user.id, username: user.username, role: user.uloga, state });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Podaci za moderaciju nisu ucitani.'));
    }
  }

  function renderRow(user: PanelUser) {
    return (
      <li key={user.id} className="flex flex-wrap items-center gap-2 px-4 py-2.5 text-sm">
        <span className="min-w-0">
          <span className="block truncate font-semibold">{user.username}</span>
          <span className="block truncate text-[11px] text-slate-400">{user.email}</span>
        </span>
        <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-bold uppercase text-slate-600">{roleLabel(user.uloga)}</span>
        {user.aktivnaMera && <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-700">{user.aktivnaMera}</span>}
        <div className="ml-auto flex items-center gap-2">
          {isAdmin && user.uloga !== 'administrator' && (
            <select
              aria-label={`Uloga za ${user.username}`}
              className="rounded border border-slate-300 px-2 py-1 text-xs"
              value={user.uloga}
              onChange={(event) => changeRole(user, event.target.value as 'registrovani' | 'moderator')}
            >
              <option value="registrovani">Registrovani</option>
              <option value="moderator">Moderator</option>
            </select>
          )}
          <button
            aria-label={`Moderisi korisnika ${user.username}`}
            className="grid size-7 place-items-center rounded border border-slate-300 text-slate-700 hover:bg-slate-50 hover:text-brand"
            onClick={() => moderate(user)}
            title="Moderisi"
            type="button"
          >
            <UserCog size={14} />
          </button>
        </div>
      </li>
    );
  }

  return (
    <section className="space-y-4">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <Users size={16} className="text-brand" />
          <h2 className="text-sm font-extrabold">Korisnici</h2>
        </div>
        <form className="flex gap-2 p-3" onSubmit={search}>
          <label className="relative flex-1">
            <Search className="absolute left-3 top-2.5 text-slate-400" size={15} />
            <input
              aria-label="Pretrazi korisnike"
              className="w-full rounded-md border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-brand"
              placeholder="Pretrazi po imenu ili email-u"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
          </label>
          <button className="rounded bg-brand px-3 text-xs font-bold text-white" type="submit">Trazi</button>
        </form>

        {error && <p className="px-4 pb-2 text-sm text-red-600">{error}</p>}

        {searched && (
          results.length === 0 ? (
            <p className="px-4 py-4 text-center text-sm text-slate-400">Nema rezultata.</p>
          ) : (
            <ul className="divide-y divide-slate-100 border-t border-slate-100">{results.map(renderRow)}</ul>
          )
        )}
      </div>

      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <ShieldCheck size={16} className="text-brand" />
          <h2 className="text-sm font-extrabold">Tim (administratori i moderatori)</h2>
        </div>
        {staff.length === 0 ? (
          <p className="px-4 py-4 text-center text-sm text-slate-400">Nema clanova tima.</p>
        ) : (
          <ul className="divide-y divide-slate-100">{staff.map(renderRow)}</ul>
        )}
      </div>

      {moderationTarget && (
        <ModerationModal
          currentState={moderationTarget.state}
          onChanged={() => setModerationTarget(null)}
          onClose={() => setModerationTarget(null)}
          target={{ id: moderationTarget.id, username: moderationTarget.username, role: moderationTarget.role }}
        />
      )}
    </section>
  );
}

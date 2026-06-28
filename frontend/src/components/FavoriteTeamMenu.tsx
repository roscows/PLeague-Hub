import { Check, Shield } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';
import { teamsApi } from '../services/teamsApi';
import { usersApi } from '../services/usersApi';
import type { Team } from '../types/api';
import { TeamLogo } from './TeamLogo';

interface FavoriteTeamMenuProps {
  teams: Team[];
  selectedTeam?: Team;
}

export function FavoriteTeamMenu({ teams, selectedTeam }: FavoriteTeamMenuProps) {
  const { refreshProfile } = useAuth();
  const rootRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [pendingTeamId, setPendingTeamId] = useState<string | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [availableTeams, setAvailableTeams] = useState<Team[]>(teams);

  useEffect(() => {
    if (teams.length > 0) setAvailableTeams(teams);
  }, [teams]);

  // The header loads teams once; if that call failed (e.g. API restarting) the
  // list would stay empty. Fetch on open so the menu always offers the teams.
  useEffect(() => {
    if (isOpen && availableTeams.length === 0) {
      teamsApi.list().then(setAvailableTeams).catch(() => undefined);
    }
  }, [isOpen, availableTeams.length]);

  useEffect(() => {
    if (!isOpen) return;

    function closeOnPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setIsOpen(false);
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setIsOpen(false);
    }

    document.addEventListener('pointerdown', closeOnPointerDown);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnPointerDown);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [isOpen]);

  async function selectTeam(teamId: string | null) {
    setPendingTeamId(teamId);
    setError(null);

    try {
      await usersApi.updateFavoriteTeams(teamId ? [teamId] : []);
      await refreshProfile();
      setIsOpen(false);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Omiljeni tim nije sacuvan.'));
    } finally {
      setPendingTeamId(undefined);
    }
  }

  const label = selectedTeam ? `Omiljeni tim: ${selectedTeam.naziv}` : 'Izaberi omiljeni tim';

  return (
    <div className="relative" ref={rootRef}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-label={label}
        className="grid size-9 place-items-center rounded-md border border-white/20 bg-white/10 transition hover:bg-white/15"
        onClick={() => {
          setError(null);
          setIsOpen((current) => !current);
        }}
        title={label}
        type="button"
      >
        {selectedTeam ? (
          <TeamLogo className="size-6" logoUrl={selectedTeam.logoUrl} name={selectedTeam.naziv} />
        ) : (
          <Shield aria-hidden="true" className="size-5 text-white" strokeWidth={1.8} />
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 top-11 z-50 w-64 overflow-hidden rounded-md border border-slate-200 bg-white text-slate-900 shadow-xl">
          <p className="border-b border-slate-100 px-3 py-2 text-[10px] font-bold uppercase text-slate-400">
            Omiljeni tim
          </p>
          <div aria-label="Izbor omiljenog tima" className="max-h-72 overflow-y-auto p-1" role="listbox">
            <TeamOption
              isPending={pendingTeamId === null}
              isSelected={!selectedTeam}
              label="Bez omiljenog kluba"
              onSelect={() => selectTeam(null)}
            />
            {availableTeams.map((team) => (
              <TeamOption
                isPending={pendingTeamId === team.id}
                isSelected={selectedTeam?.id === team.id}
                key={team.id}
                logo={<TeamLogo className="size-5" logoUrl={team.logoUrl} name={team.naziv} />}
                label={team.naziv}
                onSelect={() => selectTeam(team.id)}
              />
            ))}
          </div>
          {error && <p className="border-t border-red-100 bg-red-50 px-3 py-2 text-xs text-red-700">{error}</p>}
        </div>
      )}
    </div>
  );
}

interface TeamOptionProps {
  label: string;
  logo?: ReactNode;
  isSelected: boolean;
  isPending: boolean;
  onSelect: () => void;
}

function TeamOption({ label, logo, isSelected, isPending, onSelect }: TeamOptionProps) {
  return (
    <button
      aria-selected={isSelected}
      className="flex w-full items-center gap-2 rounded px-2 py-2 text-left text-sm hover:bg-slate-50 disabled:opacity-60"
      disabled={isPending}
      onClick={onSelect}
      role="option"
      type="button"
    >
      <span className="grid size-5 shrink-0 place-items-center">
        {logo ?? <Shield className="size-4 text-slate-400" />}
      </span>
      <span className="min-w-0 flex-1 truncate font-semibold">{label}</span>
      {isSelected && <Check aria-hidden="true" className="size-4 text-brand" />}
    </button>
  );
}

import type { Match, Team } from '../types/api';
import { RelativeTime } from './RelativeTime';
import { TeamIdentity } from './TeamIdentity';

interface MatchRowProps {
  match: Match;
  teams: Map<string, Team>;
}

export function MatchRow({ match, teams }: MatchRowProps) {
  const home = teams.get(match.domacinId);
  const away = teams.get(match.gostId);
  const played = match.golDomacin !== null && match.golGost !== null;

  return (
    <div className="grid grid-cols-[56px_minmax(0,1fr)_44px] items-center gap-3 border-b border-slate-100 px-3 py-3 last:border-0 sm:grid-cols-[72px_minmax(0,1fr)_64px]">
      <div className="text-center">
        {played ? (
          <>
            <p className="text-xs font-bold text-slate-700">FT</p>
            <p className="mt-1 text-[10px] font-semibold uppercase text-slate-400">{match.status}</p>
            {match.zavrsenaAt && <RelativeTime className="mt-1 block text-[10px] normal-case text-slate-400" value={match.zavrsenaAt} />}
          </>
        ) : (
          <>
            <p className="text-xs font-bold text-slate-700">
              {new Date(match.datum).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })}
            </p>
            <p className="mt-1 text-[10px] font-semibold text-brand">
              {new Date(match.datum).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </p>
          </>
        )}
      </div>

      <div className="space-y-2">
        <TeamIdentity team={home} compact />
        <TeamIdentity team={away} compact />
      </div>

      <div className="space-y-2 text-center text-sm font-black">
        <p>{match.golDomacin ?? '-'}</p>
        <p>{match.golGost ?? '-'}</p>
      </div>
    </div>
  );
}

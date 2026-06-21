import { Star } from 'lucide-react';
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
    <div className="grid grid-cols-[56px_minmax(0,1fr)_44px] items-center gap-3 border-b border-slate-100 px-3 py-3 last:border-0 sm:grid-cols-[72px_minmax(0,1fr)_64px_36px]">
      <div className="text-center">
        <p className="text-xs font-bold text-slate-700">
          {played ? 'FT' : new Date(match.datum).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </p>
        <p className={`mt-1 text-[10px] font-semibold uppercase ${played ? 'text-slate-400' : 'text-brand'}`}>
          {match.status}
        </p>
        {played && match.zavrsenaAt && <RelativeTime className="mt-1 block text-[10px] normal-case text-slate-400" value={match.zavrsenaAt} />}
      </div>

      <div className="space-y-2">
        <TeamIdentity team={home} compact />
        <TeamIdentity team={away} compact />
      </div>

      <div className="space-y-2 text-center text-sm font-black">
        <p>{match.golDomacin ?? '-'}</p>
        <p>{match.golGost ?? '-'}</p>
      </div>

      <button className="hidden text-slate-300 hover:text-amber-400 sm:block" title="Dodaj u favorite">
        <Star size={17} />
      </button>
    </div>
  );
}

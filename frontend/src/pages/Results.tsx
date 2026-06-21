import { Filter, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { MatchRow } from '../components/MatchRow';
import { matchesApi } from '../services/matchesApi';
import { teamsApi } from '../services/teamsApi';
import type { Match, Team } from '../types/api';

export function Results() {
  const [matches, setMatches] = useState<Match[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [status, setStatus] = useState('');
  const [season, setSeason] = useState('');

  useEffect(() => {
    teamsApi.list().then(setTeams);
  }, []);

  useEffect(() => {
    matchesApi.list({ status: status || undefined, season: season || undefined }).then(setMatches);
  }, [status, season]);

  const teamMap = useMemo(() => new Map(teams.map((team) => [team.id, team])), [teams]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-[10px] font-bold uppercase text-brand">Premier League</p>
            <h1 className="mt-1 text-xl font-extrabold">Rezultati i raspored</h1>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <label className="relative">
              <Filter className="absolute left-3 top-2.5 text-slate-400" size={15} />
              <input
                className="w-full rounded-md border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-brand sm:w-36"
                placeholder="2026/27"
                value={season}
                onChange={(event) => setSeason(event.target.value)}
              />
            </label>
            <select
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={status}
              onChange={(event) => setStatus(event.target.value)}
            >
              <option value="">Sve utakmice</option>
              <option value="zakazana">Zakazane</option>
              <option value="uzivo">Uzivo</option>
              <option value="zavrsena">Zavrsene</option>
            </select>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center gap-3 bg-ink px-4 py-3 text-white">
          <Trophy size={17} className="text-red-400" />
          <div>
            <p className="text-[10px] font-bold uppercase text-slate-400">Engleska</p>
            <h2 className="text-sm font-extrabold">Premier League</h2>
          </div>
        </div>
        {matches.map((match) => <MatchRow key={match.id} match={match} teams={teamMap} />)}
      </section>
    </div>
  );
}

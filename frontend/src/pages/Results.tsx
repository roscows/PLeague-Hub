import { Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { MatchRow } from '../components/MatchRow';
import { matchesApi } from '../services/matchesApi';
import { teamsApi } from '../services/teamsApi';
import { DEFAULT_SEASON } from '../constants';
import type { Match, Team } from '../types/api';

export function Results() {
  const [matches, setMatches] = useState<Match[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [season, setSeason] = useState('');
  const [gameweek, setGameweek] = useState('');
  const [status, setStatus] = useState('');

  useEffect(() => {
    teamsApi.list().then(setTeams);
  }, []);

  useEffect(() => {
    matchesApi.list().then(setMatches);
  }, []);

  const seasons = useMemo(
    () => [...new Set(matches.map((match) => match.sezona))].sort().reverse(),
    [matches]
  );

  const selectedSeason = season || (seasons.includes(DEFAULT_SEASON) ? DEFAULT_SEASON : seasons[0]) || '';

  const gameweeks = useMemo(
    () =>
      [...new Set(matches.filter((match) => match.sezona === selectedSeason).map((match) => match.kolo))]
        .sort((a, b) => a - b),
    [matches, selectedSeason]
  );

  const latestGameweek = gameweeks.length > 0 ? gameweeks[gameweeks.length - 1] : null;
  const selectedGameweek =
    gameweek !== '' && gameweeks.includes(Number(gameweek)) ? Number(gameweek) : latestGameweek;

  const teamMap = useMemo(() => new Map(teams.map((team) => [team.id, team])), [teams]);

  const visibleMatches = useMemo(
    () =>
      matches
        .filter((match) => (selectedSeason ? match.sezona === selectedSeason : true))
        .filter((match) => (selectedGameweek !== null ? match.kolo === selectedGameweek : true))
        .filter((match) => (status ? match.status === status : true))
        .sort((a, b) => new Date(a.datum).getTime() - new Date(b.datum).getTime()),
    [matches, selectedSeason, selectedGameweek, status]
  );

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-[10px] font-bold uppercase text-brand">Premier League</p>
            <h1 className="mt-1 text-xl font-extrabold text-slate-950">Rezultati i raspored</h1>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <select
              aria-label="Sezona"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={selectedSeason}
              onChange={(event) => setSeason(event.target.value)}
            >
              {seasons.length === 0 && <option value="">Sezona</option>}
              {seasons.map((value) => (
                <option key={value} value={value}>{value}</option>
              ))}
            </select>
            <select
              aria-label="GW"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={selectedGameweek ?? ''}
              onChange={(event) => setGameweek(event.target.value)}
            >
              {gameweeks.map((value) => (
                <option key={value} value={value}>GW{value}</option>
              ))}
            </select>
            <select
              aria-label="Status"
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
        {visibleMatches.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema utakmica za izabrane filtere.</p>
        ) : (
          visibleMatches.map((match) => <MatchRow key={match.id} match={match} teams={teamMap} />)
        )}
      </section>
    </div>
  );
}

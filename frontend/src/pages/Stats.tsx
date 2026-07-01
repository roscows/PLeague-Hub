import { Search, TrendingUp } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { playerStatsApi } from '../services/playerStatsApi';
import { standingsApi } from '../services/standingsApi';
import { DEFAULT_SEASON } from '../constants';
import type { PlayerStat, Season } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

type SortKey = 'golovi' | 'asistencije';

export function Stats() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [season, setSeason] = useState('');
  const [players, setPlayers] = useState<PlayerStat[]>([]);
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('golovi');
  const [status, setStatus] = useState<'loading' | 'ready'>('loading');

  useEffect(() => {
    standingsApi.getSeasons().then(setSeasons).catch(() => setSeasons([]));
  }, []);

  const selectedSeason = season
    || (seasons.some((item) => item.season === DEFAULT_SEASON) ? DEFAULT_SEASON : seasons[0]?.season)
    || '';

  useEffect(() => {
    if (!selectedSeason) {
      setPlayers([]);
      setStatus('ready');
      return;
    }

    setStatus('loading');
    playerStatsApi.get(selectedSeason)
      .then((data) => {
        setPlayers(data);
        setStatus('ready');
      })
      .catch(() => {
        setPlayers([]);
        setStatus('ready');
      });
  }, [selectedSeason]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return players
      .filter((player) => (term ? player.ime.toLowerCase().includes(term) : true))
      .slice()
      .sort((a, b) => b[sortKey] - a[sortKey]);
  }, [players, search, sortKey]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
              <TrendingUp size={13} /> Najbolji igraci
            </p>
            <h1 className="mt-1 text-xl font-extrabold">Statistike igraca</h1>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <label className="relative">
              <Search className="absolute left-3 top-2.5 text-slate-400" size={15} />
              <input
                className="w-full rounded-md border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-brand"
                placeholder="Pretrazi igraca"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </label>
            <select
              aria-label="Sezona"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={selectedSeason}
              onChange={(event) => setSeason(event.target.value)}
            >
              {seasons.map((item) => (
                <option key={item.season} value={item.season}>{item.season}</option>
              ))}
            </select>
            <div className="flex overflow-hidden rounded-md border border-slate-300 text-sm font-semibold">
              <button
                className={`px-3 py-2 ${sortKey === 'golovi' ? 'bg-brand text-white' : 'text-slate-600'}`}
                onClick={() => setSortKey('golovi')}
                type="button"
              >
                Gol
              </button>
              <button
                className={`px-3 py-2 ${sortKey === 'asistencije' ? 'bg-brand text-white' : 'text-slate-600'}`}
                onClick={() => setSortKey('asistencije')}
                type="button"
              >
                Ast
              </button>
            </div>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        {status === 'ready' && visible.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema statistike za izabranu sezonu.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="bg-ink text-[10px] uppercase text-slate-300">
                <tr>
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Igrac</th>
                  <th className="px-4 py-3">Tim</th>
                  <th className="px-4 py-3 text-right">Gol</th>
                  <th className="px-4 py-3 text-right">Ast</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((player, index) => (
                  <tr key={player.providerId} className="border-b border-slate-100 hover:bg-slate-50">
                    <td className="px-4 py-3 text-xs font-bold text-slate-400">{index + 1}</td>
                    <td className="px-4 py-3 font-bold">
                      <Link to={`/igrac/${player.providerId}`} className="hover:text-brand hover:underline">
                        {player.ime}
                      </Link>
                    </td>
                    <td className="px-4 py-3">
                      {player.teamProviderId > 0 ? (
                        <Link to={`/klub/${player.teamProviderId}`} className="flex items-center gap-2 hover:text-brand">
                          <TeamLogo className="size-6" logoUrl={player.teamLogoUrl} name={player.teamNaziv} />
                          <span className="text-xs font-semibold">{player.teamNaziv}</span>
                        </Link>
                      ) : (
                        <div className="flex items-center gap-2">
                          <TeamLogo className="size-6" logoUrl={player.teamLogoUrl} name={player.teamNaziv} />
                          <span className="text-xs font-semibold">{player.teamNaziv}</span>
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right font-black">{player.golovi}</td>
                    <td className="px-4 py-3 text-right font-black">{player.asistencije}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

import { Search, TrendingUp } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { playersApi } from '../services/playersApi';
import { teamsApi } from '../services/teamsApi';
import type { Player, Team } from '../types/api';

export function Stats() {
  const [players, setPlayers] = useState<Player[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [search, setSearch] = useState('');
  const [teamId, setTeamId] = useState('');

  useEffect(() => {
    teamsApi.list().then(setTeams);
  }, []);

  useEffect(() => {
    playersApi.list({ search: search || undefined, teamId: teamId || undefined }).then(setPlayers);
  }, [search, teamId]);

  const teamMap = useMemo(() => new Map(teams.map((team) => [team.id, team])), [teams]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
              <TrendingUp size={13} /> Forma i ucinak
            </p>
            <h1 className="mt-1 text-xl font-extrabold">Statistike igraca</h1>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
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
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={teamId}
              onChange={(event) => setTeamId(event.target.value)}
            >
              <option value="">Svi timovi</option>
              {teams.map((team) => (
                <option key={team.id} value={team.id}>{team.naziv}</option>
              ))}
            </select>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-left text-sm">
            <thead className="bg-ink text-[10px] uppercase text-slate-300">
              <tr>
                <th className="px-4 py-3">#</th>
                <th className="px-4 py-3">Igrac</th>
                <th className="px-4 py-3">Tim</th>
                <th className="px-4 py-3">Poz.</th>
                <th className="px-4 py-3 text-right">Gol</th>
                <th className="px-4 py-3 text-right">Ast</th>
                <th className="px-4 py-3 text-right">Ocena</th>
              </tr>
            </thead>
            <tbody>
              {players.map((player, index) => {
                const team = teamMap.get(player.teamId);
                return (
                  <tr key={player.id} className="border-b border-slate-100 hover:bg-slate-50">
                    <td className="px-4 py-3 text-xs font-bold text-slate-400">{index + 1}</td>
                    <td className="px-4 py-3">
                      <p className="font-bold">{player.ime} {player.prezime}</p>
                      <p className="mt-0.5 text-xs text-slate-400">{player.nacionalnost}</p>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <img className="size-6 object-contain" src={team?.logoUrl} alt="" />
                        <span className="text-xs font-semibold">{team?.skracenica ?? '-'}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-xs font-semibold text-slate-500">{player.pozicija}</td>
                    <td className="px-4 py-3 text-right font-black">{player.golovi}</td>
                    <td className="px-4 py-3 text-right font-black">{player.asistencije}</td>
                    <td className="px-4 py-3 text-right">
                      <span className="rounded bg-emerald-100 px-2 py-1 text-xs font-black text-emerald-700">
                        {player.ocena.toFixed(1)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

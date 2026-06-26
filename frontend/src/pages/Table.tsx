import { ListOrdered } from 'lucide-react';
import { useEffect, useState } from 'react';
import { standingsApi } from '../services/standingsApi';
import type { Season, StandingRow } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

export function TablePage() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [season, setSeason] = useState('');
  const [rows, setRows] = useState<StandingRow[]>([]);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    standingsApi.getSeasons()
      .then(setSeasons)
      .catch(() => setSeasons([]));
  }, []);

  const selectedSeason = season || seasons[0]?.season || '';

  useEffect(() => {
    if (!selectedSeason) {
      setRows([]);
      setStatus('ready');
      return;
    }

    setStatus('loading');
    standingsApi.getStandings(selectedSeason)
      .then((data) => {
        setRows(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [selectedSeason]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
              <ListOrdered size={13} /> Premier League
            </p>
            <h1 className="mt-1 text-xl font-extrabold">Tabela</h1>
          </div>
          <label className="flex flex-col text-xs font-semibold text-slate-500">
            Sezona
            <select
              aria-label="Sezona"
              className="mt-1 rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand"
              value={selectedSeason}
              onChange={(event) => setSeason(event.target.value)}
            >
              {seasons.map((item) => (
                <option key={item.season} value={item.season}>{item.season}</option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        {status === 'loading' && (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Ucitavanje tabele...</p>
        )}
        {status === 'error' && (
          <p className="px-4 py-8 text-center text-sm text-red-500">
            Trenutno nije moguce ucitati tabelu. Pokusaj ponovo kasnije.
          </p>
        )}
        {status === 'ready' && rows.length === 0 && (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema podataka za izabranu sezonu.</p>
        )}
        {status === 'ready' && rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="bg-ink text-[10px] uppercase text-slate-300">
                <tr>
                  <th className="px-3 py-3">#</th>
                  <th className="px-3 py-3">Klub</th>
                  <th className="px-3 py-3 text-center">OD</th>
                  <th className="px-3 py-3 text-center">P</th>
                  <th className="px-3 py-3 text-center">N</th>
                  <th className="px-3 py-3 text-center">I</th>
                  <th className="hidden px-3 py-3 text-center sm:table-cell">GF:GA</th>
                  <th className="hidden px-3 py-3 text-center sm:table-cell">GR</th>
                  <th className="px-3 py-3 text-right">Bod</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((team) => (
                  <tr key={team.position} className="border-b border-slate-100 hover:bg-slate-50">
                    <td className="px-3 py-3 text-xs font-bold text-slate-400">{team.position}</td>
                    <td className="px-3 py-3">
                      <div className="flex items-center gap-2">
                        <TeamLogo className="size-6" logoUrl={team.logoUrl} name={team.naziv} />
                        <span className="text-xs font-semibold">{team.naziv}</span>
                      </div>
                    </td>
                    <td className="px-3 py-3 text-center text-xs">{team.odigrano}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.pobede}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.nereseno}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.porazi}</td>
                    <td className="hidden px-3 py-3 text-center text-xs sm:table-cell">
                      {team.datiGolovi}:{team.primljeniGolovi}
                    </td>
                    <td className="hidden px-3 py-3 text-center text-xs font-semibold sm:table-cell">
                      {team.golRazlika > 0 ? `+${team.golRazlika}` : team.golRazlika}
                    </td>
                    <td className="px-3 py-3 text-right font-black">{team.bodovi}</td>
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

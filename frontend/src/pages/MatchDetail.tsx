import { ArrowLeft, Goal, RectangleVertical } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { matchDetailApi } from '../services/matchDetailApi';
import type { MatchDetail } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

function statPercent(value: string): number {
  const parsed = Number.parseFloat(value.replace('%', ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

export function MatchDetailPage() {
  const { id } = useParams();
  const [detail, setDetail] = useState<MatchDetail | null>(null);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    if (!id) {
      return;
    }

    setStatus('loading');
    matchDetailApi.get(id)
      .then((data) => {
        setDetail(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [id]);

  if (status === 'loading') {
    return <p className="px-4 py-10 text-center text-sm text-slate-400">Ucitavanje detalja...</p>;
  }

  if (status === 'error' || !detail) {
    return <p className="px-4 py-10 text-center text-sm text-red-500">Trenutno nije moguce ucitati detalje meca.</p>;
  }

  const { header, statistics, incidents, lineups } = detail;
  const played = header.golDomacin !== null && header.golGost !== null;
  const empty = statistics.length === 0 && incidents.length === 0 && !lineups;

  return (
    <div className="space-y-4">
      <Link to="/results" className="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-brand">
        <ArrowLeft size={14} /> Rezultati
      </Link>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <p className="text-center text-[10px] font-bold uppercase text-slate-400">
          {header.sezona} · {header.kolo}. kolo
        </p>
        <div className="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3">
          <div className="flex flex-col items-center gap-2 text-center">
            <TeamLogo className="size-12" logoUrl={header.domacin.logoUrl} name={header.domacin.naziv} />
            <span className="text-sm font-bold">{header.domacin.naziv}</span>
          </div>
          <div className="text-center">
            {played ? (
              <p className="text-3xl font-black">{header.golDomacin} : {header.golGost}</p>
            ) : (
              <p className="text-sm font-bold text-brand">
                {new Date(header.datum).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })}
                <br />
                {new Date(header.datum).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </p>
            )}
            <p className="mt-1 text-[10px] font-semibold uppercase text-slate-400">{header.status}</p>
          </div>
          <div className="flex flex-col items-center gap-2 text-center">
            <TeamLogo className="size-12" logoUrl={header.gost.logoUrl} name={header.gost.naziv} />
            <span className="text-sm font-bold">{header.gost.naziv}</span>
          </div>
        </div>
      </section>

      {empty && (
        <section className="rounded-lg border border-slate-200 bg-white p-6 text-center text-sm text-slate-400 shadow-sm">
          Statistika dostupna nakon meca.
        </section>
      )}

      {incidents.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <h2 className="bg-ink px-4 py-2 text-[11px] font-bold uppercase text-slate-300">Tok meca</h2>
          <ul className="divide-y divide-slate-100">
            {incidents.map((incident, index) => (
              <li key={`${incident.minut}-${index}`} className={`flex items-center gap-2 px-4 py-2 text-sm ${incident.domacin ? '' : 'flex-row-reverse text-right'}`}>
                <span className="w-8 text-xs font-bold text-slate-400">{incident.minut}'</span>
                {incident.tip === 'goal' ? <Goal size={15} className="text-emerald-600" /> : <RectangleVertical size={15} className="text-amber-500" />}
                <span className="font-semibold">{incident.tekst}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {statistics.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="mb-3 text-[11px] font-bold uppercase text-slate-400">Statistika</h2>
          <div className="space-y-3">
            {statistics.map((stat) => {
              const home = statPercent(stat.domacin);
              const total = home + statPercent(stat.gost);
              const homePct = total > 0 ? (home / total) * 100 : 50;
              return (
                <div key={stat.naziv}>
                  <div className="flex justify-between text-xs font-semibold">
                    <span>{stat.domacin}</span>
                    <span className="text-slate-500">{stat.naziv}</span>
                    <span>{stat.gost}</span>
                  </div>
                  <div className="mt-1 flex h-1.5 overflow-hidden rounded bg-slate-200">
                    <div className="bg-brand" style={{ width: `${homePct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      )}

      {lineups && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="mb-3 text-[11px] font-bold uppercase text-slate-400">Postave</h2>
          <div className="grid grid-cols-2 gap-4">
            {[lineups.domacin, lineups.gost].map((team, side) => (
              <div key={side}>
                <p className="mb-2 text-xs font-bold text-brand">{team.formacija}</p>
                <ul className="space-y-1">
                  {team.igraci.filter((player) => !player.zamena).map((player) => (
                    <li key={player.ime} className="flex items-center gap-2 text-xs">
                      <span className="w-5 text-right font-bold text-slate-400">{player.broj}</span>
                      <span className="font-semibold">{player.ime}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

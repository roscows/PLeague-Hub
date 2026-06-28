import { ArrowLeft } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { clubsApi } from '../services/clubsApi';
import { TeamLogo } from '../components/TeamLogo';
import type { ClubProfile, ClubRoster } from '../types/api';

const FORM_STYLES: Record<string, string> = {
  W: 'bg-emerald-500 text-white',
  D: 'bg-slate-300 text-slate-700',
  L: 'bg-red-500 text-white'
};

const POSITION_GROUPS: { code: string; label: string }[] = [
  { code: 'G', label: 'Golmani' },
  { code: 'D', label: 'Odbrana' },
  { code: 'M', label: 'Vezni red' },
  { code: 'F', label: 'Napad' }
];

export function ClubProfilePage() {
  const { id } = useParams();
  const [club, setClub] = useState<ClubProfile | null>(null);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    if (!id) {
      return;
    }

    setStatus('loading');
    clubsApi.getProfile(Number(id))
      .then((data) => {
        setClub(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [id]);

  if (status === 'loading') {
    return <p className="px-4 py-10 text-center text-sm text-slate-400">Ucitavanje kluba...</p>;
  }

  if (status === 'error' || !club) {
    return <p className="px-4 py-10 text-center text-sm text-red-500">Profil kluba trenutno nije dostupan.</p>;
  }

  return (
    <div className="space-y-4">
      <Link to="/tabela" className="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-brand">
        <ArrowLeft size={14} /> Tabela
      </Link>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center gap-4">
          <TeamLogo className="size-16" logoUrl={club.logoUrl} name={club.naziv} />
          <div className="min-w-0">
            <h1 className="text-xl font-extrabold">{club.naziv}</h1>
            {club.pozicija > 0 && (
              <p className="text-sm font-semibold text-slate-500">
                {club.pozicija}. mesto · {club.sezona}
              </p>
            )}
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2 text-xs">
          {club.stadion && <Chip label="Stadion" value={club.stadion} />}
          {club.trener && <Chip label="Trener" value={club.trener} />}
          {club.osnovan > 0 && <Chip label="Osnovan" value={String(club.osnovan)} />}
          {club.drzava && <Chip label="Drzava" value={club.drzava} />}
        </div>

        {club.forma.length > 0 && (
          <div className="mt-4 flex items-center gap-2">
            <span className="text-[10px] font-bold uppercase text-slate-400">Forma</span>
            {club.forma.map((result, index) => (
              <span
                key={index}
                className={`grid size-6 place-items-center rounded text-[11px] font-black ${FORM_STYLES[result] ?? 'bg-slate-200'}`}
              >
                {result}
              </span>
            ))}
          </div>
        )}
      </section>

      {club.poslednjiMecevi.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <h2 className="bg-ink px-4 py-2 text-[11px] font-bold uppercase text-slate-300">Poslednji mecevi</h2>
          <ul className="divide-y divide-slate-100">
            {club.poslednjiMecevi.map((match, index) => (
              <li key={match.mecId || index}>
                <Link to={`/mec/${match.mecId}`} className="flex items-center gap-3 px-4 py-2 text-sm hover:bg-slate-50">
                  <span className={`grid size-5 shrink-0 place-items-center rounded text-[10px] font-black ${FORM_STYLES[match.ishod] ?? 'bg-slate-200'}`}>
                    {match.ishod}
                  </span>
                  <span className="text-[10px] font-semibold uppercase text-slate-400">{match.domaci ? 'DOM' : 'GOST'}</span>
                  <TeamLogo className="size-5" logoUrl={match.protivnikLogo} name={match.protivnik} />
                  <span className="flex-1 truncate font-semibold">{match.protivnik}</span>
                  <span className="font-black">{match.golMi} : {match.golProtivnik}</span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <h2 className="bg-ink px-4 py-2 text-[11px] font-bold uppercase text-slate-300">Igracki kadar</h2>
        {club.roster.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-400">Spisak igraca nije dostupan.</p>
        ) : (
          <div className="p-4">
            {POSITION_GROUPS.map((group) => {
              const players = club.roster.filter((player) => player.pozicija === group.code);
              if (players.length === 0) {
                return null;
              }
              return (
                <div key={group.code} className="mb-4 last:mb-0">
                  <p className="mb-2 text-[10px] font-bold uppercase text-slate-400">{group.label}</p>
                  <ul className="grid gap-1 sm:grid-cols-2">
                    {players.map((player) => (
                      <RosterRow key={player.providerId} player={player} />
                    ))}
                  </ul>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

function RosterRow({ player }: { player: ClubRoster }) {
  return (
    <li>
      <Link
        to={`/igrac/${player.providerId}`}
        className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-slate-50"
      >
        <span className="w-6 text-right text-xs font-bold text-slate-400">{player.broj > 0 ? player.broj : ''}</span>
        <span className="flex-1 truncate font-semibold text-brand">{player.ime}</span>
        {player.drzava && <span className="text-[11px] text-slate-400">{player.drzava}</span>}
      </Link>
    </li>
  );
}

function Chip({ label, value }: { label: string; value: string }) {
  return (
    <span className="rounded-md bg-slate-100 px-2 py-1">
      <span className="text-slate-400">{label}: </span>
      <span className="font-semibold">{value}</span>
    </span>
  );
}

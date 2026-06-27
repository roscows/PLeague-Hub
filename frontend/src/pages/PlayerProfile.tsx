import { ArrowLeft } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { playersApi } from '../services/playersApi';
import { resolveApiAssetUrl } from '../services/assets';
import type { PlayerProfile } from '../types/api';

const POSITIONS: Record<string, string> = {
  G: 'Golman',
  D: 'Odbrana',
  M: 'Vezni red',
  F: 'Napad'
};

function positionLabel(code: string): string {
  return POSITIONS[code] ?? code;
}

function initials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

export function PlayerProfilePage() {
  const { id } = useParams();
  const [profile, setProfile] = useState<PlayerProfile | null>(null);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    if (!id) {
      return;
    }

    setStatus('loading');
    playersApi.getProfile(Number(id))
      .then((data) => {
        setProfile(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [id]);

  if (status === 'loading') {
    return <p className="px-4 py-10 text-center text-sm text-slate-400">Ucitavanje profila...</p>;
  }

  if (status === 'error' || !profile) {
    return <p className="px-4 py-10 text-center text-sm text-red-500">Profil igraca trenutno nije dostupan.</p>;
  }

  const photo = resolveApiAssetUrl(profile.fotoUrl);

  return (
    <div className="space-y-4">
      <Link to="/stats" className="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-brand">
        <ArrowLeft size={14} /> Statistike
      </Link>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex items-center gap-4">
          {photo ? (
            <img alt="" className="size-20 shrink-0 rounded-full object-cover" src={photo} />
          ) : (
            <span className="grid size-20 shrink-0 place-items-center rounded-full bg-slate-100 text-lg font-black text-slate-400">
              {initials(profile.ime)}
            </span>
          )}
          <div className="min-w-0">
            <h1 className="text-xl font-extrabold">{profile.ime}</h1>
            <p className="text-sm font-semibold text-slate-500">{positionLabel(profile.pozicija)}</p>
            {profile.klubProviderId > 0 ? (
              <Link to={`/klub/${profile.klubProviderId}`} className="text-sm font-bold text-brand hover:underline">
                {profile.klubNaziv}
              </Link>
            ) : (
              <span className="text-sm font-bold">{profile.klubNaziv}</span>
            )}
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2 text-xs">
          {profile.drzava && <Chip label="Nacionalnost" value={profile.drzava} />}
          {profile.godine !== null && <Chip label="Godine" value={String(profile.godine)} />}
          {profile.visina > 0 && <Chip label="Visina" value={`${profile.visina} cm`} />}
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <h2 className="bg-ink px-4 py-2 text-[11px] font-bold uppercase text-slate-300">Statistika po sezonama</h2>
        {profile.sezone.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-400">Nema zabelezenih sezona.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[480px] text-left text-sm">
              <thead className="text-[10px] uppercase text-slate-400">
                <tr>
                  <th className="px-4 py-2">Sezona</th>
                  <th className="px-4 py-2">Tim</th>
                  <th className="px-4 py-2 text-right">Gol</th>
                  <th className="px-4 py-2 text-right">Ast</th>
                  <th className="px-4 py-2 text-right">Nastupi</th>
                </tr>
              </thead>
              <tbody>
                {profile.sezone.map((line) => (
                  <tr key={line.sezona} className="border-t border-slate-100">
                    <td className="px-4 py-2 font-bold">{line.sezona}</td>
                    <td className="px-4 py-2">
                      {line.teamProviderId > 0 ? (
                        <Link to={`/klub/${line.teamProviderId}`} className="font-semibold text-brand hover:underline">
                          {line.teamNaziv}
                        </Link>
                      ) : (
                        <span className="font-semibold">{line.teamNaziv}</span>
                      )}
                    </td>
                    <td className="px-4 py-2 text-right font-black">{line.golovi}</td>
                    <td className="px-4 py-2 text-right font-black">{line.asistencije}</td>
                    <td className="px-4 py-2 text-right">{line.odigrano}</td>
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

function Chip({ label, value }: { label: string; value: string }) {
  return (
    <span className="rounded-md bg-slate-100 px-2 py-1">
      <span className="text-slate-400">{label}: </span>
      <span className="font-semibold">{value}</span>
    </span>
  );
}

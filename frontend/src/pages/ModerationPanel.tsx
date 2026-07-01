import { ShieldAlert, Trash2, UserCog, X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { ModerationModal } from '../components/forum/ModerationModal';
import { ActivityFeed } from '../components/moderation/ActivityFeed';
import { StaffNotices } from '../components/moderation/StaffNotices';
import { UserManagement } from '../components/moderation/UserManagement';
import { RelativeTime } from '../components/RelativeTime';
import { getApiErrorMessage } from '../services/apiError';
import { moderationApi } from '../services/moderationApi';
import { reportsApi } from '../services/reportsApi';
import type { CommentReport, ModerationState, Role } from '../types/api';

const CATEGORY_LABELS: Record<string, string> = {
  spam: 'Spam',
  uvrede: 'Uvrede',
  offtopic: 'Off-topic',
  ostalo: 'Ostalo'
};

interface AuthorTarget {
  id: string;
  username: string;
  role: Role;
  state: ModerationState | null;
}

export function ModerationPanel() {
  const [reports, setReports] = useState<CommentReport[]>([]);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');
  const [error, setError] = useState<string | null>(null);
  const [authorTarget, setAuthorTarget] = useState<AuthorTarget | null>(null);
  const [modSignal, setModSignal] = useState(0);

  function afterModeration() {
    setModSignal((current) => current + 1);
  }

  async function load() {
    setStatus('loading');
    try {
      setReports(await reportsApi.listPending());
      setStatus('ready');
    } catch {
      setStatus('error');
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function resolve(id: string, akcija: 'obrisi' | 'odbaci') {
    setError(null);
    try {
      await reportsApi.resolve(id, akcija);
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Akcija nije izvrsena.'));
    }
  }

  async function moderateAuthor(report: CommentReport) {
    setError(null);
    try {
      const state = await moderationApi.getUserState(report.autorId);
      setAuthorTarget({ id: report.autorId, username: report.autorUsername, role: report.autorUloga, state });
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Podaci za moderaciju nisu ucitani.'));
    }
  }

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
          <ShieldAlert size={13} /> Moderacija
        </p>
        <h1 className="mt-1 text-xl font-extrabold">Moderacija</h1>
        <p className="mt-1 text-sm text-slate-500">Centar za moderaciju zajednice.</p>
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <StaffNotices />
        <ActivityFeed reloadSignal={modSignal} />
      </div>

      <h2 className="px-1 pt-2 text-sm font-extrabold text-slate-700">Prijave komentara{reports.length > 0 ? ` (${reports.length})` : ''}</h2>

      {error && <p className="rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        {status === 'loading' && <p className="px-4 py-8 text-center text-sm text-slate-400">Ucitavanje prijava...</p>}
        {status === 'error' && <p className="px-4 py-8 text-center text-sm text-red-500">Prijave nije moguce ucitati.</p>}
        {status === 'ready' && reports.length === 0 && (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema prijava na cekanju.</p>
        )}

        {status === 'ready' && reports.length > 0 && (
          <ul className="divide-y divide-slate-100">
            {reports.map((report) => (
              <li key={report.id} className="p-4">
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="rounded bg-red-100 px-2 py-0.5 font-bold text-red-700">{CATEGORY_LABELS[report.kategorija] ?? report.kategorija}</span>
                  <span className="text-slate-500">Autor: <span className="font-semibold text-slate-700">{report.autorUsername}</span></span>
                  <span className="text-slate-500">Prijavio: <span className="font-semibold text-slate-700">{report.prijavioUsername}</span></span>
                  <RelativeTime className="text-slate-400" value={report.datumPrijave} />
                </div>

                <blockquote className="mt-2 break-words rounded border-l-4 border-slate-300 bg-slate-50 px-3 py-2 text-sm italic text-slate-700">
                  {report.komentarTekst}
                </blockquote>

                {report.opis && <p className="mt-2 text-sm text-slate-600"><span className="font-semibold">Napomena:</span> {report.opis}</p>}

                <div className="mt-3 flex flex-wrap gap-2">
                  <button
                    className="inline-flex items-center gap-1 rounded bg-brand px-3 py-1.5 text-xs font-bold text-white hover:opacity-90"
                    onClick={() => resolve(report.id, 'obrisi')}
                    type="button"
                  >
                    <Trash2 size={14} /> Obrisi komentar
                  </button>
                  <button
                    className="inline-flex items-center gap-1 rounded border border-slate-300 px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
                    onClick={() => resolve(report.id, 'odbaci')}
                    type="button"
                  >
                    <X size={14} /> Odbaci prijavu
                  </button>
                  <button
                    className="inline-flex items-center gap-1 rounded border border-slate-300 px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
                    onClick={() => moderateAuthor(report)}
                    type="button"
                  >
                    <UserCog size={14} /> Moderisi autora
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <UserManagement onModerated={afterModeration} />

      {authorTarget && (
        <ModerationModal
          currentState={authorTarget.state}
          onChanged={() => { setAuthorTarget(null); afterModeration(); }}
          onClose={() => setAuthorTarget(null)}
          target={{ id: authorTarget.id, username: authorTarget.username, role: authorTarget.role }}
        />
      )}
    </div>
  );
}

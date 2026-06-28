import { Megaphone, Pin, PinOff, Send, Trash2 } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiError';
import { panelApi } from '../../services/panelApi';
import type { StaffNotice } from '../../types/api';
import { RelativeTime } from '../RelativeTime';

export function StaffNotices() {
  const [notices, setNotices] = useState<StaffNotice[]>([]);
  const [tekst, setTekst] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function load() {
    try {
      setNotices(await panelApi.listNotices());
    } catch {
      /* tiha greska; sekcija ostaje prazna */
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!tekst.trim()) return;
    setPending(true);
    setError(null);
    try {
      await panelApi.createNotice(tekst.trim());
      setTekst('');
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Obavestenje nije sacuvano.'));
    } finally {
      setPending(false);
    }
  }

  async function togglePin(notice: StaffNotice) {
    setError(null);
    try {
      await (notice.pinovano ? panelApi.unpinNotice(notice.id) : panelApi.pinNotice(notice.id));
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Pin nije promenjen.'));
    }
  }

  async function remove(id: string) {
    setError(null);
    try {
      await panelApi.removeNotice(id);
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Obavestenje nije obrisano.'));
    }
  }

  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
        <Megaphone size={16} className="text-brand" />
        <h2 className="text-sm font-extrabold">Obavestenja za tim</h2>
      </div>

      <form className="flex gap-2 border-b border-slate-100 p-3" onSubmit={submit}>
        <textarea
          aria-label="Novo obavestenje"
          className="min-h-10 flex-1 resize-y rounded border border-slate-300 p-2 text-sm outline-none focus:border-brand"
          placeholder="Napisi uputstvo ili obavestenje kolegama..."
          value={tekst}
          onChange={(event) => setTekst(event.target.value)}
        />
        <button
          className="inline-flex h-10 shrink-0 items-center gap-1 self-end rounded bg-brand px-3 text-xs font-bold text-white disabled:opacity-50"
          disabled={pending}
          type="submit"
        >
          <Send size={14} /> Objavi
        </button>
      </form>

      {error && <p className="px-4 py-2 text-sm text-red-600">{error}</p>}

      {notices.length === 0 ? (
        <p className="px-4 py-6 text-center text-sm text-slate-400">Nema obavestenja.</p>
      ) : (
        <ul className="divide-y divide-slate-100">
          {notices.map((notice) => (
            <li key={notice.id} className={`p-3 ${notice.pinovano ? 'bg-amber-50' : ''}`}>
              <div className="flex items-start justify-between gap-2">
                <p className="min-w-0 whitespace-pre-wrap break-words text-sm text-slate-700">{notice.tekst}</p>
                <div className="flex shrink-0 gap-1">
                  <button
                    aria-label={notice.pinovano ? 'Otkaci' : 'Zakaci'}
                    className="rounded p-1 text-slate-500 hover:bg-slate-100 hover:text-brand"
                    onClick={() => togglePin(notice)}
                    title={notice.pinovano ? 'Otkaci' : 'Zakaci na vrh'}
                    type="button"
                  >
                    {notice.pinovano ? <PinOff size={14} /> : <Pin size={14} />}
                  </button>
                  <button
                    aria-label="Obrisi obavestenje"
                    className="rounded p-1 text-slate-500 hover:bg-slate-100 hover:text-red-600"
                    onClick={() => remove(notice.id)}
                    type="button"
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
              <p className="mt-1 flex items-center gap-2 text-[11px] text-slate-400">
                <span className="font-semibold text-slate-500">{notice.autorUsername}</span>
                <RelativeTime value={notice.datumKreiranja} />
                {notice.pinovano && <span className="font-bold text-amber-600">Pinovano</span>}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

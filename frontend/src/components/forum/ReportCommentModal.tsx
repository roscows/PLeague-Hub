import { Flag, X } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiError';
import { reportsApi } from '../../services/reportsApi';
import type { ReportCategory } from '../../types/api';

const categories: Array<{ value: ReportCategory; label: string }> = [
  { value: 'spam', label: 'Spam' },
  { value: 'uvrede', label: 'Uvrede / govor mrznje' },
  { value: 'offtopic', label: 'Off-topic' },
  { value: 'ostalo', label: 'Ostalo' }
];

interface ReportCommentModalProps {
  commentId: string;
  onClose: () => void;
  onReported: () => void;
}

export function ReportCommentModal({ commentId, onClose, onReported }: ReportCommentModalProps) {
  const [kategorija, setKategorija] = useState<ReportCategory>('spam');
  const [opis, setOpis] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      await reportsApi.create(commentId, { kategorija, opis: opis.trim() || undefined });
      onReported();
      onClose();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Prijava nije poslata.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/55 p-3" role="dialog" aria-modal="true" aria-labelledby="report-title">
      <form className="w-full max-w-md rounded-lg border border-slate-300 bg-white shadow-2xl" onSubmit={submit}>
        <header className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h2 className="flex items-center gap-2 text-lg font-extrabold" id="report-title">
            <Flag size={18} className="text-brand" /> Prijavi komentar
          </h2>
          <button aria-label="Zatvori" className="rounded p-1 text-slate-500 hover:bg-slate-100" onClick={onClose} type="button">
            <X size={18} />
          </button>
        </header>

        <div className="space-y-4 p-4">
          <fieldset>
            <legend className="text-xs font-extrabold uppercase text-slate-500">Razlog</legend>
            <div className="mt-2 grid grid-cols-2 gap-2">
              {categories.map((item) => (
                <button
                  key={item.value}
                  className={`rounded border px-3 py-2 text-sm font-bold ${kategorija === item.value ? 'border-brand bg-red-50 text-brand' : 'border-slate-300'}`}
                  onClick={() => setKategorija(item.value)}
                  type="button"
                >
                  {item.label}
                </button>
              ))}
            </div>
          </fieldset>

          <label className="block text-xs font-extrabold uppercase text-slate-500">
            Objasnjenje (opciono)
            <textarea
              aria-label="Objasnjenje"
              className="mt-2 min-h-20 w-full resize-y rounded border border-slate-300 p-3 text-sm font-normal normal-case text-slate-900 outline-none focus:border-brand"
              value={opis}
              onChange={(event) => setOpis(event.target.value)}
            />
          </label>

          {error && <p className="text-sm font-semibold text-red-600">{error}</p>}
        </div>

        <footer className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-4 py-3">
          <button className="rounded border border-slate-300 bg-white px-4 py-2 text-sm font-bold" onClick={onClose} type="button">Odustani</button>
          <button className="rounded bg-brand px-4 py-2 text-sm font-bold text-white disabled:opacity-50" disabled={pending} type="submit">Posalji prijavu</button>
        </footer>
      </form>
    </div>
  );
}

import { FormEvent, useState } from 'react';
import { Send, X } from 'lucide-react';

interface ForumComposerProps {
  onCancel: () => void;
  onSubmit: (naslov: string, sadrzaj: string) => Promise<void>;
}

export function ForumComposer({ onCancel, onSubmit }: ForumComposerProps) {
  const [naslov, setNaslov] = useState('');
  const [sadrzaj, setSadrzaj] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await onSubmit(naslov, sadrzaj);
    } catch (submissionError) {
      setError(submissionError instanceof Error ? submissionError.message : 'Tema nije objavljena.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="border-b border-slate-200 bg-slate-50 p-4" onSubmit={handleSubmit}>
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-extrabold text-slate-900">Nova tema</h2>
        <button aria-label="Zatvori formu" className="text-slate-500 hover:text-slate-900" onClick={onCancel} type="button">
          <X size={17} />
        </button>
      </div>
      <div className="mt-3 grid gap-3">
        <label className="grid gap-1 text-xs font-bold text-slate-600">
          Naslov teme
          <input
            className="rounded border border-slate-300 bg-white px-3 py-2 text-sm font-normal text-slate-900 outline-none focus:border-brand"
            maxLength={140}
            required
            value={naslov}
            onChange={(event) => setNaslov(event.target.value)}
          />
        </label>
        <label className="grid gap-1 text-xs font-bold text-slate-600">
          Sadrzaj teme
          <textarea
            className="min-h-28 rounded border border-slate-300 bg-white px-3 py-2 text-sm font-normal text-slate-900 outline-none focus:border-brand"
            maxLength={5000}
            required
            value={sadrzaj}
            onChange={(event) => setSadrzaj(event.target.value)}
          />
        </label>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          className="flex w-fit items-center gap-2 rounded bg-brand px-4 py-2 text-sm font-bold text-white disabled:opacity-60"
          disabled={submitting}
          type="submit"
        >
          <Send size={15} /> {submitting ? 'Objavljivanje...' : 'Objavi temu'}
        </button>
      </div>
    </form>
  );
}

import { FormEvent, useState } from 'react';
import { Send } from 'lucide-react';

interface ForumReplyFormProps {
  onCancel?: () => void;
  onSubmit: (text: string) => Promise<void>;
}

export function ForumReplyForm({ onCancel, onSubmit }: ForumReplyFormProps) {
  const [text, setText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await onSubmit(text);
      setText('');
      onCancel?.();
    } catch (submissionError) {
      setError(submissionError instanceof Error ? submissionError.message : 'Odgovor nije poslat.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="border-t border-slate-200 bg-slate-50 p-3" onSubmit={handleSubmit}>
      <label className="grid gap-1 text-xs font-bold text-slate-600">
        Tekst odgovora
        <textarea
          className="min-h-20 rounded border border-slate-300 bg-white px-3 py-2 text-sm font-normal text-slate-900 outline-none focus:border-brand"
          maxLength={3000}
          required
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
      </label>
      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
      <div className="mt-2 flex gap-2">
        <button className="flex items-center gap-2 rounded bg-brand px-3 py-2 text-xs font-bold text-white disabled:opacity-60" disabled={submitting} type="submit">
          <Send size={14} /> {submitting ? 'Slanje...' : 'Posalji odgovor'}
        </button>
        {onCancel && <button className="rounded border border-slate-300 px-3 py-2 text-xs font-bold text-slate-600" onClick={onCancel} type="button">Odustani</button>}
      </div>
    </form>
  );
}

import { X } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { getApiErrorMessage } from '../../services/apiError';
import { moderationApi } from '../../services/moderationApi';
import type { ModerationDuration, ModerationState, ModerationType, Role } from '../../types/api';

interface ModerationTarget {
  id: string;
  username: string;
  role: Role;
}

interface ModerationModalProps {
  target: ModerationTarget;
  currentState?: ModerationState | null;
  onClose: () => void;
  onChanged: () => void;
}

const durations: Array<{ value: ModerationDuration; label: string }> = [
  { value: '1h', label: '1 sat' },
  { value: '24h', label: '24 sata' },
  { value: '7d', label: '7 dana' },
  { value: '30d', label: '30 dana' },
  { value: 'permanent', label: 'Trajno' }
];

export function ModerationModal({ target, currentState, onClose, onChanged }: ModerationModalProps) {
  const [type, setType] = useState<ModerationType>('mute');
  const [duration, setDuration] = useState<ModerationDuration>('24h');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!reason.trim()) {
      setError('Razlog mere je obavezan.');
      return;
    }

    setPending(true);
    setError(null);
    try {
      await moderationApi.applyUserAction(target.id, { tip: type, trajanje: duration, razlog: reason.trim() });
      onChanged();
      onClose();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Mera nije sacuvana.'));
    } finally {
      setPending(false);
    }
  }

  async function revoke() {
    setPending(true);
    setError(null);
    try {
      await moderationApi.revokeUserAction(target.id);
      onChanged();
      onClose();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Mera nije ukinuta.'));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/55 p-3" role="dialog" aria-modal="true" aria-labelledby="moderation-title">
      <form className="max-h-[calc(100vh-24px)] w-full max-w-lg overflow-y-auto rounded-lg border border-slate-300 bg-white shadow-2xl" onSubmit={submit}>
        <header className="flex items-start justify-between border-b border-slate-200 px-4 py-3">
          <div>
            <h2 className="text-lg font-extrabold" id="moderation-title">{target.username}</h2>
            <p className="mt-0.5 text-xs text-slate-500">{target.role} - {currentState ? 'Aktivna mera' : 'Aktivan'}</p>
          </div>
          <button aria-label="Zatvori" className="rounded p-1 text-slate-500 hover:bg-slate-100" onClick={onClose} type="button"><X size={18} /></button>
        </header>

        <div className="space-y-4 p-4">
          {currentState && (
            <div className="rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              <p className="font-bold">Aktivna {currentState.tip}</p>
              <p className="mt-1">{currentState.razlog}</p>
              <button className="mt-3 rounded border border-amber-400 px-3 py-1.5 text-xs font-bold" disabled={pending} onClick={revoke} type="button">Ukini kaznu</button>
            </div>
          )}

          <fieldset>
            <legend className="text-xs font-extrabold uppercase text-slate-500">Vrsta mere</legend>
            <div className="mt-2 grid grid-cols-2 gap-2">
              {(['mute', 'suspenzija'] as ModerationType[]).map((value) => (
                <button key={value} className={`rounded border px-3 py-2 text-sm font-bold ${type === value ? 'border-brand bg-red-50 text-brand' : 'border-slate-300'}`} onClick={() => setType(value)} type="button">
                  {value === 'mute' ? 'Mute' : 'Suspenzija'}
                </button>
              ))}
            </div>
          </fieldset>

          <fieldset>
            <legend className="text-xs font-extrabold uppercase text-slate-500">Trajanje</legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {durations.map((item) => (
                <button key={item.value} className={`rounded border px-3 py-1.5 text-sm ${duration === item.value ? 'border-brand bg-red-50 font-bold text-brand' : 'border-slate-300'}`} onClick={() => setDuration(item.value)} type="button">
                  {item.label}
                </button>
              ))}
            </div>
          </fieldset>

          <label className="block text-xs font-extrabold uppercase text-slate-500">
            Razlog
            <textarea aria-label="Razlog" className="mt-2 min-h-24 w-full resize-y rounded border border-slate-300 p-3 text-sm font-normal normal-case text-slate-900 outline-none focus:border-brand" value={reason} onChange={(event) => setReason(event.target.value)} />
          </label>

          {error && <p className="text-sm font-semibold text-red-600">{error}</p>}
          <p className="rounded border border-slate-200 bg-slate-50 p-3 text-xs text-slate-500">Mera se evidentira uz moderatora, razlog i vreme. Korisnik ce videti razlog.</p>
        </div>

        <footer className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-4 py-3">
          <button className="rounded border border-slate-300 bg-white px-4 py-2 text-sm font-bold" onClick={onClose} type="button">Odustani</button>
          <button className="rounded bg-brand px-4 py-2 text-sm font-bold text-white disabled:opacity-50" disabled={pending} type="submit">Potvrdi {type}</button>
        </footer>
      </form>
    </div>
  );
}

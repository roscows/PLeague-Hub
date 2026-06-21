import { ShieldAlert } from 'lucide-react';
import type { ModerationState } from '../../types/api';

export function ModerationNotice({ state }: { state: ModerationState }) {
  const expiry = state.isticeAt
    ? new Date(state.isticeAt).toLocaleString('sr-RS', { dateStyle: 'medium', timeStyle: 'short' })
    : 'Trajno';

  return (
    <div className="border-b border-amber-300 bg-amber-50 text-amber-950">
      <div className="mx-auto flex max-w-[1440px] items-start gap-2 px-4 py-2 text-sm">
        <ShieldAlert className="mt-0.5 shrink-0" size={17} />
        <p><strong>Aktivna mera: {state.tip}.</strong> {state.razlog} · Istek: {expiry}</p>
      </div>
    </div>
  );
}

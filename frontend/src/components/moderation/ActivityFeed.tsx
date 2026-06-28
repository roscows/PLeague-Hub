import { History } from 'lucide-react';
import { useEffect, useState } from 'react';
import { panelApi } from '../../services/panelApi';
import type { ModerationActivity } from '../../types/api';
import { RelativeTime } from '../RelativeTime';

function measureLabel(tip: string | null): string {
  if (tip === 'mute') return 'mute';
  if (tip === 'suspenzija') return 'suspenziju';
  return tip ?? 'meru';
}

export function ActivityFeed() {
  const [items, setItems] = useState<ModerationActivity[]>([]);

  useEffect(() => {
    panelApi.listActivity(15).then(setItems).catch(() => setItems([]));
  }, []);

  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
        <History size={16} className="text-brand" />
        <h2 className="text-sm font-extrabold">Nedavna aktivnost</h2>
      </div>
      {items.length === 0 ? (
        <p className="px-4 py-6 text-center text-sm text-slate-400">Nema zabelezene aktivnosti.</p>
      ) : (
        <ul className="divide-y divide-slate-100">
          {items.map((item) => (
            <li key={item.id} className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5 px-4 py-2 text-sm">
              <span className="font-semibold text-slate-700">{item.moderatorUsername}</span>
              <span className="text-slate-500">{item.akcija === 'revoke' ? 'ukinuo meru korisniku' : `izrekao ${measureLabel(item.tipMere)} korisniku`}</span>
              <span className="font-semibold text-slate-700">{item.korisnikUsername}</span>
              {item.razlog && <span className="text-slate-400">— {item.razlog}</span>}
              <RelativeTime className="ml-auto text-[11px] text-slate-400" value={item.datum} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

import { Pin } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { ForumTopic } from '../../types/api';
import { formatRelativeTime } from '../../utils/relativeTime';

export function ForumTopicTable({ topics }: { topics: ForumTopic[] }) {
  return (
    <div>
      <div className="hidden grid-cols-[minmax(0,1fr)_90px_140px_170px] gap-3 border-b border-slate-300 bg-slate-100 px-4 py-2 text-xs font-extrabold uppercase text-slate-500 sm:grid">
        <span>Tema</span>
        <span className="text-center">Odgovori</span>
        <span>Autor</span>
        <span>Aktivnost</span>
      </div>
      <div className="divide-y divide-slate-200">
        {topics.map((topic) => (
          <Link
            aria-label={topic.naslov}
            className={`grid grid-cols-[minmax(0,1fr)_auto] gap-x-3 gap-y-1 px-4 py-3 transition hover:bg-red-50 sm:grid-cols-[minmax(0,1fr)_90px_140px_170px] sm:items-center ${topic.istaknut ? 'bg-slate-100' : 'bg-white'}`}
            key={topic.id}
            to={`/forum/${topic.id}`}
          >
            <span className="min-w-0">
              <span className="flex items-center gap-2 font-bold text-slate-900">
                {topic.istaknut && <Pin aria-label="Istaknuta tema" className="shrink-0 text-brand" size={14} />}
                <span className="truncate sm:whitespace-normal">{topic.naslov}</span>
              </span>
              <span className="mt-1 block text-xs text-slate-500 sm:hidden">
                {topic.autorUsername} · {formatRelativeTime(topic.poslednjaAktivnost)}
              </span>
            </span>
            <span className="self-center text-right text-sm font-extrabold text-slate-700 sm:text-center">{topic.brojOdgovora}</span>
            <span className="hidden truncate text-sm font-semibold text-slate-600 sm:block">{topic.autorUsername}</span>
            <span className="hidden text-xs text-slate-500 sm:block">
              <strong className="block truncate text-slate-700">{topic.poslednjiAutorUsername}</strong>
              {formatRelativeTime(topic.poslednjaAktivnost)}
            </span>
          </Link>
        ))}
      </div>
    </div>
  );
}

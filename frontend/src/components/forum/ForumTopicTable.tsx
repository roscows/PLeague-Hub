import { Pin } from 'lucide-react';
import { Link } from 'react-router-dom';
import { canModerateRole } from '../../services/authorization';
import type { ForumTopic, Role } from '../../types/api';
import { formatRelativeTime } from '../../utils/relativeTime';

interface ForumTopicTableProps {
  topics: ForumTopic[];
  currentRole?: Role;
  onTogglePin: (topic: ForumTopic) => void;
}

export function ForumTopicTable({ topics, currentRole, onTogglePin }: ForumTopicTableProps) {
  return (
    <div>
      <div className="hidden grid-cols-[minmax(0,1fr)_90px_140px_170px_36px] gap-3 border-b border-slate-300 bg-slate-100 px-4 py-2 text-xs font-extrabold uppercase text-slate-500 sm:grid">
        <span>Tema</span>
        <span className="text-center">Odgovori</span>
        <span>Autor</span>
        <span>Aktivnost</span>
        <span />
      </div>
      <div className="divide-y divide-slate-200">
        {topics.map((topic) => {
          const canPin = canModerateRole(currentRole, topic.autorUloga);
          return (
            <div
              className={`grid grid-cols-[minmax(0,1fr)_auto_auto] gap-x-3 gap-y-1 px-4 py-3 transition hover:bg-red-50 sm:grid-cols-[minmax(0,1fr)_90px_140px_170px_36px] sm:items-center ${topic.istaknut ? 'bg-slate-100' : 'bg-white'}`}
              key={topic.id}
            >
              <span className="min-w-0">
                <Link aria-label={topic.naslov} className="flex items-center gap-2 font-bold text-slate-900 hover:text-brand" to={`/forum/${topic.id}`}>
                  {topic.istaknut && <Pin aria-label="Istaknuta tema" className="shrink-0 text-brand" size={14} />}
                  <span className="truncate sm:whitespace-normal">{topic.naslov}</span>
                </Link>
                <span className="mt-1 block text-xs text-slate-500 sm:hidden">
                  {topic.autorUsername} - {formatRelativeTime(topic.poslednjaAktivnost)}
                </span>
              </span>
              <span className="self-center text-right text-sm font-extrabold text-slate-700 sm:text-center">{topic.brojOdgovora}</span>
              <span className="hidden truncate text-sm font-semibold text-slate-600 sm:block">{topic.autorUsername}</span>
              <span className="hidden text-xs text-slate-500 sm:block">
                <strong className="block truncate text-slate-700">{topic.poslednjiAutorUsername}</strong>
                {formatRelativeTime(topic.poslednjaAktivnost)}
              </span>
              {canPin ? (
                <button
                  aria-label={topic.istaknut ? 'Otkaci temu' : 'Pinuj temu'}
                  className={`grid size-8 place-items-center rounded hover:bg-white ${topic.istaknut ? 'text-brand' : 'text-slate-400'}`}
                  onClick={() => onTogglePin(topic)}
                  title={topic.istaknut ? 'Otkaci temu' : 'Pinuj temu'}
                  type="button"
                >
                  <Pin size={15} />
                </button>
              ) : <span />}
            </div>
          );
        })}
      </div>
    </div>
  );
}

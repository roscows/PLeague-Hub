import { Newspaper } from 'lucide-react';
import type { NewsItem } from '../../types/api';
import { NewsTimelineItem } from './NewsTimelineItem';

interface NewsTimelineProps {
  items: NewsItem[];
  loading: boolean;
  error: string | null;
  hasMore: boolean;
  loadingMore: boolean;
  onRetry: () => void;
  onLoadMore: () => void;
}

export function NewsTimeline({ items, loading, error, hasMore, loadingMore, onRetry, onLoadMore }: NewsTimelineProps) {
  if (loading) {
    return (
      <div aria-label="Ucitavanje vesti" className="divide-y divide-slate-200 border-x border-slate-200 bg-white">
        {[0, 1, 2, 3].map((item) => (
          <div className="grid animate-pulse grid-cols-[72px_1fr] gap-4 px-4 py-5 sm:grid-cols-[104px_1fr]" key={item}>
            <span className="h-3 rounded bg-slate-200" />
            <span className="space-y-3"><span className="block h-4 w-2/3 rounded bg-slate-200" /><span className="block h-3 rounded bg-slate-100" /></span>
          </div>
        ))}
      </div>
    );
  }

  if (error && items.length === 0) {
    return (
      <div className="border-x border-b border-slate-200 bg-white px-4 py-12 text-center">
        <p className="text-sm font-semibold text-red-700">{error}</p>
        <button className="mt-4 min-h-10 rounded bg-brand px-4 text-sm font-bold text-white" onClick={onRetry} type="button">Pokusaj ponovo</button>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="border-x border-b border-slate-200 bg-white px-4 py-14 text-center text-slate-500">
        <Newspaper className="mx-auto mb-3 text-slate-300" size={28} />
        <p className="text-sm font-semibold">Nema vesti za izabrane filtere.</p>
      </div>
    );
  }

  return (
    <div className="border-x border-b border-slate-200">
      {items.map((item) => <NewsTimelineItem item={item} key={item.id} />)}
      {error && <p className="bg-red-50 px-4 py-3 text-center text-sm font-semibold text-red-700">{error}</p>}
      {hasMore && (
        <div className="bg-white p-4 text-center">
          <button
            className="min-h-11 rounded border border-slate-300 bg-white px-5 text-sm font-extrabold text-slate-700 hover:border-brand hover:text-brand disabled:opacity-50"
            disabled={loadingMore}
            onClick={onLoadMore}
            type="button"
          >
            {loadingMore ? 'Ucitavanje...' : 'Ucitaj jos'}
          </button>
        </div>
      )}
    </div>
  );
}

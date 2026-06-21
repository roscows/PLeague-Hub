import { ExternalLink, MessageSquareText } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { NewsItem } from '../../types/api';
import { RelativeTime } from '../RelativeTime';
import { NewsBadge } from './NewsBadge';

const exactDate = new Intl.DateTimeFormat('sr-Latn-RS', {
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
});

export function NewsTimelineItem({ item }: { item: NewsItem }) {
  const sourceName = item.izvorNaziv ?? item.autorUsername ?? 'PLeague Hub';
  const origin = item.xEmbedUrl ? 'X objava' : item.uvozAutomatski ? 'RSS' : 'Redakcija';
  const published = new Date(item.publishedAt);

  return (
    <article className="group grid grid-cols-[72px_minmax(0,1fr)] border-b border-slate-200 bg-white px-3 py-4 transition-colors hover:bg-slate-50 sm:grid-cols-[104px_18px_minmax(0,1fr)] sm:px-4">
      <div className="pr-3 text-right">
        <RelativeTime className="block text-[11px] font-bold text-slate-500" value={item.publishedAt} />
        <time className="mt-1 block text-[10px] text-slate-400" dateTime={item.publishedAt} title={exactDate.format(published)}>
          {published.toLocaleTimeString('sr-Latn-RS', { hour: '2-digit', minute: '2-digit' })}
        </time>
      </div>
      <div aria-hidden="true" className="relative hidden sm:block">
        <span className="absolute left-1/2 top-0 h-full w-px -translate-x-1/2 bg-slate-200" />
        <span className="absolute left-1/2 top-1.5 size-2 -translate-x-1/2 rounded-full border-2 border-white bg-brand ring-1 ring-red-200" />
      </div>
      <div className="min-w-0 pl-2 sm:pl-4">
        <div className="flex flex-wrap items-center gap-2">
          <NewsBadge value={item.pouzdanost} />
          <span className="truncate text-[11px] font-bold uppercase text-slate-500">{sourceName}</span>
          <span className="text-[10px] font-semibold uppercase text-slate-400">{origin}</span>
        </div>
        <Link
          aria-label={item.naslov}
          className="mt-2 block text-[15px] font-extrabold leading-5 text-slate-950 outline-none group-hover:text-brand focus-visible:text-brand sm:text-base"
          to={`/news/${item.id}`}
        >
          {item.naslov}
        </Link>
        {item.sazetak && <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-600">{item.sazetak}</p>}
        <div className="mt-3 flex items-center gap-4 text-[11px] font-semibold text-slate-500">
          <span className="inline-flex items-center gap-1"><MessageSquareText size={13} />{item.brojKomentara}</span>
          {item.originalUrl && <span className="inline-flex items-center gap-1"><ExternalLink size={12} />Izvorni link</span>}
        </div>
      </div>
    </article>
  );
}

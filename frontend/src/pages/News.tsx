import { Newspaper } from 'lucide-react';
import { useEffect, useState } from 'react';
import { newsApi } from '../services/newsApi';
import type { Post } from '../types/api';

export function News() {
  const [news, setNews] = useState<Post[]>([]);

  useEffect(() => {
    newsApi.list().then(setNews);
  }, []);

  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-4">
        <Newspaper size={18} className="text-brand" />
        <h1 className="text-xl font-extrabold">Premier League vesti</h1>
      </div>
      <div className="divide-y divide-slate-100">
        {news.map((post) => (
          <article key={post.id} className="grid gap-3 px-4 py-5 hover:bg-slate-50 sm:grid-cols-[110px_1fr]">
            <p className="text-xs font-semibold text-slate-400">{new Date(post.datumKreiranja).toLocaleDateString('sr-RS')}</p>
            <div>
              <h2 className="text-base font-extrabold">{post.naslov}</h2>
              <p className="mt-2 text-sm leading-6 text-slate-600">{post.sadrzaj}</p>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

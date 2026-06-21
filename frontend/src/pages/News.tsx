import { Plus, Rss } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { NewsFilters } from '../components/news/NewsFilters';
import type { NewsCategoryFilter } from '../components/news/NewsFilters';
import { NewsTimeline } from '../components/news/NewsTimeline';
import { NewsEditor } from '../components/news/NewsEditor';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';
import { newsApi } from '../services/newsApi';
import type { NewsItem, NewsListQuery, NewsReliability } from '../types/api';

const PAGE_SIZE = 20;

export function News() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [items, setItems] = useState<NewsItem[]>([]);
  const [category, setCategory] = useState<NewsCategoryFilter>('sve');
  const [reliability, setReliability] = useState<NewsReliability | ''>('');
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const generation = useRef(0);
  const canEdit = user?.uloga === 'moderator' || user?.uloga === 'administrator';
  const editorOpen = canEdit && searchParams.get('compose') === '1';

  function closeEditor() {
    const next = new URLSearchParams(searchParams);
    next.delete('compose');
    setSearchParams(next, { replace: true });
  }

  function query(nextCursor?: string): NewsListQuery {
    return {
      ...(category !== 'sve' ? { kategorija: category } : {}),
      ...(reliability ? { pouzdanost: reliability } : {}),
      ...(nextCursor ? { cursor: nextCursor } : {}),
      limit: PAGE_SIZE
    };
  }

  useEffect(() => {
    const requestGeneration = ++generation.current;
    setLoading(true);
    setError(null);
    newsApi.list(query())
      .then((response) => {
        if (generation.current !== requestGeneration) return;
        setItems(response.items);
        setCursor(response.nextCursor);
      })
      .catch((requestError) => {
        if (generation.current === requestGeneration) {
          setItems([]);
          setError(getApiErrorMessage(requestError, 'Vesti trenutno nisu dostupne.'));
        }
      })
      .finally(() => {
        if (generation.current === requestGeneration) setLoading(false);
      });
  }, [category, reliability, reloadKey]);

  async function loadMore() {
    if (!cursor || loadingMore) return;
    const requestGeneration = generation.current;
    setLoadingMore(true);
    setError(null);
    try {
      const response = await newsApi.list(query(cursor));
      if (generation.current !== requestGeneration) return;
      setItems((current) => {
        const merged = new Map(current.map((item) => [item.id, item]));
        response.items.forEach((item) => merged.set(item.id, item));
        return [...merged.values()];
      });
      setCursor(response.nextCursor);
    } catch (requestError) {
      if (generation.current === requestGeneration) {
        setError(getApiErrorMessage(requestError, 'Sledece vesti nisu ucitane.'));
      }
    } finally {
      if (generation.current === requestGeneration) setLoadingMore(false);
    }
  }

  return (
    <section className="min-w-0">
      <header className="flex min-h-20 items-center justify-between gap-4 border-b-2 border-brand bg-white px-4 py-4">
        <div>
          <p className="text-[11px] font-extrabold uppercase text-brand">Vesti</p>
          <h1 className="mt-1 text-xl font-extrabold text-slate-950">Premier League uzivo</h1>
        </div>
        {canEdit && <div className="flex items-center gap-2">
          <Link aria-label="Upravljaj izvorima" className="grid size-10 place-items-center rounded border border-slate-300 text-slate-600 hover:border-brand hover:text-brand" title="Izvori" to="/news/sources"><Rss size={16} /></Link>
          <Link className="inline-flex min-h-10 shrink-0 items-center gap-2 rounded bg-brand px-3 text-sm font-extrabold text-white hover:bg-red-700" to="/news?compose=1">
            <Plus size={16} /><span className="hidden sm:inline">Objavi vest</span><span className="sm:hidden">Objavi</span>
          </Link>
        </div>}
      </header>
      {editorOpen && <NewsEditor onClose={closeEditor} onSaved={() => { closeEditor(); setReloadKey((value) => value + 1); }} />}
      <NewsFilters
        category={category}
        reliability={reliability}
        onCategoryChange={setCategory}
        onReliabilityChange={setReliability}
      />
      <NewsTimeline
        error={error}
        hasMore={Boolean(cursor)}
        items={items}
        loading={loading}
        loadingMore={loadingMore}
        onLoadMore={loadMore}
        onRetry={() => setReloadKey((value) => value + 1)}
      />
    </section>
  );
}

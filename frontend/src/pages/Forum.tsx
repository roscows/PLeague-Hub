import { ChevronLeft, ChevronRight, MessageCircleMore, Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { ForumTopicTable } from '../components/forum/ForumTopicTable';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';
import { forumApi } from '../services/forumApi';
import { moderationApi } from '../services/moderationApi';
import type { ForumTopic } from '../types/api';

const PAGE_SIZE = 20;

export function Forum() {
  const { user } = useAuth();
  const [topics, setTopics] = useState<ForumTopic[]>([]);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setPage(1);
      setSearch(searchInput.trim());
    }, 300);

    return () => window.clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    forumApi.list({ ...(search ? { search } : {}), page, pageSize: PAGE_SIZE })
      .then((response) => {
        if (!active) return;
        setTopics(response.items);
        setTotalPages(response.totalPages);
      })
      .catch((requestError) => {
        if (active) setError(getApiErrorMessage(requestError, 'Diskusije trenutno nisu dostupne.'));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [page, reloadKey, search]);

  async function toggleTopicPin(topic: ForumTopic) {
    const previousTopics = topics;
    setTopics((current) => current.map((item) => item.id === topic.id ? { ...item, istaknut: !item.istaknut } : item));
    try {
      if (topic.istaknut) await moderationApi.unpinPost(topic.id);
      else await moderationApi.pinPost(topic.id);
    } catch (requestError) {
      setTopics(previousTopics);
      setError(getApiErrorMessage(requestError, 'Pin teme nije sacuvan.'));
    }
  }

  return (
    <div className="space-y-3">
      <section className="overflow-hidden rounded border border-slate-200 bg-white shadow-sm">
        <header className="border-b border-slate-200 px-4 py-4">
          <div>
            <p className="text-xs font-extrabold uppercase text-brand">Forum</p>
            <h1 className="mt-1 text-xl font-extrabold text-slate-950">Premier League diskusije</h1>
          </div>
        </header>

        <div className="border-b border-slate-200 p-3">
          <label className="relative block max-w-md">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={16} />
            <input
              aria-label="Pretraga tema"
              className="w-full rounded border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-brand"
              placeholder="Pretrazi teme"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </label>
        </div>

        {loading ? (
          <div className="px-4 py-12 text-center text-sm text-slate-500">Ucitavanje diskusija...</div>
        ) : error ? (
          <div className="px-4 py-12 text-center">
            <p className="text-sm text-red-600">{error}</p>
            <button className="mt-3 text-sm font-bold text-brand" onClick={() => setReloadKey((value) => value + 1)} type="button">
              Pokusaj ponovo
            </button>
          </div>
        ) : topics.length === 0 ? (
          <div className="px-4 py-12 text-center text-slate-500">
            <MessageCircleMore className="mx-auto mb-2" size={24} />
            <p className="text-sm">Nema tema za izabranu pretragu.</p>
          </div>
        ) : (
          <ForumTopicTable topics={topics} currentRole={user?.uloga} onTogglePin={toggleTopicPin} />
        )}

        {totalPages > 1 && (
          <footer className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm">
            <button
              aria-label="Prethodna strana"
              className="rounded border border-slate-300 p-2 disabled:opacity-40"
              disabled={page === 1}
              onClick={() => setPage((value) => value - 1)}
              type="button"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="font-semibold text-slate-600">Strana {page} od {totalPages}</span>
            <button
              aria-label="Sledeca strana"
              className="rounded border border-slate-300 p-2 disabled:opacity-40"
              disabled={page === totalPages}
              onClick={() => setPage((value) => value + 1)}
              type="button"
            >
              <ChevronRight size={16} />
            </button>
          </footer>
        )}
      </section>
    </div>
  );
}

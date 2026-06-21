import { LoaderCircle, Search, Shield, UserRound, X } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { getApiErrorMessage } from '../services/apiError';
import { debounce } from '../services/debounce';
import { searchApi } from '../services/searchApi';
import type { SearchResult } from '../types/api';
import { TeamLogo } from './TeamLogo';

export function GlobalSearch() {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const requestIdRef = useRef(0);

  const runSearch = useCallback(async (value: string) => {
    const normalized = value.trim();

    if (normalized.length < 2) {
      setResults([]);
      setError(null);
      setIsLoading(false);
      return;
    }

    const requestId = ++requestIdRef.current;
    setIsLoading(true);
    setError(null);

    try {
      const searchResults = await searchApi.search(normalized);

      if (requestId === requestIdRef.current) {
        setResults(searchResults);
      }
    } catch (requestError) {
      if (requestId === requestIdRef.current) {
        setResults([]);
        setError(getApiErrorMessage(requestError, 'Pretraga trenutno nije dostupna.'));
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setIsLoading(false);
      }
    }
  }, []);

  const debouncedSearch = useMemo(() => debounce(runSearch, 250), [runSearch]);

  useEffect(() => {
    debouncedSearch(query);
    return debouncedSearch.cancel;
  }, [debouncedSearch, query]);

  useEffect(() => {
    function closeOnOutsideClick(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    document.addEventListener('mousedown', closeOnOutsideClick);
    return () => document.removeEventListener('mousedown', closeOnOutsideClick);
  }, []);

  function clearSearch() {
    requestIdRef.current += 1;
    debouncedSearch.cancel();
    setQuery('');
    setResults([]);
    setError(null);
    setIsLoading(false);
    setIsOpen(false);
  }

  const showDropdown = isOpen && query.trim().length >= 2;

  return (
    <div ref={containerRef} className="relative w-full">
      <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={17} />
      <input
        aria-label="Pretrazi igrace i timove"
        className="h-10 w-full rounded-md border border-white/10 bg-white/10 pl-10 pr-10 text-sm text-white outline-none placeholder:text-slate-400 focus:border-red-400 focus:bg-white/15"
        placeholder="Pretrazi igrace i timove..."
        value={query}
        onChange={(event) => {
          setQuery(event.target.value);
          setIsOpen(true);
        }}
        onFocus={() => setIsOpen(true)}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            setIsOpen(false);
          }
        }}
      />
      {isLoading ? (
        <LoaderCircle className="absolute right-3 top-1/2 -translate-y-1/2 animate-spin text-slate-400" size={17} />
      ) : query ? (
        <button
          aria-label="Obrisi pretragu"
          className="absolute right-2 top-1/2 grid size-7 -translate-y-1/2 place-items-center rounded text-slate-400 hover:bg-white/10 hover:text-white"
          onClick={clearSearch}
          type="button"
        >
          <X size={15} />
        </button>
      ) : null}

      {showDropdown && (
        <div className="absolute left-0 right-0 top-[calc(100%+8px)] z-50 overflow-hidden rounded-md border border-slate-200 bg-white text-slate-900 shadow-xl">
          {error ? (
            <p className="px-4 py-4 text-sm text-red-600">{error}</p>
          ) : !isLoading && results.length === 0 ? (
            <p className="px-4 py-4 text-sm text-slate-500">Nema igraca ili timova za "{query.trim()}".</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {results.map((result) => (
                <NavLink
                  key={`${result.type}-${result.id}`}
                  className="grid grid-cols-[36px_minmax(0,1fr)_auto] items-center gap-3 px-3 py-2.5 hover:bg-slate-50"
                  onClick={() => setIsOpen(false)}
                  to={`/stats?${result.type}Id=${result.id}`}
                >
                  <span className="grid size-9 place-items-center rounded-md bg-slate-100">
                    {result.type === 'team' ? (
                      <TeamLogo className="size-7" logoUrl={result.imageUrl} name={result.name} />
                    ) : result.imageUrl ? (
                      <img className="size-7 object-contain" src={result.imageUrl} alt="" />
                    ) : result.type === 'player' ? (
                      <UserRound size={18} className="text-slate-500" />
                    ) : (
                      <Shield size={18} className="text-slate-500" />
                    )}
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-bold">{result.name}</span>
                    <span className="block truncate text-xs text-slate-500">{result.subtitle}</span>
                  </span>
                  <span className="text-[10px] font-bold uppercase text-slate-400">
                    {result.type === 'player' ? 'Igrac' : 'Tim'}
                  </span>
                </NavLink>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

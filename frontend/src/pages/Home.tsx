import { CalendarDays, ChevronLeft, ChevronRight, CircleAlert, Newspaper, Radio, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { MatchRow } from '../components/MatchRow';
import { TeamLogo } from '../components/TeamLogo';
import { healthApi } from '../services/healthApi';
import { matchesApi } from '../services/matchesApi';
import { newsApi } from '../services/newsApi';
import { teamsApi } from '../services/teamsApi';
import type { HealthResponse, Match, NewsItem, Team } from '../types/api';

export function Home() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [matches, setMatches] = useState<Match[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [news, setNews] = useState<NewsItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      healthApi.get(),
      matchesApi.list(),
      teamsApi.list(),
      newsApi.list()
    ])
      .then(([healthData, matchesData, teamsData, newsData]) => {
        setHealth(healthData);
        setMatches(matchesData);
        setTeams(teamsData);
        setNews(newsData.items.slice(0, 4));
      })
      .catch(() => setError('Backend nije dostupan. Pokreni MongoDB i .NET API na portu 5000.'))
      .finally(() => setIsLoading(false));
  }, []);

  const teamMap = useMemo(() => new Map(teams.map((team) => [team.id, team])), [teams]);

  if (isLoading) {
    return <LoadingPanel />;
  }

  if (error) {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-red-200 bg-white p-5 text-sm text-red-700 shadow-sm">
        <CircleAlert className="mt-0.5 shrink-0" size={19} />
        <div>
          <p className="font-bold">Nije moguce ucitati podatke</p>
          <p className="mt-1 text-red-600">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <section className="flex items-center justify-between rounded-lg border border-slate-200 bg-white px-3 py-2 shadow-sm">
        <button className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-slate-100" title="Prethodni dan">
          <ChevronLeft size={18} />
        </button>
        <div className="flex items-center gap-2 text-sm font-bold">
          <CalendarDays size={17} className="text-brand" />
          Danas, {new Date().toLocaleDateString('sr-RS', { day: '2-digit', month: 'short' })}
        </div>
        <button className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-slate-100" title="Sledeci dan">
          <ChevronRight size={18} />
        </button>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between bg-ink px-4 py-3 text-white">
          <div className="flex items-center gap-3">
            <span className="grid size-8 place-items-center rounded-md bg-white/10">
              <Trophy size={17} className="text-red-400" />
            </span>
            <div>
              <p className="text-[10px] font-bold uppercase text-slate-400">Engleska</p>
              <h2 className="text-sm font-extrabold">Premier League</h2>
            </div>
          </div>
          <div className="ml-2 flex shrink-0 items-center gap-2 text-[11px] font-semibold text-emerald-300">
            <span className="size-2 rounded-full bg-emerald-400" />
            <span className="hidden sm:inline">{health?.status === 'healthy' ? 'Podaci uzivo' : 'Offline'}</span>
          </div>
        </div>

        {matches.length ? (
          matches.slice(0, 8).map((match) => <MatchRow key={match.id} match={match} teams={teamMap} />)
        ) : (
          <p className="p-6 text-center text-sm text-slate-500">Nema utakmica za prikaz.</p>
        )}
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
          <div className="flex items-center gap-2">
            <Newspaper size={17} className="text-brand" />
            <h2 className="text-sm font-extrabold">Najnovije vesti</h2>
          </div>
          <span className="hidden items-center gap-1 text-[10px] font-bold uppercase text-brand sm:flex">
            <Radio size={13} /> PLeague feed
          </span>
        </div>
        <div className="divide-y divide-slate-100">
          {news.map((post) => (
            <article key={post.id} className="grid gap-2 px-4 py-4 hover:bg-slate-50 sm:grid-cols-[120px_1fr]">
              <p className="text-xs font-semibold text-slate-400">
                {new Date(post.publishedAt).toLocaleDateString('sr-RS')}
              </p>
              <div className="min-w-0">
                <h3 className="break-words text-sm font-bold">{post.naslov}</h3>
                <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-600">{post.sazetak}</p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm xl:hidden">
        <div className="border-b border-slate-100 px-4 py-3 text-sm font-extrabold">Tabela</div>
        {teams.map((team) => (
          <div key={team.id} className="grid grid-cols-[28px_32px_1fr_40px] items-center gap-2 border-b border-slate-100 px-4 py-2.5 last:border-0">
            <span className="text-xs font-bold text-slate-400">{team.pozicija}</span>
            <TeamLogo className="size-6" logoUrl={team.logoUrl} name={team.naziv} />
            <span className="truncate text-sm font-semibold">{team.naziv}</span>
            <span className="text-right text-sm font-black">{team.bodovi}</span>
          </div>
        ))}
      </section>
    </div>
  );
}

function LoadingPanel() {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div className="h-4 w-36 animate-pulse rounded bg-slate-200" />
      <div className="mt-5 space-y-3">
        {[1, 2, 3, 4].map((item) => (
          <div key={item} className="h-14 animate-pulse rounded bg-slate-100" />
        ))}
      </div>
    </div>
  );
}

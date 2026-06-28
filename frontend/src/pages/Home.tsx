import { CircleAlert, ListOrdered, Newspaper, Target, Trophy } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { MatchRow } from '../components/MatchRow';
import { TeamLogo } from '../components/TeamLogo';
import { matchesApi } from '../services/matchesApi';
import { newsApi } from '../services/newsApi';
import { playerStatsApi } from '../services/playerStatsApi';
import { standingsApi } from '../services/standingsApi';
import { teamsApi } from '../services/teamsApi';
import type { Match, NewsItem, PlayerStat, StandingRow, Team } from '../types/api';

interface Round {
  season: string;
  gw: number;
}

function pickLatestRound(matches: Match[]): Round | null {
  const finished = matches.filter(
    (match) => match.status === 'zavrsena' && match.golDomacin !== null && match.golGost !== null
  );

  if (finished.length > 0) {
    const latest = finished.reduce((best, match) =>
      new Date(match.datum).getTime() > new Date(best.datum).getTime() ? match : best
    );
    return { season: latest.sezona, gw: latest.kolo };
  }

  const seasons = [...new Set(matches.map((match) => match.sezona))].sort().reverse();
  const season = seasons[0];
  if (!season) {
    return null;
  }
  const gws = matches.filter((match) => match.sezona === season).map((match) => match.kolo);
  return gws.length > 0 ? { season, gw: Math.min(...gws) } : null;
}

export function Home() {
  const [matches, setMatches] = useState<Match[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [news, setNews] = useState<NewsItem[]>([]);
  const [standings, setStandings] = useState<StandingRow[]>([]);
  const [scorers, setScorers] = useState<PlayerStat[]>([]);
  const [round, setRound] = useState<Round | null>(null);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [matchesData, teamsData, newsData] = await Promise.all([
          matchesApi.list(),
          teamsApi.list(),
          newsApi.list()
        ]);
        if (cancelled) return;

        setMatches(matchesData);
        setTeams(teamsData);
        setNews(newsData.items.slice(0, 5));

        const latest = pickLatestRound(matchesData);
        setRound(latest);

        if (latest) {
          const [standingsData, scorersData] = await Promise.all([
            standingsApi.getStandings(latest.season),
            playerStatsApi.get(latest.season)
          ]);
          if (cancelled) return;
          setStandings(standingsData.slice(0, 6));
          setScorers(scorersData.slice(0, 5));
        }

        setStatus('ready');
      } catch {
        if (!cancelled) setStatus('error');
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  const teamMap = new Map(teams.map((team) => [team.id, team]));
  const roundMatches = round
    ? matches
        .filter((match) => match.sezona === round.season && match.kolo === round.gw)
        .sort((a, b) => new Date(a.datum).getTime() - new Date(b.datum).getTime())
    : [];

  if (status === 'loading') {
    return <LoadingPanel />;
  }

  if (status === 'error') {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-red-200 bg-white p-5 text-sm text-red-700 shadow-sm">
        <CircleAlert className="mt-0.5 shrink-0" size={19} />
        <div>
          <p className="font-bold">Nije moguce ucitati podatke</p>
          <p className="mt-1 text-red-600">Pokreni MongoDB i .NET API na portu 5000.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
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
          {round && (
            <span className="text-[11px] font-bold uppercase text-slate-300">
              GW{round.gw} · {round.season}
            </span>
          )}
        </div>

        {roundMatches.length > 0 ? (
          roundMatches.map((match) => <MatchRow key={match.id} match={match} teams={teamMap} />)
        ) : (
          <p className="p-6 text-center text-sm text-slate-500">Nema utakmica za prikaz.</p>
        )}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <CardHeader icon={<ListOrdered size={16} className="text-brand" />} title="Tabela" linkTo="/tabela" linkText="Cela tabela" />
          {standings.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-slate-400">Tabela nije dostupna.</p>
          ) : (
            <ul className="divide-y divide-slate-100">
              {standings.map((team) => (
                <li key={team.position}>
                  <Link
                    to={team.providerId > 0 ? `/klub/${team.providerId}` : '/tabela'}
                    className="grid grid-cols-[24px_28px_1fr_auto] items-center gap-2 px-4 py-2.5 hover:bg-slate-50"
                  >
                    <span className="text-xs font-bold text-slate-400">{team.position}</span>
                    <TeamLogo className="size-6" logoUrl={team.logoUrl} name={team.naziv} />
                    <span className="truncate text-sm font-semibold">{team.naziv}</span>
                    <span className="text-sm font-black">{team.bodovi}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <CardHeader icon={<Target size={16} className="text-brand" />} title="Najbolji strelci" linkTo="/stats" linkText="Sve" />
          {scorers.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-slate-400">Nema statistike.</p>
          ) : (
            <ul className="divide-y divide-slate-100">
              {scorers.map((player, index) => (
                <li key={player.providerId}>
                  <Link
                    to={`/igrac/${player.providerId}`}
                    className="grid grid-cols-[24px_1fr_auto] items-center gap-2 px-4 py-2.5 hover:bg-slate-50"
                  >
                    <span className="text-xs font-bold text-slate-400">{index + 1}</span>
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-semibold">{player.ime}</span>
                      <span className="block truncate text-[11px] text-slate-400">{player.teamNaziv}</span>
                    </span>
                    <span className="text-sm font-black">{player.golovi}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <CardHeader icon={<Newspaper size={16} className="text-brand" />} title="Najnovije vesti" linkTo="/news" linkText="Sve vesti" />
        {news.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-400">Nema vesti.</p>
        ) : (
          <div className="divide-y divide-slate-100">
            {news.map((post) => (
              <Link
                key={post.id}
                to={`/news/${post.id}`}
                className="grid gap-2 px-4 py-4 hover:bg-slate-50 sm:grid-cols-[110px_1fr]"
              >
                <p className="text-xs font-semibold text-slate-400">
                  {new Date(post.publishedAt).toLocaleDateString('sr-RS')}
                </p>
                <div className="min-w-0">
                  <h3 className="break-words text-sm font-bold">{post.naslov}</h3>
                  <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-600">{post.sazetak}</p>
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function CardHeader({ icon, title, linkTo, linkText }: { icon: React.ReactNode; title: string; linkTo: string; linkText: string }) {
  return (
    <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
      <div className="flex items-center gap-2">
        {icon}
        <h2 className="text-sm font-extrabold">{title}</h2>
      </div>
      <Link to={linkTo} className="text-[11px] font-bold uppercase text-brand hover:underline">
        {linkText}
      </Link>
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

import { useEffect, useRef, useState } from 'react';
import {
  BarChart3,
  CalendarDays,
  Home,
  LogIn,
  LogOut,
  MessagesSquare,
  Newspaper,
  Star,
  Trophy
} from 'lucide-react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { teamsApi } from '../services/teamsApi';
import type { Team } from '../types/api';
import { GlobalSearch } from './GlobalSearch';
import { FavoriteTeamMenu } from './FavoriteTeamMenu';
import { TeamLogo } from './TeamLogo';
import { ModerationNotice } from './forum/ModerationNotice';

const navItems = [
  { to: '/', label: 'Pocetna', icon: Home },
  { to: '/results', label: 'Rezultati', icon: CalendarDays },
  { to: '/stats', label: 'Statistike', icon: BarChart3 },
  { to: '/news', label: 'Vesti', icon: Newspaper },
  { to: '/forum', label: 'Forum', icon: MessagesSquare }
];

function accountSubtitle(role: string | undefined, registeredAt: string | undefined) {
  if (role === 'administrator') return 'Administrator';
  if (role === 'moderator') return 'Moderator';
  if (!registeredAt) return '';

  const date = new Date(registeredAt);
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  return `Clan od ${day}.${month}.${date.getFullYear()}.`;
}

export function Layout() {
  const location = useLocation();
  const { user, isAuthenticated, logout } = useAuth();
  const [teams, setTeams] = useState<Team[]>([]);
  const activeMobileNavRef = useRef<HTMLAnchorElement>(null);
  const selectedTeam = teams.find((team) => team.id === user?.favoritniTimovi[0]);

  useEffect(() => {
    teamsApi.list().then(setTeams).catch(() => setTeams([]));
  }, []);

  useEffect(() => {
    activeMobileNavRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'nearest', inline: 'center' });
  }, [location.pathname]);

  return (
    <div className="min-h-screen overflow-x-hidden bg-surface text-slate-900">
      <header className="bg-ink text-white shadow-sm">
        <div className="mx-auto grid min-h-16 max-w-[1440px] grid-cols-[minmax(0,1fr)_auto] items-center gap-3 px-4 py-3 md:grid-cols-[1fr_minmax(280px,560px)_1fr] md:gap-6 md:py-2">
          <NavLink className="flex min-w-0 items-center gap-3 justify-self-start" to="/">
            <span className="grid size-9 place-items-center rounded-md bg-brand font-black italic">P</span>
            <span>
              <span className="block text-lg font-extrabold leading-none">PLeague Hub</span>
              <span className="mt-1 block text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                Premier League centar
              </span>
            </span>
          </NavLink>

          <div className="order-3 col-span-2 w-full md:order-none md:col-span-1 md:col-start-2 md:row-start-1">
            <GlobalSearch />
          </div>

          <div className="flex items-center gap-2 justify-self-end md:col-start-3 md:row-start-1">
            {isAuthenticated ? (
              <>
                <FavoriteTeamMenu selectedTeam={selectedTeam} teams={teams} />
                <div className="hidden min-[480px]:block text-right">
                  <p className="text-sm font-semibold">{user?.username}</p>
                  <p className="text-[10px] text-slate-400">{accountSubtitle(user?.uloga, user?.datumReg)}</p>
                </div>
                <button
                  className="grid size-9 place-items-center rounded-md bg-white/10 hover:bg-white/15"
                  onClick={logout}
                  title="Odjava"
                >
                  <LogOut size={17} />
                </button>
              </>
            ) : (
              <NavLink className="flex items-center gap-2 rounded-md bg-brand p-2.5 text-sm font-bold sm:px-3 sm:py-2" to="/login">
                <LogIn size={16} />
                <span className="hidden sm:inline">Login</span>
              </NavLink>
            )}
          </div>
        </div>
      </header>

      {user?.aktivnaModeracija && <ModerationNotice state={user.aktivnaModeracija} />}

      <div className="w-full max-w-full overflow-hidden border-b border-slate-200 bg-white md:hidden">
        <nav className="flex w-full max-w-full gap-1 overflow-x-auto px-3 py-2">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              ref={location.pathname === to || (to !== '/' && location.pathname.startsWith(`${to}/`)) ? activeMobileNavRef : undefined}
              to={to}
              className={({ isActive }) =>
                `flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-xs font-semibold ${
                  isActive ? 'bg-brand text-white' : 'text-slate-600'
                }`
              }
            >
              <Icon size={15} />
              {label}
            </NavLink>
          ))}
        </nav>
      </div>

      <div className="mx-auto grid max-w-[1440px] grid-cols-[minmax(0,1fr)] gap-4 px-3 py-4 md:grid-cols-[210px_minmax(0,1fr)] xl:grid-cols-[210px_minmax(0,1fr)_270px]">
        <aside className="hidden md:block">
          <nav className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
            <p className="border-b border-slate-100 px-4 py-3 text-[11px] font-bold uppercase text-slate-400">Meni</p>
            {navItems.map(({ to, label, icon: Icon }) => (
              <NavLink
                key={to}
                to={to}
                className={({ isActive }) =>
                  `flex items-center gap-3 border-l-4 px-4 py-3 text-sm font-semibold transition ${
                    isActive
                      ? 'border-brand bg-red-50 text-brand'
                      : 'border-transparent text-slate-600 hover:bg-slate-50 hover:text-slate-900'
                  }`
                }
              >
                <Icon size={17} />
                {label}
              </NavLink>
            ))}
          </nav>
        </aside>

        <main className="min-w-0">
          <Outlet />
        </main>

        <aside className="hidden xl:block">
          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
              <div className="flex items-center gap-2">
                <Trophy size={17} className="text-brand" />
                <h2 className="text-sm font-bold">Premier League</h2>
              </div>
              <Star size={16} className="text-slate-300" />
            </div>
            <div className="px-3 py-2">
              {teams.slice(0, 6).map((team) => (
                <div key={team.id} className="grid grid-cols-[24px_28px_1fr_32px] items-center gap-2 border-b border-slate-100 py-2.5 last:border-0">
                  <span className="text-center text-xs font-bold text-slate-400">{team.pozicija}</span>
                  <TeamLogo className="size-6" logoUrl={team.logoUrl} name={team.naziv} />
                  <span className="truncate text-xs font-semibold">{team.naziv}</span>
                  <span className="text-right text-xs font-black">{team.bodovi}</span>
                </div>
              ))}
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}

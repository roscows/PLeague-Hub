export type Role = 'gost' | 'registrovani' | 'moderator' | 'administrator';

export interface AuthResponse {
  userId: string;
  username: string;
  email: string;
  uloga: Role;
  token: string;
  expiresAt: string;
}

export interface UserProfile {
  userId: string;
  username: string;
  email: string;
  uloga: Role;
  aktivan: boolean;
  datumReg: string;
  favoritniTimovi: string[];
}

export interface Team {
  id: string;
  providerId?: number | null;
  naziv: string;
  skracenica: string;
  stadion: string;
  osnovan: number;
  logoUrl: string;
  bodovi: number;
  pozicija: number;
}

export interface Player {
  id: string;
  teamId: string;
  ime: string;
  prezime: string;
  pozicija: string;
  nacionalnost: string;
  golovi: number;
  asistencije: number;
  ocena: number;
}

export interface Match {
  id: string;
  domacinId: string;
  gostId: string;
  datum: string;
  kolo: number;
  sezona: string;
  golDomacin: number | null;
  golGost: number | null;
  status: string;
}

export interface Post {
  id: string;
  autorId: string;
  naslov: string;
  sadrzaj: string;
  tip: 'vest' | 'diskusija';
  datumKreiranja: string;
  obrisan: boolean;
}

export interface Comment {
  id: string;
  postId: string;
  autorId: string;
  tekst: string;
  datumKreiranja: string;
  obrisan: boolean;
}

export interface Statistic {
  id: string;
  matchId: string;
  playerId: string;
  golovi: number;
  asistencije: number;
  kartoni: number;
  minutiIgre: number;
  ocena: number;
}

export interface HealthResponse {
  status: string;
  service: string;
  environment: string;
  checkedAtUtc: string;
}

export interface LoginRequest {
  emailOrUsername: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface DiscussionRequest {
  naslov: string;
  sadrzaj: string;
}

export interface CommentRequest {
  tekst: string;
}

export interface PlayerFilters {
  search?: string;
  teamId?: string;
}

export interface MatchFilters {
  status?: string;
  season?: string;
  teamId?: string;
}

export interface StatisticFilters {
  matchId?: string;
  playerId?: string;
}

export type TeamWriteRequest = Omit<Team, 'id'>;
export type PlayerWriteRequest = Omit<Player, 'id'>;
export type MatchCreateRequest = Omit<Match, 'id'>;
export type MatchUpdateRequest = Omit<Match, 'id' | 'domacinId' | 'gostId'>;
export type StatisticWriteRequest = Omit<Statistic, 'id'>;

export interface SearchResult {
  id: string;
  type: 'player' | 'team';
  name: string;
  subtitle: string;
  imageUrl: string;
}

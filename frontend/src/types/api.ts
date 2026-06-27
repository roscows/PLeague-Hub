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
  aktivnaModeracija?: ModerationState | null;
}

export type ModerationType = 'mute' | 'suspenzija';
export type ModerationDuration = '1h' | '24h' | '7d' | '30d' | 'permanent';

export interface ModerationState {
  tip: ModerationType;
  razlog: string;
  pocetak: string;
  isticeAt: string | null;
  moderatorId: string;
}

export interface ModerationActionRequest {
  tip: ModerationType;
  trajanje: ModerationDuration;
  razlog: string;
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
  zavrsenaAt?: string | null;
  providerId?: number | null;
}

export interface MatchTeamInfo {
  providerId: number;
  naziv: string;
  skracenica: string;
  logoUrl: string;
}

export interface MatchHeader {
  domacin: MatchTeamInfo;
  gost: MatchTeamInfo;
  golDomacin: number | null;
  golGost: number | null;
  kolo: number;
  sezona: string;
  status: string;
  datum: string;
}

export interface StatItem {
  naziv: string;
  domacin: string;
  gost: string;
}

export interface Incident {
  tip: string;
  minut: number;
  domacin: boolean;
  tekst: string;
}

export interface LineupPlayer {
  ime: string;
  broj: number;
  zamena: boolean;
  pozicija: string;
}

export interface LineupTeam {
  formacija: string;
  igraci: LineupPlayer[];
}

export interface Lineups {
  potvrdjeno: boolean;
  domacin: LineupTeam;
  gost: LineupTeam;
}

export interface MatchDetail {
  header: MatchHeader;
  statistics: StatItem[];
  incidents: Incident[];
  lineups: Lineups | null;
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

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface ForumTopic {
  id: string;
  naslov: string;
  autorId: string;
  autorUsername: string;
  autorUloga: Role;
  brojOdgovora: number;
  datumKreiranja: string;
  poslednjaAktivnost: string;
  poslednjiAutorUsername: string;
  istaknut: boolean;
}

export interface ForumDiscussion {
  id: string;
  naslov: string;
  sadrzaj: string;
  autorId: string;
  autorUsername: string;
  autorUloga: Role;
  datumKreiranja: string;
  istaknut: boolean;
}

export type CommentVoteValue = 1 | -1;

export interface FavoriteTeamSummary {
  id: string;
  naziv: string;
  logoUrl: string;
}

export interface ForumComment {
  id: string;
  postId: string;
  parentCommentId: string | null;
  autorId: string;
  autorUsername: string;
  autorUloga: Role;
  tekst: string;
  datumKreiranja: string;
  obrisan: boolean;
  broj: number;
  lajkovi: number;
  dislajkovi: number;
  trenutniGlas: CommentVoteValue | null;
  istaknut: boolean;
  istaknutAt: string | null;
  istakaoId: string | null;
  autorFavoritniTim?: FavoriteTeamSummary | null;
}

export interface ForumCommentNode extends ForumComment {
  children: ForumCommentNode[];
  depth: number;
}

export interface ForumVoteResponse {
  commentId: string;
  lajkovi: number;
  dislajkovi: number;
  trenutniGlas: CommentVoteValue | null;
}

export interface ForumListQuery {
  search?: string;
  page?: number;
  pageSize?: number;
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
  parentCommentId?: string;
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

export type NewsCategory = 'premier_league' | 'transferi' | 'fpl' | 'klubovi';
export type NewsReliability = 'zvanicno' | 'pouzdan_izvor' | 'glasina' | 'fpl_analiza';

export interface NewsItem {
  id: string;
  naslov: string;
  sazetak: string;
  kategorija: NewsCategory;
  pouzdanost: NewsReliability;
  sourceId: string | null;
  izvorNaziv: string | null;
  autorId: string | null;
  autorUsername: string | null;
  originalUrl: string | null;
  imageUrl: string | null;
  xEmbedUrl: string | null;
  publishedAt: string;
  fetchedAt: string | null;
  uvozAutomatski: boolean;
  brojKomentara: number;
}

export interface NewsDetail extends Omit<NewsItem, 'sazetak'> {
  sadrzaj: string;
  externalAuthor: string | null;
  updatedAt: string | null;
}

export interface NewsTimelineResponse {
  items: NewsItem[];
  nextCursor: string | null;
}

export interface NewsListQuery {
  kategorija?: NewsCategory;
  pouzdanost?: NewsReliability;
  sourceId?: string;
  preDatuma?: string;
  cursor?: string;
  limit?: number;
}

export interface CreateNewsArticleRequest {
  naslov: string;
  sadrzaj: string;
  kategorija: NewsCategory;
  pouzdanost: NewsReliability;
  imageUrl?: string;
  originalUrl?: string;
}

export interface CreateXNewsRequest {
  naslov: string;
  xUrl: string;
  kategorija: NewsCategory;
  pouzdanost: NewsReliability;
}

export interface UpdateNewsRequest {
  naslov?: string;
  sadrzaj?: string;
  kategorija?: NewsCategory;
  pouzdanost?: NewsReliability;
  imageUrl?: string;
  originalUrl?: string;
}

export interface NewsSource {
  id: string;
  naziv: string;
  feedUrl: string;
  siteUrl: string;
  tip: 'rss';
  podrazumevanaKategorija: NewsCategory;
  podrazumevanaPouzdanost: NewsReliability;
  ukljuceniPojmovi: string[];
  iskljuceniPojmovi: string[];
  aktivan: boolean;
  pauziranRazlog: string | null;
  uzastopneGreske: number;
  poslednjaProveraAt: string | null;
  poslednjiUspehAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface NewsSourceRequest {
  naziv: string;
  feedUrl: string;
  siteUrl: string;
  podrazumevanaKategorija: NewsCategory;
  podrazumevanaPouzdanost: NewsReliability;
  ukljuceniPojmovi: string[];
  iskljuceniPojmovi: string[];
  aktivan: boolean;
}

export interface Season {
  season: string;
}

export interface PlayerStat {
  position: number;
  providerId: number;
  ime: string;
  teamProviderId: number;
  teamNaziv: string;
  teamLogoUrl: string;
  golovi: number;
  asistencije: number;
  odigrano: number;
}

export interface PlayerSeasonLine {
  sezona: string;
  teamNaziv: string;
  teamProviderId: number;
  golovi: number;
  asistencije: number;
  odigrano: number;
}

export interface PlayerProfile {
  providerId: number;
  ime: string;
  pozicija: string;
  drzava: string;
  visina: number;
  godine: number | null;
  klubNaziv: string;
  klubProviderId: number;
  fotoUrl: string;
  sezone: PlayerSeasonLine[];
}

export interface ClubMatch {
  sezona: string;
  datum: string;
  protivnik: string;
  protivnikLogo: string;
  domaci: boolean;
  golMi: number | null;
  golProtivnik: number | null;
  ishod: string;
}

export interface ClubRoster {
  providerId: number;
  ime: string;
  pozicija: string;
  broj: number;
  drzava: string;
}

export interface ClubProfile {
  providerId: number;
  naziv: string;
  logoUrl: string;
  stadion: string;
  osnovan: number;
  trener: string;
  drzava: string;
  pozicija: number;
  sezona: string;
  forma: string[];
  poslednjiMecevi: ClubMatch[];
  roster: ClubRoster[];
}

export interface StandingRow {
  position: number;
  providerId: number;
  naziv: string;
  skracenica: string;
  logoUrl: string;
  odigrano: number;
  pobede: number;
  nereseno: number;
  porazi: number;
  datiGolovi: number;
  primljeniGolovi: number;
  golRazlika: number;
  bodovi: number;
}

export interface NewsSourceSyncResponse {
  sourceId: string;
  success: boolean;
  notModified: boolean;
  created: number;
  duplicates: number;
  promoted: number;
  skipped: number;
  error: string | null;
}

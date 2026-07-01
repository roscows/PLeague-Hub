import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { authApi } from '../services/authApi';
import { hasRequiredRole } from '../services/authorization';
import { subscribeUnauthorized } from '../services/authEvents';
import { clearSession, readSession, saveSession } from '../services/session';
import { usersApi } from '../services/usersApi';
import type { RegisterRequest, Role, UserProfile } from '../types/api';

interface AuthContextValue {
  token: string | null;
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (emailOrUsername: string, password: string) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  logout: () => void;
  refreshProfile: () => Promise<void>;
  hasRole: (...roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() => readSession()?.token ?? null);
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(Boolean(token));

  const logout = useCallback(() => {
    clearSession();
    setToken(null);
    setUser(null);
  }, []);

  const refreshProfile = useCallback(async () => {
    setUser(await usersApi.getMe());
  }, []);

  const finishAuthentication = useCallback(async (authToken: string, expiresAt: string) => {
    saveSession({ token: authToken, expiresAt });
    setToken(authToken);

    try {
      setUser(await usersApi.getMe());
    } catch (error) {
      logout();
      throw error;
    }
  }, [logout]);

  async function login(emailOrUsername: string, password: string) {
    const response = await authApi.login({
      emailOrUsername,
      password
    });
    await finishAuthentication(response.token, response.expiresAt);
  }

  async function register(request: RegisterRequest) {
    const response = await authApi.register(request);
    await finishAuthentication(response.token, response.expiresAt);
  }

  useEffect(() => {
    if (!token) {
      setIsLoading(false);
      return;
    }

    refreshProfile()
      .catch(() => logout())
      .finally(() => setIsLoading(false));
  }, [logout, refreshProfile, token]);

  useEffect(() => subscribeUnauthorized(logout), [logout]);

  // Osvezi profil (i moderacione mere) kad se korisnik vrati na tab i periodicno,
  // da bi mute/suspenzija bila vidljiva bez rucnog osvezavanja stranice.
  useEffect(() => {
    if (!token) return;

    const refresh = () => { refreshProfile().catch(() => undefined); };
    window.addEventListener('focus', refresh);
    const intervalId = window.setInterval(refresh, 45000);

    return () => {
      window.removeEventListener('focus', refresh);
      window.clearInterval(intervalId);
    };
  }, [token, refreshProfile]);

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      user,
      isAuthenticated: Boolean(token && user),
      isLoading,
      login,
      register,
      logout,
      refreshProfile,
      hasRole: (...roles: Role[]) => hasRequiredRole(user?.uloga, roles)
    }),
    [token, user, isLoading, logout, refreshProfile]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }

  return context;
}

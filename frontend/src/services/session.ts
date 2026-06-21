export const AUTH_TOKEN_KEY = 'pleaguehub.token';
export const AUTH_EXPIRY_KEY = 'pleaguehub.expiresAt';

export interface AuthSession {
  token: string;
  expiresAt: string;
}

interface SessionStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function browserStorage(): SessionStorage {
  return window.localStorage;
}

export function readSession(storage: SessionStorage = browserStorage()): AuthSession | null {
  const token = storage.getItem(AUTH_TOKEN_KEY);
  const expiresAt = storage.getItem(AUTH_EXPIRY_KEY);

  if (!token) {
    return null;
  }

  if (expiresAt && Date.parse(expiresAt) <= Date.now()) {
    clearSession(storage);
    return null;
  }

  return { token, expiresAt: expiresAt ?? '' };
}

export function saveSession(session: AuthSession, storage: SessionStorage = browserStorage()) {
  storage.setItem(AUTH_TOKEN_KEY, session.token);
  storage.setItem(AUTH_EXPIRY_KEY, session.expiresAt);
}

export function clearSession(storage: SessionStorage = browserStorage()) {
  storage.removeItem(AUTH_TOKEN_KEY);
  storage.removeItem(AUTH_EXPIRY_KEY);
}

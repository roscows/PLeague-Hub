import { describe, expect, it } from 'vitest';
import { clearSession, readSession, saveSession } from './session';

function createStorage(initial: Record<string, string> = {}) {
  const values = new Map(Object.entries(initial));

  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key)
  };
}

describe('JWT session storage', () => {
  it('persists and restores a valid session', () => {
    const storage = createStorage();
    const expiresAt = new Date(Date.now() + 60_000).toISOString();

    saveSession({ token: 'jwt-token', expiresAt }, storage);

    expect(readSession(storage)).toEqual({ token: 'jwt-token', expiresAt });
  });

  it('removes an expired session', () => {
    const storage = createStorage({
      'pleaguehub.token': 'expired-token',
      'pleaguehub.expiresAt': new Date(Date.now() - 60_000).toISOString()
    });

    expect(readSession(storage)).toBeNull();
    expect(storage.getItem('pleaguehub.token')).toBeNull();
  });

  it('clears all session values', () => {
    const storage = createStorage({
      'pleaguehub.token': 'jwt-token',
      'pleaguehub.expiresAt': new Date(Date.now() + 60_000).toISOString()
    });

    clearSession(storage);

    expect(readSession(storage)).toBeNull();
  });
});

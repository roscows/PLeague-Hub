import { describe, expect, it } from 'vitest';
import { getApiErrorMessage } from './apiError';

describe('getApiErrorMessage', () => {
  it('uses the message returned by the backend', () => {
    const error = {
      isAxiosError: true,
      response: { data: { message: 'Email je vec registrovan.' } }
    };

    expect(getApiErrorMessage(error, 'Doslo je do greske.')).toBe('Email je vec registrovan.');
  });

  it('uses the supplied fallback for an unknown error', () => {
    expect(getApiErrorMessage(new Error('network'), 'API nije dostupan.')).toBe('API nije dostupan.');
  });
});

import { describe, expect, it, vi } from 'vitest';
import { notifyUnauthorized, subscribeUnauthorized } from './authEvents';

describe('unauthorized event', () => {
  it('notifies subscribers and supports unsubscribe', () => {
    const listener = vi.fn();
    const unsubscribe = subscribeUnauthorized(listener);

    notifyUnauthorized();
    unsubscribe();
    notifyUnauthorized();

    expect(listener).toHaveBeenCalledTimes(1);
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';
import { debounce } from './debounce';

describe('debounce', () => {
  afterEach(() => vi.useRealTimers());

  it('runs only the latest scheduled call after the delay', () => {
    vi.useFakeTimers();
    const callback = vi.fn();
    const debounced = debounce(callback, 250);

    debounced('Er');
    debounced('Erl');
    vi.advanceTimersByTime(249);

    expect(callback).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);

    expect(callback).toHaveBeenCalledOnce();
    expect(callback).toHaveBeenCalledWith('Erl');
  });

  it('cancels a pending call', () => {
    vi.useFakeTimers();
    const callback = vi.fn();
    const debounced = debounce(callback, 250);

    debounced('Erl');
    debounced.cancel();
    vi.advanceTimersByTime(250);

    expect(callback).not.toHaveBeenCalled();
  });
});

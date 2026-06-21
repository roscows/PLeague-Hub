export type Debounced<TArguments extends unknown[]> = ((...args: TArguments) => void) & {
  cancel: () => void;
};

export function debounce<TArguments extends unknown[]>(
  callback: (...args: TArguments) => void,
  delay: number
): Debounced<TArguments> {
  let timeout: ReturnType<typeof setTimeout> | undefined;

  const debounced = ((...args: TArguments) => {
    if (timeout) {
      clearTimeout(timeout);
    }

    timeout = setTimeout(() => callback(...args), delay);
  }) as Debounced<TArguments>;

  debounced.cancel = () => {
    if (timeout) {
      clearTimeout(timeout);
      timeout = undefined;
    }
  };

  return debounced;
}

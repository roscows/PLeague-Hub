function pluralMinutes(value: number) {
  return value === 1 ? 'minut' : 'minuta';
}

function pluralHours(value: number) {
  if (value === 1) return 'sat';
  if (value >= 2 && value <= 4) return 'sata';
  return 'sati';
}

function utcDay(value: Date) {
  return Date.UTC(value.getUTCFullYear(), value.getUTCMonth(), value.getUTCDate());
}

export function formatRelativeTime(value: string | Date, now = new Date()) {
  const date = value instanceof Date ? value : new Date(value);
  const differenceSeconds = Math.max(0, Math.floor((now.getTime() - date.getTime()) / 1000));

  if (differenceSeconds < 60) return 'pre nekoliko sekundi';

  const differenceMinutes = Math.floor(differenceSeconds / 60);

  if (differenceMinutes < 60) return `pre ${differenceMinutes} ${pluralMinutes(differenceMinutes)}`;

  const dayDifference = Math.floor((utcDay(now) - utcDay(date)) / 86_400_000);

  if (dayDifference === 1 && differenceMinutes >= 360) return 'juce';

  const differenceHours = Math.floor(differenceMinutes / 60);

  if (differenceHours < 24) return `pre ${differenceHours} ${pluralHours(differenceHours)}`;
  if (dayDifference < 7) return `pre ${dayDifference} dana`;

  return new Intl.DateTimeFormat('sr-Latn-RS', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date);
}

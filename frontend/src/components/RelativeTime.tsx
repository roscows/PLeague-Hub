import { useEffect, useState } from 'react';
import { formatRelativeTime } from '../utils/relativeTime';

interface RelativeTimeProps {
  value: string | Date;
  className?: string;
}

export function RelativeTime({ value, className }: RelativeTimeProps) {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const interval = window.setInterval(() => setNow(new Date()), 60_000);
    return () => window.clearInterval(interval);
  }, []);

  const date = value instanceof Date ? value : new Date(value);

  return (
    <time className={className} dateTime={date.toISOString()}>
      {formatRelativeTime(date, now)}
    </time>
  );
}

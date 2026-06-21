import type { NewsReliability } from '../../types/api';

const badges: Record<NewsReliability, { label: string; className: string }> = {
  zvanicno: { label: 'Zvanicno', className: 'border-emerald-200 bg-emerald-50 text-emerald-800' },
  pouzdan_izvor: { label: 'Pouzdan izvor', className: 'border-blue-200 bg-blue-50 text-blue-800' },
  glasina: { label: 'Glasina', className: 'border-amber-200 bg-amber-50 text-amber-900' },
  fpl_analiza: { label: 'FPL analiza', className: 'border-cyan-200 bg-cyan-50 text-cyan-900' }
};

export function NewsBadge({ value }: { value: NewsReliability }) {
  const badge = badges[value];
  return (
    <span className={`inline-flex min-h-6 items-center rounded border px-2 text-[10px] font-extrabold uppercase ${badge.className}`}>
      {badge.label}
    </span>
  );
}

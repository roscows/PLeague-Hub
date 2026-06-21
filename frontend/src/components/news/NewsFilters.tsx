import type { NewsCategory, NewsReliability } from '../../types/api';

export type NewsCategoryFilter = NewsCategory | 'sve';

const categories: Array<{ value: NewsCategoryFilter; label: string }> = [
  { value: 'sve', label: 'Sve' },
  { value: 'premier_league', label: 'Premier liga' },
  { value: 'transferi', label: 'Transferi' },
  { value: 'fpl', label: 'FPL' },
  { value: 'klubovi', label: 'Klubovi' }
];

interface NewsFiltersProps {
  category: NewsCategoryFilter;
  reliability: NewsReliability | '';
  onCategoryChange: (value: NewsCategoryFilter) => void;
  onReliabilityChange: (value: NewsReliability | '') => void;
}

export function NewsFilters({ category, reliability, onCategoryChange, onReliabilityChange }: NewsFiltersProps) {
  return (
    <div className="flex flex-col gap-3 border-y border-slate-200 bg-white px-3 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div aria-label="Kategorije vesti" className="flex max-w-full gap-1 overflow-x-auto" role="group">
        {categories.map((item) => (
          <button
            aria-pressed={category === item.value}
            className={`min-h-10 shrink-0 rounded px-3 text-xs font-bold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-brand ${
              category === item.value ? 'bg-ink text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
            key={item.value}
            onClick={() => onCategoryChange(item.value)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>
      <label className="flex shrink-0 items-center gap-2 text-xs font-bold text-slate-500">
        Pouzdanost
        <select
          className="min-h-10 rounded border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-700 outline-none focus:border-brand"
          onChange={(event) => onReliabilityChange(event.target.value as NewsReliability | '')}
          value={reliability}
        >
          <option value="">Sve oznake</option>
          <option value="zvanicno">Zvanicno</option>
          <option value="pouzdan_izvor">Pouzdani izvori</option>
          <option value="glasina">Glasine</option>
          <option value="fpl_analiza">FPL analize</option>
        </select>
      </label>
    </div>
  );
}

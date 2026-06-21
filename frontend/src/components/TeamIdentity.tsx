import type { Team } from '../types/api';

interface TeamIdentityProps {
  team?: Team;
  align?: 'left' | 'right';
  compact?: boolean;
}

export function TeamIdentity({ team, align = 'left', compact = false }: TeamIdentityProps) {
  const content = (
    <>
      <img className={`${compact ? 'size-5' : 'size-7'} shrink-0 object-contain`} src={team?.logoUrl} alt="" />
      <span className="truncate font-semibold">{team?.naziv ?? 'Nepoznat tim'}</span>
    </>
  );

  return (
    <div className={`flex min-w-0 items-center gap-2 text-sm ${align === 'right' ? 'justify-end' : ''}`}>
      {align === 'right' ? <>{content}</> : content}
    </div>
  );
}

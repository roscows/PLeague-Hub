import type { Team } from '../types/api';
import { TeamLogo } from './TeamLogo';

interface TeamIdentityProps {
  team?: Team;
  align?: 'left' | 'right';
  compact?: boolean;
}

export function TeamIdentity({ team, align = 'left', compact = false }: TeamIdentityProps) {
  const content = (
    <>
      <TeamLogo
        className={compact ? 'size-5' : 'size-7'}
        logoUrl={team?.logoUrl}
        name={team?.naziv}
      />
      <span className="truncate font-semibold">{team?.naziv ?? 'Nepoznat tim'}</span>
    </>
  );

  return (
    <div className={`flex min-w-0 items-center gap-2 text-sm ${align === 'right' ? 'justify-end' : ''}`}>
      {align === 'right' ? <>{content}</> : content}
    </div>
  );
}

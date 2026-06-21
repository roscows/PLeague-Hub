import { Shield } from 'lucide-react';
import { useEffect, useState } from 'react';
import { resolveApiAssetUrl } from '../services/assets';

interface TeamLogoProps {
  logoUrl?: string;
  name?: string;
  className?: string;
}

export function TeamLogo({ logoUrl, name, className = 'size-7' }: TeamLogoProps) {
  const source = resolveApiAssetUrl(logoUrl);
  const [hasFailed, setHasFailed] = useState(false);

  useEffect(() => {
    setHasFailed(false);
  }, [source]);

  if (!source || hasFailed) {
    return (
      <span
        aria-label={name ? `Logo tima ${name} nije dostupan` : 'Logo tima nije dostupan'}
        className={`${className} grid shrink-0 place-items-center rounded border border-slate-200 bg-slate-50 text-slate-400`}
        role="img"
      >
        <Shield className="size-3/5" />
      </span>
    );
  }

  return (
    <img
      alt=""
      className={`${className} shrink-0 object-contain`}
      onError={() => setHasFailed(true)}
      src={source}
    />
  );
}

import { MoreHorizontal, Pin, PinOff, Trash2 } from 'lucide-react';
import { useState } from 'react';

interface CommentActionsMenuProps {
  number: number;
  pinned: boolean;
  onDelete: () => void;
  onTogglePin: () => void;
}

export function CommentActionsMenu({ number, pinned, onDelete, onTogglePin }: CommentActionsMenuProps) {
  const [open, setOpen] = useState(false);

  return (
    <div className="relative">
      <button
        aria-expanded={open}
        aria-label={`Akcije za komentar #${number}`}
        className="grid size-7 place-items-center rounded text-slate-500 hover:bg-slate-200"
        onClick={() => setOpen((value) => !value)}
        type="button"
      >
        <MoreHorizontal size={16} />
      </button>
      {open && (
        <div className="absolute right-0 top-8 z-20 min-w-44 overflow-hidden rounded border border-slate-200 bg-white py-1 shadow-lg">
          <button
            aria-label={`${pinned ? 'Otkaci' : 'Pinuj'} komentar #${number}`}
            className="flex w-full items-center gap-2 px-3 py-2 text-left text-xs font-semibold hover:bg-slate-50"
            onClick={() => { setOpen(false); onTogglePin(); }}
            type="button"
          >
            {pinned ? <PinOff size={14} /> : <Pin size={14} />}
            {pinned ? 'Otkaci komentar' : 'Pinuj komentar'}
          </button>
          <button
            aria-label={`Obrisi komentar #${number}`}
            className="flex w-full items-center gap-2 px-3 py-2 text-left text-xs font-semibold text-red-600 hover:bg-red-50"
            onClick={() => { setOpen(false); onDelete(); }}
            type="button"
          >
            <Trash2 size={14} /> Obrisi komentar
          </button>
        </div>
      )}
    </div>
  );
}

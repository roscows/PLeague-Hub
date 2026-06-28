import { Flag, MessageSquareReply, Pin, ThumbsDown, ThumbsUp } from 'lucide-react';
import type { CommentVoteValue, ForumCommentNode } from '../../types/api';
import { RelativeTime } from '../RelativeTime';
import { TeamLogo } from '../TeamLogo';
import { CommentActionsMenu } from './CommentActionsMenu';
import { ForumReplyForm } from './ForumReplyForm';

interface ForumCommentProps {
  comment: ForumCommentNode;
  currentUserId?: string;
  isReplying: boolean;
  onReply: (commentId: string) => void;
  onCancelReply: () => void;
  onSubmitReply: (commentId: string, text: string) => Promise<void>;
  onVote: (commentId: string, value: CommentVoteValue) => void;
  parentNumber?: number;
  canModerate: boolean;
  onModerate: () => void;
  onTogglePin: () => void;
  onDelete: () => void;
  onReport: () => void;
}

function roleLabel(role: string) {
  if (role === 'administrator') return 'Administrator';
  return 'Moderator';
}

export function ForumComment({
  comment,
  currentUserId,
  isReplying,
  onReply,
  onCancelReply,
  onSubmitReply,
  onVote,
  parentNumber,
  canModerate,
  onModerate,
  onTogglePin,
  onDelete,
  onReport
}: ForumCommentProps) {
  const ownComment = currentUserId === comment.autorId;
  const voteDisabled = ownComment || comment.obrisan;
  const visualDepth = Math.min(comment.depth, 6);
  const indent = (visualDepth - 1) * 8;

  return (
    <div
      className={`min-w-0 max-w-full ${visualDepth > 1 ? 'border-l-2 border-slate-300 pl-2' : ''}`}
      data-comment-depth={comment.depth}
      style={{ marginLeft: indent, width: `calc(100% - ${indent}px)` }}
    >
      <article className={`min-w-0 max-w-full overflow-hidden rounded border ${comment.obrisan ? 'border-slate-200 bg-slate-50' : 'border-slate-300 bg-white'}`}>
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 bg-slate-100 px-3 py-2 text-xs">
          <span className="font-extrabold text-slate-500">#{comment.broj}</span>
          {comment.depth > 6 && parentNumber && <span className="text-[11px] text-slate-500">odgovor na #{parentNumber}</span>}
          {comment.istaknut && <span className="flex items-center gap-1 text-[11px] font-bold text-brand"><Pin size={12} /> Pinovano</span>}
          <div className="ml-auto flex min-w-0 items-center gap-1.5">
            {comment.autorFavoritniTim && (
              <span className="shrink-0" title={comment.autorFavoritniTim.naziv}>
                <TeamLogo
                  className="size-5"
                  logoUrl={comment.autorFavoritniTim.logoUrl}
                  name={comment.autorFavoritniTim.naziv}
                />
              </span>
            )}
            {canModerate ? (
              <button className="truncate font-bold text-slate-800 hover:text-brand hover:underline" onClick={onModerate} type="button">
                {comment.autorUsername}
              </button>
            ) : (
              <span className="truncate font-bold text-slate-800">{comment.autorUsername}</span>
            )}
          </div>
          {comment.autorUloga !== 'registrovani' && (
            <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-bold uppercase text-slate-500">
              {roleLabel(comment.autorUloga)}
            </span>
          )}
          {canModerate && !comment.obrisan && (
            <CommentActionsMenu number={comment.broj} pinned={comment.istaknut} onDelete={onDelete} onTogglePin={onTogglePin} />
          )}
        </header>
        <p className={`break-words whitespace-pre-wrap px-3 py-3 text-sm leading-6 ${comment.obrisan ? 'italic text-slate-500' : 'text-slate-700'}`}>
          {comment.tekst}
        </p>
        {!comment.obrisan && (
          <footer className="flex flex-wrap items-center gap-1 border-t border-slate-200 px-2 py-1.5 text-xs text-slate-500">
            <button
              aria-label={`Odgovori na komentar #${comment.broj}`}
              className="flex items-center gap-1 rounded px-2 py-1 hover:bg-slate-100 hover:text-slate-900"
              onClick={() => onReply(comment.id)}
              type="button"
            >
              <MessageSquareReply size={14} /> Odgovori
            </button>
            {currentUserId && !ownComment && (
              <button
                aria-label={`Prijavi komentar #${comment.broj}`}
                className="flex items-center gap-1 rounded px-2 py-1 hover:bg-slate-100 hover:text-red-600"
                onClick={onReport}
                title="Prijavi komentar"
                type="button"
              >
                <Flag size={14} />
              </button>
            )}
            <button
              aria-label={`Svidja mi se komentaru #${comment.broj}`}
              aria-pressed={comment.trenutniGlas === 1}
              className={`flex items-center gap-1 rounded px-2 py-1 sm:ml-auto ${comment.trenutniGlas === 1 ? 'bg-emerald-100 text-emerald-700' : 'hover:bg-slate-100'}`}
              disabled={voteDisabled}
              onClick={() => onVote(comment.id, 1)}
              title={ownComment ? 'Nije moguce glasati za sopstveni komentar' : 'Svidja mi se'}
              type="button"
            >
              <ThumbsUp size={14} /> {comment.lajkovi}
            </button>
            <button
              aria-label={`Ne svidja mi se komentar #${comment.broj}`}
              aria-pressed={comment.trenutniGlas === -1}
              className={`flex items-center gap-1 rounded px-2 py-1 ${comment.trenutniGlas === -1 ? 'bg-red-100 text-red-700' : 'hover:bg-slate-100'}`}
              disabled={voteDisabled}
              onClick={() => onVote(comment.id, -1)}
              title={ownComment ? 'Nije moguce glasati za sopstveni komentar' : 'Ne svidja mi se'}
              type="button"
            >
              <ThumbsDown size={14} /> {comment.dislajkovi}
            </button>
            <RelativeTime className="w-full px-2 pt-1 text-[11px] text-slate-400 sm:ml-1 sm:w-auto sm:pt-0" value={comment.datumKreiranja} />
          </footer>
        )}
        {isReplying && (
          <ForumReplyForm
            onCancel={onCancelReply}
            onSubmit={(text) => onSubmitReply(comment.id, text)}
          />
        )}
      </article>
    </div>
  );
}

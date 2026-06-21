import { MessageSquareReply, ThumbsDown, ThumbsUp } from 'lucide-react';
import type { CommentVoteValue, ForumCommentNode } from '../../types/api';
import { formatRelativeTime } from '../../utils/relativeTime';
import { ForumReplyForm } from './ForumReplyForm';

interface ForumCommentProps {
  comment: ForumCommentNode;
  currentUserId?: string;
  isReplying: boolean;
  onReply: (commentId: string) => void;
  onCancelReply: () => void;
  onSubmitReply: (commentId: string, text: string) => Promise<void>;
  onVote: (commentId: string, value: CommentVoteValue) => void;
}

const depthClasses = {
  1: 'min-w-0 w-full max-w-full',
  2: 'ml-2 min-w-0 w-[calc(100%_-_0.5rem)] max-w-full border-l-2 border-slate-300 pl-2 sm:ml-8 sm:w-[calc(100%_-_2rem)] sm:pl-3',
  3: 'ml-4 min-w-0 w-[calc(100%_-_1rem)] max-w-full border-l-2 border-slate-300 pl-2 sm:ml-16 sm:w-[calc(100%_-_4rem)] sm:pl-3'
};

function roleLabel(role: string) {
  if (role === 'administrator') return 'Administrator';
  if (role === 'moderator') return 'Moderator';
  return 'Clan';
}

export function ForumComment({
  comment,
  currentUserId,
  isReplying,
  onReply,
  onCancelReply,
  onSubmitReply,
  onVote
}: ForumCommentProps) {
  const ownComment = currentUserId === comment.autorId;
  const voteDisabled = ownComment || comment.obrisan;

  return (
    <div className={depthClasses[comment.depth as 1 | 2 | 3]} data-comment-depth={comment.depth}>
      <article className={`min-w-0 max-w-full overflow-hidden rounded border ${comment.obrisan ? 'border-slate-200 bg-slate-50' : 'border-slate-300 bg-white'}`}>
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 bg-slate-100 px-3 py-2 text-xs">
          <span className="font-extrabold text-slate-500">#{comment.broj}</span>
          <span className="ml-auto font-bold text-slate-800">{comment.autorUsername}</span>
          <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-bold uppercase text-slate-500">{roleLabel(comment.autorUloga)}</span>
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
            <time className="w-full px-2 pt-1 text-[11px] text-slate-400 sm:ml-1 sm:w-auto sm:pt-0" dateTime={comment.datumKreiranja}>
              {formatRelativeTime(comment.datumKreiranja)}
            </time>
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

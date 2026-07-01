import { MessageSquareReply } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { RelativeTime } from '../components/RelativeTime';
import { ForumReplyForm } from '../components/forum/ForumReplyForm';
import { ForumThread } from '../components/forum/ForumThread';
import { ModerationModal } from '../components/forum/ModerationModal';
import { ReportCommentModal } from '../components/forum/ReportCommentModal';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';
import { forumApi } from '../services/forumApi';
import { moderationApi } from '../services/moderationApi';
import type { CommentVoteValue, ForumComment, ForumCommentNode, ForumDiscussion, ModerationState } from '../types/api';
import { buildCommentTree } from '../utils/forumTree';

export function ForumDiscussionPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, user } = useAuth();
  const [discussion, setDiscussion] = useState<ForumDiscussion | null>(null);
  const [comments, setComments] = useState<ForumComment[]>([]);
  const [replyingTo, setReplyingTo] = useState<string | null>(null);
  const [rootReplyOpen, setRootReplyOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [mutationError, setMutationError] = useState<string | null>(null);
  const [moderationTarget, setModerationTarget] = useState<{ comment: ForumCommentNode; state: ModerationState | null } | null>(null);
  const [reportTarget, setReportTarget] = useState<ForumCommentNode | null>(null);

  useEffect(() => {
    if (!id) return;
    let active = true;
    setLoading(true);
    setError(null);

    Promise.all([forumApi.getById(id), forumApi.listComments(id)])
      .then(([loadedDiscussion, loadedComments]) => {
        if (!active) return;
        setDiscussion(loadedDiscussion);
        setComments(loadedComments);
      })
      .catch((requestError) => {
        if (active) setError(getApiErrorMessage(requestError, 'Diskusija nije pronadjena.'));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [id]);

  const thread = useMemo(() => buildCommentTree(comments), [comments]);

  async function reloadComments() {
    if (id) setComments(await forumApi.listComments(id));
  }

  // Kad korisnik promeni omiljeni klub, ponovo povuci komentare da se grb autora
  // odmah osvezi (bez rucnog refresha stranice).
  const favTeamId = user?.favoritniTimovi?.[0];
  const favInitialized = useRef(false);
  useEffect(() => {
    if (!favInitialized.current) {
      favInitialized.current = true;
      return;
    }
    reloadComments().catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [favTeamId]);

  async function openModeration(comment: ForumCommentNode) {
    setMutationError(null);
    try {
      const state = await moderationApi.getUserState(comment.autorId);
      setModerationTarget({ comment, state });
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Podaci za moderaciju nisu ucitani.'));
    }
  }

  async function togglePin(comment: ForumCommentNode) {
    setMutationError(null);
    try {
      await (comment.istaknut ? moderationApi.unpinComment(comment.id) : moderationApi.pinComment(comment.id));
      await reloadComments();
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Status pinovanja nije sacuvan.'));
    }
  }

  async function deleteComment(comment: ForumCommentNode) {
    setMutationError(null);
    try {
      await moderationApi.removeComment(comment.id);
      await reloadComments();
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Komentar nije obrisan.'));
    }
  }

  function requireAuthentication() {
    if (isAuthenticated) return true;
    navigate('/login', { state: { from: location.pathname } });
    return false;
  }

  function openReport(comment: ForumCommentNode) {
    if (!requireAuthentication()) return;
    setReportTarget(comment);
  }

  function startReply(commentId: string) {
    if (!requireAuthentication()) return;
    setRootReplyOpen(false);
    setReplyingTo(commentId);
  }

  async function submitReply(parentCommentId: string | undefined, text: string) {
    if (!id || !requireAuthentication()) return;

    try {
      await forumApi.createComment(id, { tekst: text, ...(parentCommentId ? { parentCommentId } : {}) });
      setComments(await forumApi.listComments(id));
      setReplyingTo(null);
      setRootReplyOpen(false);
    } catch (requestError) {
      throw new Error(getApiErrorMessage(requestError, 'Odgovor nije poslat.'));
    }
  }

  async function vote(commentId: string, value: CommentVoteValue) {
    if (!requireAuthentication()) return;
    const previousComments = comments;
    const target = comments.find((comment) => comment.id === commentId);

    if (!target || target.autorId === user?.userId) return;

    const removing = target.trenutniGlas === value;
    setMutationError(null);
    setComments((current) => current.map((comment) => {
      if (comment.id !== commentId) return comment;
      const next = { ...comment };

      if (comment.trenutniGlas === 1) next.lajkovi--;
      if (comment.trenutniGlas === -1) next.dislajkovi--;
      next.trenutniGlas = removing ? null : value;
      if (next.trenutniGlas === 1) next.lajkovi++;
      if (next.trenutniGlas === -1) next.dislajkovi++;
      return next;
    }));

    try {
      const response = removing
        ? await forumApi.removeCommentVote(commentId)
        : await forumApi.voteComment(commentId, value);
      setComments((current) => current.map((comment) => comment.id === commentId
        ? { ...comment, lajkovi: response.lajkovi, dislajkovi: response.dislajkovi, trenutniGlas: response.trenutniGlas }
        : comment));
    } catch (requestError) {
      setComments(previousComments);
      setMutationError(getApiErrorMessage(requestError, 'Glas nije sacuvan.'));
    }
  }

  if (loading) return <div className="rounded border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Ucitavanje diskusije...</div>;

  if (error || !discussion) {
    return (
      <div className="rounded border border-slate-200 bg-white p-8 text-center">
        <p className="text-sm text-red-600">{error ?? 'Diskusija nije pronadjena.'}</p>
        <Link className="mt-3 inline-block text-sm font-bold text-brand" to="/forum">Nazad na forum</Link>
      </div>
    );
  }

  return (
    <div className="min-w-0 max-w-full space-y-3 overflow-hidden">
      <article className="min-w-0 max-w-full overflow-hidden rounded border border-slate-300 bg-white shadow-sm">
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 bg-slate-100 px-4 py-3">
          <h1 className="text-lg font-extrabold text-slate-950">{discussion.naslov}</h1>
          <span className="text-xs font-bold text-slate-600">{discussion.autorUsername}</span>
        </header>
        <p className="break-words whitespace-pre-wrap px-4 py-4 text-sm leading-6 text-slate-700">{discussion.sadrzaj}</p>
        <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 px-3 py-2 text-xs text-slate-500">
          <RelativeTime value={discussion.datumKreiranja} />
          <button
            className="flex items-center gap-1 rounded px-2 py-1 font-bold hover:bg-slate-100 hover:text-slate-900"
            onClick={() => {
              if (!requireAuthentication()) return;
              setReplyingTo(null);
              setRootReplyOpen(true);
            }}
            type="button"
          >
            <MessageSquareReply size={14} /> Odgovori na temu
          </button>
        </footer>
        {rootReplyOpen && <ForumReplyForm onCancel={() => setRootReplyOpen(false)} onSubmit={(text) => submitReply(undefined, text)} />}
      </article>

      {mutationError && <p className="rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{mutationError}</p>}

      {comments.length === 0 ? (
        <div className="rounded border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Jos nema odgovora.</div>
      ) : (
        <ForumThread
          currentUserId={user?.userId}
          currentUserRole={user?.uloga}
          nodes={thread}
          replyingTo={replyingTo}
          onCancelReply={() => setReplyingTo(null)}
          onDelete={deleteComment}
          onModerate={openModeration}
          onReply={startReply}
          onReport={openReport}
          onSubmitReply={submitReply}
          onTogglePin={togglePin}
          onVote={vote}
        />
      )}
      {reportTarget && (
        <ReportCommentModal
          commentId={reportTarget.id}
          onClose={() => setReportTarget(null)}
          onReported={() => setMutationError(null)}
        />
      )}
      {moderationTarget && (
        <ModerationModal
          currentState={moderationTarget.state}
          onChanged={() => setModerationTarget(null)}
          onClose={() => setModerationTarget(null)}
          target={{
            id: moderationTarget.comment.autorId,
            username: moderationTarget.comment.autorUsername,
            role: moderationTarget.comment.autorUloga
          }}
        />
      )}
    </div>
  );
}

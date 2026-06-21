import { ArrowLeft, ExternalLink, MessageSquareReply, Pencil, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { RelativeTime } from '../components/RelativeTime';
import { ForumReplyForm } from '../components/forum/ForumReplyForm';
import { ForumThread } from '../components/forum/ForumThread';
import { ModerationModal } from '../components/forum/ModerationModal';
import { NewsBadge } from '../components/news/NewsBadge';
import { NewsEditor } from '../components/news/NewsEditor';
import { XEmbed } from '../components/news/XEmbed';
import { useAuth } from '../contexts/AuthContext';
import { getApiErrorMessage } from '../services/apiError';
import { forumApi } from '../services/forumApi';
import { moderationApi } from '../services/moderationApi';
import { newsApi } from '../services/newsApi';
import type { CommentVoteValue, ForumComment, ForumCommentNode, ModerationState, NewsDetail } from '../types/api';
import { buildCommentTree } from '../utils/forumTree';

const exactDate = new Intl.DateTimeFormat('sr-Latn-RS', {
  day: '2-digit', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit'
});

export function NewsDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { isAuthenticated, user } = useAuth();
  const [news, setNews] = useState<NewsDetail | null>(null);
  const [comments, setComments] = useState<ForumComment[]>([]);
  const [replyingTo, setReplyingTo] = useState<string | null>(null);
  const [rootReplyOpen, setRootReplyOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [mutationError, setMutationError] = useState<string | null>(null);
  const [moderationTarget, setModerationTarget] = useState<{ comment: ForumCommentNode; state: ModerationState | null } | null>(null);
  const canEdit = user?.uloga === 'moderator' || user?.uloga === 'administrator';
  const editorOpen = canEdit && searchParams.get('edit') === '1';

  function closeEditor() {
    const next = new URLSearchParams(searchParams);
    next.delete('edit');
    setSearchParams(next, { replace: true });
  }

  useEffect(() => {
    if (!id) return;
    let active = true;
    setLoading(true);
    Promise.all([newsApi.getById(id), newsApi.listComments(id)])
      .then(([loadedNews, loadedComments]) => {
        if (!active) return;
        setNews(loadedNews);
        setComments(loadedComments);
        setError(null);
      })
      .catch((requestError) => active && setError(getApiErrorMessage(requestError, 'Vest nije pronadjena.')))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [id]);

  const thread = useMemo(() => buildCommentTree(comments), [comments]);

  function requireAuthentication() {
    if (isAuthenticated) return true;
    navigate('/login', { state: { from: location.pathname } });
    return false;
  }

  async function reloadComments() {
    if (id) setComments(await newsApi.listComments(id));
  }

  async function submitReply(parentCommentId: string | undefined, text: string) {
    if (!id || !requireAuthentication()) return;
    try {
      await newsApi.createComment(id, { tekst: text, ...(parentCommentId ? { parentCommentId } : {}) });
      await reloadComments();
      setReplyingTo(null);
      setRootReplyOpen(false);
    } catch (requestError) {
      throw new Error(getApiErrorMessage(requestError, 'Komentar nije poslat.'));
    }
  }

  async function vote(commentId: string, value: CommentVoteValue) {
    if (!requireAuthentication()) return;
    const previous = comments;
    const target = comments.find((comment) => comment.id === commentId);
    if (!target || target.autorId === user?.userId) return;
    const removing = target.trenutniGlas === value;
    setMutationError(null);
    setComments((current) => current.map((comment) => {
      if (comment.id !== commentId) return comment;
      const next = { ...comment };
      if (next.trenutniGlas === 1) next.lajkovi--;
      if (next.trenutniGlas === -1) next.dislajkovi--;
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
        ? { ...comment, ...response }
        : comment));
    } catch (requestError) {
      setComments(previous);
      setMutationError(getApiErrorMessage(requestError, 'Glas nije sacuvan.'));
    }
  }

  async function openModeration(comment: ForumCommentNode) {
    try {
      const state = await moderationApi.getUserState(comment.autorId);
      setModerationTarget({ comment, state });
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Podaci za moderaciju nisu ucitani.'));
    }
  }

  async function togglePin(comment: ForumCommentNode) {
    try {
      await (comment.istaknut ? moderationApi.unpinComment(comment.id) : moderationApi.pinComment(comment.id));
      await reloadComments();
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Status pinovanja nije sacuvan.'));
    }
  }

  async function deleteComment(comment: ForumCommentNode) {
    try {
      await moderationApi.removeComment(comment.id);
      await reloadComments();
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Komentar nije obrisan.'));
    }
  }

  async function deleteNews() {
    if (!id || !window.confirm('Obrisati ovu vest?')) return;
    try {
      await newsApi.remove(id);
      navigate('/news');
    } catch (requestError) {
      setMutationError(getApiErrorMessage(requestError, 'Vest nije obrisana.'));
    }
  }

  if (loading) return <div className="border border-slate-200 bg-white p-10 text-center text-sm text-slate-500">Ucitavanje vesti...</div>;
  if (error || !news) {
    return (
      <div className="border border-slate-200 bg-white p-10 text-center">
        <p className="text-sm text-red-700">{error ?? 'Vest nije pronadjena.'}</p>
        <Link className="mt-4 inline-block text-sm font-bold text-brand" to="/news">Nazad na vesti</Link>
      </div>
    );
  }

  return (
    <div className="min-w-0 max-w-full space-y-4 overflow-hidden">
      {editorOpen && <NewsEditor existing={news} onClose={closeEditor} onSaved={(updated) => { setNews(updated); closeEditor(); }} />}
      <article className="min-w-0 overflow-hidden border border-slate-200 bg-white">
        <header className="border-b border-slate-200 px-4 py-4 sm:px-6 sm:py-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <Link className="inline-flex items-center gap-2 text-xs font-bold text-slate-500 hover:text-brand" to="/news"><ArrowLeft size={14} /> Sve vesti</Link>
            {canEdit && (
              <div className="flex items-center gap-2">
                <Link className="inline-flex min-h-9 items-center gap-1.5 rounded border border-slate-300 px-3 text-xs font-bold text-slate-700" to={`/news/${news.id}?edit=1`}><Pencil size={13} /> Izmeni</Link>
                <button className="inline-flex min-h-9 items-center gap-1.5 rounded border border-red-200 px-3 text-xs font-bold text-red-700" onClick={deleteNews} type="button"><Trash2 size={13} /> Obrisi</button>
              </div>
            )}
          </div>
          <div className="mt-5 flex flex-wrap items-center gap-2">
            <NewsBadge value={news.pouzdanost} />
            <span className="text-xs font-bold text-slate-500">{news.izvorNaziv ?? news.autorUsername ?? 'PLeague Hub'}</span>
          </div>
          <h1 className="mt-3 break-words text-2xl font-extrabold leading-tight text-slate-950 sm:text-3xl">{news.naslov}</h1>
          <div className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-xs text-slate-500">
            <RelativeTime value={news.publishedAt} />
            <time dateTime={news.publishedAt}>{exactDate.format(new Date(news.publishedAt))}</time>
            {news.externalAuthor && <span>Autor: {news.externalAuthor}</span>}
          </div>
        </header>
        {news.imageUrl && <img alt={news.naslov} className="max-h-[460px] w-full object-cover" src={news.imageUrl} />}
        <div className="px-4 py-5 sm:px-6">
          {news.sadrzaj && <p className="break-words whitespace-pre-wrap text-[15px] leading-7 text-slate-700">{news.sadrzaj}</p>}
          {news.xEmbedUrl && <div className="mt-5"><XEmbed url={news.xEmbedUrl} /></div>}
          {news.originalUrl && (
            <a aria-label="Otvori originalnu vest" className="mt-6 inline-flex min-h-10 items-center gap-2 rounded bg-ink px-4 text-sm font-bold text-white" href={news.originalUrl} rel="noreferrer" target="_blank">
              Otvori originalnu vest <ExternalLink size={15} />
            </a>
          )}
        </div>
      </article>

      <section aria-labelledby="comments-heading" className="min-w-0 max-w-full">
        <header className="flex items-center justify-between gap-3 border-b-2 border-brand bg-white px-4 py-3">
          <h2 className="font-extrabold text-slate-950" id="comments-heading">Komentari ({comments.length})</h2>
          <button
            className="inline-flex min-h-9 items-center gap-2 rounded bg-brand px-3 text-xs font-bold text-white"
            onClick={() => {
              if (!requireAuthentication()) return;
              setReplyingTo(null);
              setRootReplyOpen(true);
            }}
            type="button"
          >
            <MessageSquareReply size={14} /> Napisi komentar
          </button>
        </header>
        {rootReplyOpen && <ForumReplyForm onCancel={() => setRootReplyOpen(false)} onSubmit={(text) => submitReply(undefined, text)} />}
        {mutationError && <p className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{mutationError}</p>}
        {comments.length === 0 ? (
          <div className="border-x border-b border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Jos nema komentara.</div>
        ) : (
          <div className="pt-3">
            <ForumThread
              currentUserId={user?.userId}
              currentUserRole={user?.uloga}
              nodes={thread}
              replyingTo={replyingTo}
              onCancelReply={() => setReplyingTo(null)}
              onDelete={deleteComment}
              onModerate={openModeration}
              onReply={(commentId) => {
                if (!requireAuthentication()) return;
                setRootReplyOpen(false);
                setReplyingTo(commentId);
              }}
              onSubmitReply={submitReply}
              onTogglePin={togglePin}
              onVote={vote}
            />
          </div>
        )}
      </section>
      {moderationTarget && (
        <ModerationModal
          currentState={moderationTarget.state}
          onChanged={() => setModerationTarget(null)}
          onClose={() => setModerationTarget(null)}
          target={{ id: moderationTarget.comment.autorId, username: moderationTarget.comment.autorUsername, role: moderationTarget.comment.autorUloga }}
        />
      )}
    </div>
  );
}

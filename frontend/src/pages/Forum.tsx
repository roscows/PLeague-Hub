import { MessageCircleMore, Send } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { getApiErrorMessage } from '../services/apiError';
import { forumApi } from '../services/forumApi';
import { useAuth } from '../contexts/AuthContext';
import type { Post } from '../types/api';

export function Forum() {
  const { isAuthenticated } = useAuth();
  const [discussions, setDiscussions] = useState<Post[]>([]);
  const [naslov, setNaslov] = useState('');
  const [sadrzaj, setSadrzaj] = useState('');
  const [error, setError] = useState<string | null>(null);

  function loadDiscussions() {
    forumApi.list().then(setDiscussions).catch((requestError) => {
      setError(getApiErrorMessage(requestError, 'Diskusije trenutno nisu dostupne.'));
    });
  }

  useEffect(() => {
    loadDiscussions();
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    try {
      await forumApi.create({ naslov, sadrzaj });
      setNaslov('');
      setSadrzaj('');
      loadDiscussions();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Diskusija nije sacuvana. Proveri da li si prijavljen.'));
    }
  }

  return (
    <div className="space-y-6">
      {isAuthenticated && (
        <form className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm" onSubmit={handleSubmit}>
          <h2 className="flex items-center gap-2 text-lg font-extrabold"><MessageCircleMore size={19} className="text-brand" /> Nova diskusija</h2>
          <div className="mt-4 grid gap-3">
            <input
              className="rounded-md border border-slate-300 px-3 py-2 text-sm"
              placeholder="Naslov"
              value={naslov}
              onChange={(event) => setNaslov(event.target.value)}
            />
            <textarea
              className="min-h-28 rounded-md border border-slate-300 px-3 py-2 text-sm"
              placeholder="Sadrzaj"
              value={sadrzaj}
              onChange={(event) => setSadrzaj(event.target.value)}
            />
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button className="flex w-fit items-center gap-2 rounded-md bg-brand px-4 py-2 text-sm font-bold text-white" type="submit">
              <Send size={15} /> Objavi
            </button>
          </div>
        </form>
      )}

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-100 px-4 py-4"><h2 className="text-xl font-extrabold">Forum</h2></div>
        <div className="divide-y divide-slate-100">
          {discussions.map((post) => (
            <article key={post.id} className="px-4 py-4 hover:bg-slate-50">
              <h3 className="font-bold">{post.naslov}</h3>
              <p className="mt-2 text-sm text-slate-600">{post.sadrzaj}</p>
              <p className="mt-3 text-xs text-slate-500">{new Date(post.datumKreiranja).toLocaleString()}</p>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

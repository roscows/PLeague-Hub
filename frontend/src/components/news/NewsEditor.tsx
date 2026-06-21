import { FormEvent, useState } from 'react';
import { Send, X } from 'lucide-react';
import { getApiErrorMessage } from '../../services/apiError';
import { newsApi } from '../../services/newsApi';
import type { NewsCategory, NewsDetail, NewsReliability } from '../../types/api';

type EditorMode = 'article' | 'x';

interface NewsEditorProps {
  existing?: NewsDetail;
  onClose: () => void;
  onSaved: (news: NewsDetail) => void;
}

const fieldClass = 'min-h-10 rounded border border-slate-300 bg-white px-3 py-2 text-sm font-normal text-slate-900 outline-none focus:border-brand';

export function NewsEditor({ existing, onClose, onSaved }: NewsEditorProps) {
  const [mode, setMode] = useState<EditorMode>(existing?.xEmbedUrl ? 'x' : 'article');
  const [naslov, setNaslov] = useState(existing?.naslov ?? '');
  const [sadrzaj, setSadrzaj] = useState(existing?.sadrzaj ?? '');
  const [xUrl, setXUrl] = useState(existing?.xEmbedUrl ?? '');
  const [kategorija, setKategorija] = useState<NewsCategory>(existing?.kategorija ?? 'premier_league');
  const [pouzdanost, setPouzdanost] = useState<NewsReliability>(existing?.pouzdanost ?? 'pouzdan_izvor');
  const [imageUrl, setImageUrl] = useState(existing?.imageUrl ?? '');
  const [originalUrl, setOriginalUrl] = useState(existing?.originalUrl ?? '');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (mode === 'x' && !/^https:\/\/(?:www\.)?x\.com\/[^/]+\/status\/\d+\/?(?:\?.*)?$/.test(xUrl.trim())) {
      setError('X URL mora imati oblik https://x.com/korisnik/status/broj.');
      return;
    }

    setSubmitting(true);
    try {
      let saved: NewsDetail;
      if (existing) {
        saved = await newsApi.update(existing.id, {
          naslov: naslov.trim(),
          ...(mode === 'article' ? { sadrzaj: sadrzaj.trim() } : {}),
          kategorija,
          pouzdanost,
          ...(mode === 'article' ? { imageUrl: imageUrl.trim(), originalUrl: originalUrl.trim() } : {})
        });
      } else if (mode === 'x') {
        saved = await newsApi.createX({ naslov: naslov.trim(), xUrl: xUrl.trim(), kategorija, pouzdanost });
      } else {
        saved = await newsApi.create({
          naslov: naslov.trim(), sadrzaj: sadrzaj.trim(), kategorija, pouzdanost,
          ...(imageUrl.trim() ? { imageUrl: imageUrl.trim() } : {}),
          ...(originalUrl.trim() ? { originalUrl: originalUrl.trim() } : {})
        });
      }
      onSaved(saved);
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Vest nije sacuvana.'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="border-x border-b border-slate-200 bg-slate-50 p-4 sm:p-5" onSubmit={submit}>
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-[11px] font-extrabold uppercase text-brand">Redakcija</p>
          <h2 className="mt-1 text-base font-extrabold text-slate-950">{existing ? 'Izmeni vest' : 'Objavi vest'}</h2>
        </div>
        <button aria-label="Zatvori editor" className="grid size-10 place-items-center rounded text-slate-500 hover:bg-white hover:text-slate-900" onClick={onClose} type="button"><X size={18} /></button>
      </div>

      {!existing && (
        <div aria-label="Tip objave" className="mt-4 inline-flex rounded border border-slate-300 bg-white p-1" role="group">
          <button aria-pressed={mode === 'article'} className={`min-h-9 rounded px-4 text-xs font-bold ${mode === 'article' ? 'bg-ink text-white' : 'text-slate-600'}`} onClick={() => setMode('article')} type="button">Clanak</button>
          <button aria-pressed={mode === 'x'} className={`min-h-9 rounded px-4 text-xs font-bold ${mode === 'x' ? 'bg-ink text-white' : 'text-slate-600'}`} onClick={() => setMode('x')} type="button">X objava</button>
        </div>
      )}

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <label className="grid gap-1 text-xs font-bold text-slate-600 sm:col-span-2">
          Naslov
          <input aria-label="Naslov" className={fieldClass} maxLength={250} onChange={(event) => setNaslov(event.target.value)} required value={naslov} />
        </label>
        <label className="grid gap-1 text-xs font-bold text-slate-600">
          Kategorija
          <select className={fieldClass} onChange={(event) => setKategorija(event.target.value as NewsCategory)} value={kategorija}>
            <option value="premier_league">Premier liga</option><option value="transferi">Transferi</option><option value="fpl">FPL</option><option value="klubovi">Klubovi</option>
          </select>
        </label>
        <label className="grid gap-1 text-xs font-bold text-slate-600">
          Pouzdanost
          <select className={fieldClass} onChange={(event) => setPouzdanost(event.target.value as NewsReliability)} value={pouzdanost}>
            <option value="zvanicno">Zvanicno</option><option value="pouzdan_izvor">Pouzdan izvor</option><option value="glasina">Glasina</option><option value="fpl_analiza">FPL analiza</option>
          </select>
        </label>
        {mode === 'article' ? (
          <>
            <label className="grid gap-1 text-xs font-bold text-slate-600 sm:col-span-2">
              Sadrzaj
              <textarea className={`${fieldClass} min-h-36 resize-y`} maxLength={20000} onChange={(event) => setSadrzaj(event.target.value)} required value={sadrzaj} />
            </label>
            <label className="grid gap-1 text-xs font-bold text-slate-600">Originalni URL<input className={fieldClass} onChange={(event) => setOriginalUrl(event.target.value)} placeholder="https://..." type="url" value={originalUrl} /></label>
            <label className="grid gap-1 text-xs font-bold text-slate-600">URL slike<input className={fieldClass} onChange={(event) => setImageUrl(event.target.value)} placeholder="https://..." type="url" value={imageUrl} /></label>
          </>
        ) : (
          <label className="grid gap-1 text-xs font-bold text-slate-600 sm:col-span-2">
            X URL
            <input aria-label="X URL" className={fieldClass} disabled={Boolean(existing)} onChange={(event) => setXUrl(event.target.value)} placeholder="https://x.com/korisnik/status/123" required value={xUrl} />
          </label>
        )}
      </div>
      {error && <p className="mt-4 border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-700">{error}</p>}
      <button className="mt-4 inline-flex min-h-10 items-center gap-2 rounded bg-brand px-4 text-sm font-extrabold text-white disabled:opacity-50" disabled={submitting} type="submit">
        <Send size={15} /> {submitting ? 'Cuvanje...' : existing ? 'Sacuvaj izmene' : 'Objavi vest'}
      </button>
    </form>
  );
}

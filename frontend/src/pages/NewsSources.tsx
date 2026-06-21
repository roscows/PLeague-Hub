import { Activity, ArrowLeft, MoreVertical, Plus, RefreshCw, X } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { RelativeTime } from '../components/RelativeTime';
import { getApiErrorMessage } from '../services/apiError';
import { newsApi } from '../services/newsApi';
import type { NewsCategory, NewsReliability, NewsSource, NewsSourceRequest } from '../types/api';

const emptyRequest: NewsSourceRequest = {
  naziv: '', feedUrl: '', siteUrl: '', podrazumevanaKategorija: 'premier_league',
  podrazumevanaPouzdanost: 'pouzdan_izvor', ukljuceniPojmovi: [], iskljuceniPojmovi: [], aktivan: false
};

export function NewsSourcesPage() {
  const [sources, setSources] = useState<NewsSource[]>([]);
  const [editing, setEditing] = useState<NewsSource | 'new' | null>(null);
  const [pauseTarget, setPauseTarget] = useState<NewsSource | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setSources(await newsApi.listSources());
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Izvori trenutno nisu dostupni.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

  async function run(id: string, operation: () => Promise<unknown>, success?: string) {
    setBusyId(id);
    setError(null);
    setNotice(null);
    try {
      await operation();
      if (success) setNotice(success);
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Akcija nad izvorom nije uspela.'));
    } finally {
      setBusyId(null);
    }
  }

  async function sync(source: NewsSource) {
    setBusyId(source.id);
    setError(null);
    try {
      const result = await newsApi.syncSource(source.id);
      setNotice(result.success
        ? `${source.naziv}: ${result.created} nove, ${result.duplicates} duplikata, ${result.promoted} unapredjeno, ${result.skipped} preskoceno.`
        : `${source.naziv}: ${result.error ?? 'Provera nije uspela.'}`);
      await load();
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Provera izvora nije uspela.'));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section className="min-w-0">
      <header className="flex min-h-20 flex-wrap items-center justify-between gap-3 border-b-2 border-brand bg-white px-4 py-4">
        <div>
          <Link className="inline-flex items-center gap-1 text-xs font-bold text-slate-500 hover:text-brand" to="/news"><ArrowLeft size={13} /> Vesti</Link>
          <h1 className="mt-1 text-xl font-extrabold text-slate-950">Izvori vesti</h1>
        </div>
        <button className="inline-flex min-h-10 items-center gap-2 rounded bg-brand px-3 text-sm font-extrabold text-white" onClick={() => setEditing('new')} type="button"><Plus size={15} /> Dodaj izvor</button>
      </header>

      {editing && (
        <SourceForm
          source={editing === 'new' ? undefined : editing}
          onCancel={() => setEditing(null)}
          onSaved={async () => { setEditing(null); setNotice('Izvor je sacuvan.'); await load(); }}
        />
      )}
      {error && <p className="border-x border-b border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">{error}</p>}
      {notice && <p className="border-x border-b border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">{notice}</p>}

      {loading ? (
        <div className="border border-slate-200 bg-white p-10 text-center text-sm text-slate-500">Ucitavanje izvora...</div>
      ) : sources.length === 0 ? (
        <div className="border border-slate-200 bg-white p-12 text-center text-slate-500"><Activity className="mx-auto mb-2 text-slate-300" /><p className="text-sm">Nema konfigurisanih izvora.</p></div>
      ) : (
        <div className="max-w-full overflow-x-auto border-x border-b border-slate-200 bg-white">
          <table className="w-full min-w-[820px] border-collapse text-left text-xs">
            <thead className="bg-slate-100 font-extrabold uppercase text-slate-500">
              <tr><th className="px-3 py-2">Izvor</th><th className="px-3 py-2">Status</th><th className="px-3 py-2">Oznaka</th><th className="px-3 py-2">Kategorija</th><th className="px-3 py-2">Poslednji uspeh</th><th className="px-3 py-2 text-center">Greske</th><th className="w-12 px-2 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {sources.map((source) => (
                <tr className="align-top hover:bg-slate-50" key={source.id}>
                  <td className="max-w-64 px-3 py-3"><strong className="block text-sm text-slate-900">{source.naziv}</strong><span className="mt-1 block truncate text-slate-400" title={source.feedUrl}>{source.feedUrl}</span></td>
                  <td className="px-3 py-3"><span className={`inline-flex rounded px-2 py-1 font-bold ${source.aktivan ? 'bg-emerald-50 text-emerald-800' : 'bg-slate-200 text-slate-600'}`}>{source.aktivan ? 'Aktivan' : 'Pauziran'}</span>{source.pauziranRazlog && <span className="mt-1 block max-w-48 text-[10px] leading-4 text-red-700">{source.pauziranRazlog}</span>}</td>
                  <td className="px-3 py-3 font-semibold text-slate-700">{reliabilityLabel(source.podrazumevanaPouzdanost)}</td>
                  <td className="px-3 py-3 font-semibold text-slate-700">{categoryLabel(source.podrazumevanaKategorija)}</td>
                  <td className="px-3 py-3 text-slate-500">{source.poslednjiUspehAt ? <RelativeTime value={source.poslednjiUspehAt} /> : 'Nikad'}</td>
                  <td className={`px-3 py-3 text-center font-extrabold ${source.uzastopneGreske ? 'text-red-700' : 'text-slate-500'}`}>{source.uzastopneGreske}</td>
                  <td className="relative px-2 py-2">
                    <details className="group relative">
                      <summary aria-label={`Akcije za ${source.naziv}`} className="grid size-9 cursor-pointer list-none place-items-center rounded hover:bg-slate-200"><MoreVertical size={16} /></summary>
                      <div className="absolute right-0 z-20 mt-1 w-44 border border-slate-200 bg-white p-1 shadow-lg">
                        <ActionButton disabled={busyId === source.id} label={`Proveri izvor ${source.naziv}`} onClick={() => sync(source)} text="Proveri izvor" />
                        <ActionButton label={`Izmeni izvor ${source.naziv}`} onClick={() => setEditing(source)} text="Izmeni" />
                        {source.aktivan
                          ? <ActionButton label={`Pauziraj izvor ${source.naziv}`} onClick={() => setPauseTarget(source)} text="Pauziraj" />
                          : <ActionButton label={`Nastavi izvor ${source.naziv}`} onClick={() => run(source.id, () => newsApi.resumeSource(source.id), 'Izvor je nastavljen.')} text="Nastavi" />}
                        <ActionButton danger label={`Deaktiviraj izvor ${source.naziv}`} onClick={() => {
                          if (window.confirm(`Deaktivirati izvor ${source.naziv}?`)) void run(source.id, () => newsApi.deactivateSource(source.id), 'Izvor je deaktiviran.');
                        }} text="Deaktiviraj" />
                      </div>
                    </details>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {pauseTarget && (
        <PauseDialog source={pauseTarget} onClose={() => setPauseTarget(null)} onSubmit={(reason) => run(
          pauseTarget.id, () => newsApi.pauseSource(pauseTarget.id, reason), 'Izvor je pauziran.'
        ).then(() => setPauseTarget(null))} />
      )}
    </section>
  );
}

function ActionButton({ label, text, onClick, disabled, danger }: { label: string; text: string; onClick: () => void; disabled?: boolean; danger?: boolean }) {
  return <button aria-label={label} className={`block min-h-9 w-full px-3 text-left text-xs font-bold hover:bg-slate-100 ${danger ? 'text-red-700' : 'text-slate-700'}`} disabled={disabled} onClick={onClick} type="button">{text}</button>;
}

function SourceForm({ source, onCancel, onSaved }: { source?: NewsSource; onCancel: () => void; onSaved: () => Promise<void> }) {
  const [value, setValue] = useState<NewsSourceRequest>(source ? {
    naziv: source.naziv, feedUrl: source.feedUrl, siteUrl: source.siteUrl,
    podrazumevanaKategorija: source.podrazumevanaKategorija,
    podrazumevanaPouzdanost: source.podrazumevanaPouzdanost,
    ukljuceniPojmovi: source.ukljuceniPojmovi, iskljuceniPojmovi: source.iskljuceniPojmovi, aktivan: source.aktivan
  } : emptyRequest);
  const [include, setInclude] = useState(value.ukljuceniPojmovi.join(', '));
  const [exclude, setExclude] = useState(value.iskljuceniPojmovi.join(', '));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const input = 'min-h-10 rounded border border-slate-300 bg-white px-3 text-sm font-normal outline-none focus:border-brand';

  async function submit(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError(null);
    const request = { ...value, ukljuceniPojmovi: terms(include), iskljuceniPojmovi: terms(exclude) };
    try {
      if (source) await newsApi.updateSource(source.id, request); else await newsApi.createSource(request);
      await onSaved();
    } catch (requestError) { setError(getApiErrorMessage(requestError, 'Izvor nije sacuvan.')); }
    finally { setSaving(false); }
  }

  return (
    <form className="border-x border-b border-slate-200 bg-slate-50 p-4" onSubmit={submit}>
      <div className="flex items-center justify-between"><h2 className="font-extrabold">{source ? 'Izmeni izvor' : 'Novi RSS izvor'}</h2><button aria-label="Zatvori formu izvora" className="grid size-9 place-items-center" onClick={onCancel} type="button"><X size={17} /></button></div>
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <Field label="Naziv"><input className={input} required value={value.naziv} onChange={(event) => setValue({ ...value, naziv: event.target.value })} /></Field>
        <Field label="Feed URL"><input className={input} required type="url" value={value.feedUrl} onChange={(event) => setValue({ ...value, feedUrl: event.target.value })} /></Field>
        <Field label="Sajt"><input className={input} required type="url" value={value.siteUrl} onChange={(event) => setValue({ ...value, siteUrl: event.target.value })} /></Field>
        <Field label="Kategorija"><select className={input} value={value.podrazumevanaKategorija} onChange={(event) => setValue({ ...value, podrazumevanaKategorija: event.target.value as NewsCategory })}><option value="premier_league">Premier liga</option><option value="transferi">Transferi</option><option value="fpl">FPL</option><option value="klubovi">Klubovi</option></select></Field>
        <Field label="Pouzdanost"><select className={input} value={value.podrazumevanaPouzdanost} onChange={(event) => setValue({ ...value, podrazumevanaPouzdanost: event.target.value as NewsReliability })}><option value="zvanicno">Zvanicno</option><option value="pouzdan_izvor">Pouzdan izvor</option><option value="glasina">Glasina</option><option value="fpl_analiza">FPL analiza</option></select></Field>
        <Field label="Ukljuceni pojmovi"><input className={input} placeholder="arsenal, premier league" value={include} onChange={(event) => setInclude(event.target.value)} /></Field>
        <Field label="Iskljuceni pojmovi"><input className={input} placeholder="women, academy" value={exclude} onChange={(event) => setExclude(event.target.value)} /></Field>
        <label className="flex min-h-10 items-center gap-2 text-xs font-bold text-slate-600"><input checked={value.aktivan} onChange={(event) => setValue({ ...value, aktivan: event.target.checked })} type="checkbox" /> Aktiviraj odmah</label>
      </div>
      {error && <p className="mt-3 text-sm font-semibold text-red-700">{error}</p>}
      <button className="mt-4 min-h-10 rounded bg-brand px-4 text-sm font-extrabold text-white disabled:opacity-50" disabled={saving} type="submit">{saving ? 'Cuvanje...' : 'Sacuvaj izvor'}</button>
    </form>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="grid gap-1 text-xs font-bold text-slate-600">{label}{children}</label>; }
function terms(value: string) { return value.split(',').map((item) => item.trim()).filter(Boolean); }
function reliabilityLabel(value: NewsReliability) { return ({ zvanicno: 'Zvanicno', pouzdan_izvor: 'Pouzdan izvor', glasina: 'Glasina', fpl_analiza: 'FPL analiza' })[value]; }
function categoryLabel(value: NewsCategory) { return ({ premier_league: 'Premier liga', transferi: 'Transferi', fpl: 'FPL', klubovi: 'Klubovi' })[value]; }

function PauseDialog({ source, onClose, onSubmit }: { source: NewsSource; onClose: () => void; onSubmit: (reason: string) => Promise<void> }) {
  const [reason, setReason] = useState('');
  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/50 p-4" role="presentation">
      <form className="w-full max-w-md rounded border border-slate-200 bg-white p-5 shadow-xl" onSubmit={(event) => { event.preventDefault(); void onSubmit(reason); }}>
        <h2 className="font-extrabold">Pauziraj {source.naziv}</h2>
        <label className="mt-4 grid gap-1 text-xs font-bold text-slate-600">Razlog<textarea className="min-h-24 rounded border border-slate-300 p-3 text-sm font-normal" required value={reason} onChange={(event) => setReason(event.target.value)} /></label>
        <div className="mt-4 flex justify-end gap-2"><button className="min-h-10 rounded border border-slate-300 px-4 text-sm font-bold" onClick={onClose} type="button">Odustani</button><button className="min-h-10 rounded bg-brand px-4 text-sm font-bold text-white" type="submit">Pauziraj</button></div>
      </form>
    </div>
  );
}

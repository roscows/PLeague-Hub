import { ExternalLink } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

interface TwitterWidgets {
  createTweet: (id: string, container: HTMLElement, options: { dnt: boolean; theme: 'light' }) => Promise<HTMLElement | undefined>;
}

declare global {
  interface Window {
    twttr?: { widgets: TwitterWidgets };
  }
}

let scriptPromise: Promise<TwitterWidgets> | null = null;

function loadWidgets() {
  if (window.twttr?.widgets) return Promise.resolve(window.twttr.widgets);
  if (scriptPromise) return scriptPromise;

  scriptPromise = new Promise<TwitterWidgets>((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-pleague-x-widgets]');
    const script = existing ?? document.createElement('script');
    const onLoad = () => window.twttr?.widgets ? resolve(window.twttr.widgets) : reject(new Error('X widget nije dostupan.'));
    script.addEventListener('load', onLoad, { once: true });
    script.addEventListener('error', () => reject(new Error('X widget nije ucitan.')), { once: true });

    if (!existing) {
      script.src = 'https://platform.twitter.com/widgets.js';
      script.async = true;
      script.dataset.pleagueXWidgets = 'true';
      document.head.appendChild(script);
    }
  }).catch((error) => {
    scriptPromise = null;
    throw error;
  });
  return scriptPromise;
}

function statusId(value: string) {
  try {
    const url = new URL(value);
    const match = url.pathname.match(/^\/[^/]+\/status\/(\d+)\/?$/);
    if (url.protocol !== 'https:' || !['x.com', 'www.x.com'].includes(url.hostname) || !match) return null;
    return match[1];
  } catch {
    return null;
  }
}

export function XEmbed({ url }: { url: string }) {
  const container = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const id = statusId(url);
    const host = container.current;
    if (!id || !host) {
      setFailed(true);
      return;
    }

    setFailed(false);
    const mount = document.createElement('div');
    host.replaceChildren(mount);
    let active = true;
    const timeout = window.setTimeout(() => active && setFailed(true), 8_000);
    loadWidgets()
      .then((widgets) => widgets.createTweet(id, mount, { dnt: true, theme: 'light' }))
      .then((element) => {
        if (active && !element) setFailed(true);
      })
      .catch(() => active && setFailed(true))
      .finally(() => window.clearTimeout(timeout));
    return () => {
      active = false;
      window.clearTimeout(timeout);
      if (mount.parentElement === host) mount.remove();
    };
  }, [url]);

  if (failed) {
    return (
      <div className="mx-auto w-full max-w-xl border border-slate-200 bg-slate-50 px-4 py-8 text-center">
        <p className="text-sm font-semibold text-slate-600">X objava trenutno nije dostupna za prikaz.</p>
        <a className="mt-3 inline-flex items-center gap-2 text-sm font-bold text-brand" href={url} rel="noreferrer" target="_blank">
          Otvori objavu na X <ExternalLink size={14} />
        </a>
      </div>
    );
  }

  return <div aria-label="X objava" className="mx-auto min-h-32 w-full max-w-xl overflow-hidden" ref={container} />;
}

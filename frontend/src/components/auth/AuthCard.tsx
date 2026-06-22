import type { ReactNode } from 'react';

interface AuthCardProps {
  eyebrow: string;
  title: string;
  children: ReactNode;
  footer: ReactNode;
}

export function AuthCard({ eyebrow, title, children, footer }: AuthCardProps) {
  return (
    <section className="mx-auto max-w-md overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <header className="bg-ink px-6 py-5 text-white">
        <p className="text-[10px] font-bold uppercase text-red-400">{eyebrow}</p>
        <h1 className="mt-1 text-xl font-extrabold">{title}</h1>
      </header>
      <div className="p-6">{children}</div>
      <footer className="border-t border-slate-200 bg-slate-50 px-6 py-4 text-center text-sm text-slate-600">
        {footer}
      </footer>
    </section>
  );
}

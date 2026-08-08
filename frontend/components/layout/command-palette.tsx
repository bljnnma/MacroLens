'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Search } from 'lucide-react';
import { Link } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import { ScoreBadge } from '@/components/data/score-badge';
import type { Bias } from '@/lib/mock/types';

export interface SearchItem {
  symbol: string;
  name: string;
  score: number;
  bias: Bias;
}

/**
 * ⌘K rather than an inline input: professionals expect a palette that jumps to
 * anything, and it keeps 240px of topbar free for the data underneath.
 */
export function CommandPalette({ items }: { items: SearchItem[] }) {
  const t = useTranslations();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOpen((v) => !v);
      }
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items.slice(0, 6);
    return items
      .filter((i) => i.symbol.toLowerCase().includes(q) || i.name.toLowerCase().includes(q))
      .slice(0, 8);
  }, [items, query]);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="group flex h-9 w-full min-w-0 max-w-[320px] shrink items-center gap-2.5 rounded-lg border border-line bg-surface px-3 text-left transition-ui hover:border-line-strong"
      >
        <Search className="size-4 shrink-0 text-fg-subtle" aria-hidden />
        <span className="min-w-0 flex-1 truncate text-[13px] text-fg-subtle">
          {t('topbar.searchPlaceholder')}
        </span>
        <kbd className="hidden shrink-0 rounded border border-line bg-surface-2 px-1.5 py-0.5 font-mono text-[10px] text-fg-subtle sm:block">
          ⌘K
        </kbd>
      </button>

      {open && (
        <div className="fixed inset-0 z-[60] flex items-start justify-center px-4 pt-[12vh]">
          <div className="absolute inset-0 bg-black/60" onClick={() => setOpen(false)} aria-hidden />
          <div className="relative w-full max-w-lg overflow-hidden rounded-xl border border-line-strong bg-surface shadow-2xl shadow-black/25">
            <div className="flex items-center gap-3 border-b border-line px-4">
              <Search className="size-4 shrink-0 text-fg-subtle" aria-hidden />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t('topbar.searchPlaceholder')}
                className="h-12 flex-1 bg-transparent text-[14px] text-fg placeholder:text-fg-subtle focus:outline-none"
              />
            </div>
            <div className="max-h-80 overflow-y-auto p-2">
              {results.length === 0 && (
                <p className="px-3 py-6 text-center text-[13px] text-fg-muted">
                  {t('states.emptyTitle')}
                </p>
              )}
              {results.map((item) => (
                <Link
                  key={item.symbol}
                  href={`/assets/${item.symbol.toLowerCase()}`}
                  onClick={() => setOpen(false)}
                  className={cn(
                    'flex items-center justify-between gap-4 rounded-lg px-3 py-2.5 transition-ui hover:bg-surface-2'
                  )}
                >
                  <span className="min-w-0">
                    <span className="block font-mono text-[13px] font-medium text-fg">
                      {item.symbol}
                    </span>
                    <span className="block truncate text-[12px] text-fg-muted">{item.name}</span>
                  </span>
                  <ScoreBadge score={item.score} bias={item.bias} size="sm" showBar={false} />
                </Link>
              ))}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

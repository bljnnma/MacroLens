'use client';

import { useTranslations } from 'next-intl';
import {
  CalendarClock,
  Grid3x3,
  LayoutDashboard,
  LineChart,
  Settings,
  BookOpen,
} from 'lucide-react';
import { Link, usePathname } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import type { EngineStatus } from './shell-frame';

const NAV = [
  { key: 'dashboard', href: '/', icon: LayoutDashboard },
  { key: 'markets', href: '/markets', icon: LineChart },
  { key: 'heatmap', href: '/heatmap', icon: Grid3x3 },
  { key: 'calendar', href: '/calendar', icon: CalendarClock },
  { key: 'indicators', href: '/indicators', icon: BookOpen },
] as const;

const DOT: Record<EngineStatus['freshness'], string> = {
  fresh: 'bg-pos',
  aging: 'bg-warn',
  stale: 'bg-neg',
};

export function Sidebar({
  engine,
  onNavigate,
  forceExpanded = false,
}: {
  engine: EngineStatus;
  onNavigate?: () => void;
  forceExpanded?: boolean;
}) {
  const t = useTranslations();
  const pathname = usePathname();
  const expanded = forceExpanded ? 'flex' : 'hidden xl:flex';
  const expandedInline = forceExpanded ? 'inline' : 'hidden xl:inline';

  const isActive = (href: string) =>
    href === '/' ? pathname === '/' : pathname.startsWith(href);

  return (
    <div className="flex h-full flex-col border-r border-line bg-surface">
      <div className="flex h-14 items-center gap-2.5 border-b border-line px-4 xl:px-5">
        <div className="flex size-7 shrink-0 items-center justify-center rounded-md bg-accent/15 text-accent">
          <span className="text-[13px] font-bold">M</span>
        </div>
        <span className={cn('text-[14px] font-semibold tracking-tight', expandedInline)}>
          {t('brand.name')}
        </span>
      </div>

      <nav className="flex-1 space-y-1 p-2 xl:p-3">
        <p
          className={cn(
            'px-2 pb-1 pt-2 text-[10px] font-semibold uppercase tracking-widest text-fg-subtle',
            expanded
          )}
        >
          {t('nav.sectionMain')}
        </p>
        {NAV.map(({ key, href, icon: Icon }) => {
          const active = isActive(href);
          return (
            <Link
              key={key}
              href={href}
              onClick={onNavigate}
              title={t(`nav.${key}`)}
              className={cn(
                'relative flex items-center gap-3 rounded-lg px-2.5 py-2 text-[13px] font-medium transition-ui',
                active
                  ? 'bg-surface-2 text-fg'
                  : 'text-fg-muted hover:bg-surface-2 hover:text-fg'
              )}
            >
              {active && (
                <span className="absolute inset-y-1.5 left-0 w-0.5 rounded-full bg-accent" aria-hidden />
              )}
              <Icon className="size-4 shrink-0" aria-hidden />
              <span className={cn('truncate', expandedInline)}>{t(`nav.${key}`)}</span>
            </Link>
          );
        })}

        <div className="pt-3">
          <Link
            href="/settings"
            onClick={onNavigate}
            title={t('nav.settings')}
            className={cn(
              'relative flex items-center gap-3 rounded-lg px-2.5 py-2 text-[13px] font-medium transition-ui',
              isActive('/settings')
                ? 'bg-surface-2 text-fg'
                : 'text-fg-muted hover:bg-surface-2 hover:text-fg'
            )}
          >
            {isActive('/settings') && (
              <span className="absolute inset-y-1.5 left-0 w-0.5 rounded-full bg-accent" aria-hidden />
            )}
            <Settings className="size-4 shrink-0" aria-hidden />
            <span className={cn('truncate', expandedInline)}>{t('nav.settings')}</span>
          </Link>
        </div>
      </nav>

      {/* Staleness is never hidden: this sits in peripheral vision on every page. */}
      <div className="p-2 xl:p-3">
        <div
          className={cn(
            'rounded-lg border border-line bg-canvas p-3',
            forceExpanded ? '' : 'xl:block hidden'
          )}
        >
          <div className="flex items-center gap-2">
            <span className={cn('size-1.5 rounded-full', DOT[engine.freshness])} aria-hidden />
            <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t('engine.status')}
            </span>
          </div>
          <p className="mt-2 text-[13px] text-fg">{engine.updatedLabel}</p>
          <p data-numeric className="mt-0.5 font-mono text-[11px] text-fg-subtle">
            v{engine.version}
          </p>
        </div>
        <div className={cn('flex justify-center py-2', forceExpanded ? 'hidden' : 'xl:hidden')}>
          <span className={cn('size-2 rounded-full', DOT[engine.freshness])} title={engine.updatedLabel} />
        </div>
      </div>
    </div>
  );
}

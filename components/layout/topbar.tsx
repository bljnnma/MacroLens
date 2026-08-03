'use client';

import type { ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { Moon } from 'lucide-react';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { CommandPalette, type SearchItem } from './command-palette';
import { LocaleSwitcher } from './locale-switcher';

export function Topbar({
  searchItems,
  onOpenMobileNav,
  mobileNavIcon,
}: {
  searchItems: SearchItem[];
  onOpenMobileNav: () => void;
  mobileNavIcon: ReactNode;
}) {
  const t = useTranslations();

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-line bg-canvas/95 px-5 backdrop-blur-sm sm:px-8">
      <button
        type="button"
        onClick={onOpenMobileNav}
        className="rounded-md p-1.5 text-fg-muted transition-ui hover:bg-surface-2 hover:text-fg lg:hidden"
        aria-label="Menu"
      >
        {mobileNavIcon}
      </button>

      <CommandPalette items={searchItems} />

      <div className="ml-auto flex items-center gap-2">
        <LocaleSwitcher />
        <TooltipProvider delayDuration={200}>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                disabled
                className="rounded-md p-2 text-fg-subtle opacity-50"
                aria-label={t('topbar.theme')}
              >
                <Moon className="size-4" />
              </button>
            </TooltipTrigger>
            <TooltipContent>{t('topbar.themeSoon')}</TooltipContent>
          </Tooltip>
        </TooltipProvider>

        <div className="flex items-center gap-2 rounded-lg border border-line bg-surface py-1 pl-1 pr-2.5">
          <span className="flex size-6 items-center justify-center rounded-md bg-surface-3 text-[11px] font-semibold text-fg-muted">
            Б
          </span>
          <span className="hidden text-[13px] text-fg-muted sm:block">Bataa</span>
        </div>
      </div>
    </header>
  );
}

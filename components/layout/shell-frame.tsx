'use client';

import { useState, type ReactNode } from 'react';
import { Menu } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Sidebar } from './sidebar';
import { Topbar } from './topbar';
import type { SearchItem } from './command-palette';

export interface EngineStatus {
  updatedLabel: string;
  version: string;
  freshness: 'fresh' | 'aging' | 'stale';
}

export function ShellFrame({
  children,
  engine,
  searchItems,
}: {
  children: ReactNode;
  engine: EngineStatus;
  searchItems: SearchItem[];
}) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <div className="min-h-screen bg-canvas">
      {/* Desktop rail: 240px at >=1280, 64px icon rail at >=1024, drawer below */}
      <div className="hidden lg:fixed lg:inset-y-0 lg:left-0 lg:z-40 lg:block lg:w-16 xl:w-60">
        <Sidebar engine={engine} />
      </div>

      {mobileOpen && (
        <>
          <div
            className="fixed inset-0 z-40 bg-black/60 lg:hidden"
            onClick={() => setMobileOpen(false)}
            aria-hidden
          />
          <div className="fixed inset-y-0 left-0 z-50 w-60 lg:hidden">
            <Sidebar engine={engine} onNavigate={() => setMobileOpen(false)} forceExpanded />
          </div>
        </>
      )}

      <div className={cn('lg:pl-16 xl:pl-60')}>
        <Topbar
          searchItems={searchItems}
          onOpenMobileNav={() => setMobileOpen(true)}
          mobileNavIcon={<Menu className="size-4" />}
        />
        <main className="mx-auto w-full max-w-[1280px] px-5 pb-24 pt-8 sm:px-8">{children}</main>
      </div>
    </div>
  );
}

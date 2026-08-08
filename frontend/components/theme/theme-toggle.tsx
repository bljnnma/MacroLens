'use client';

import { useTranslations } from 'next-intl';
import { Check, Monitor, Moon, Sun } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useTheme, type ThemePreference } from './theme-provider';

const OPTIONS: { value: ThemePreference; icon: typeof Sun }[] = [
  { value: 'light', icon: Sun },
  { value: 'dark', icon: Moon },
  { value: 'system', icon: Monitor },
];

/** Compact trigger for the topbar — keeps the chrome to one icon. */
export function ThemeToggle() {
  const t = useTranslations('theme');
  const { preference, resolved, ready, setPreference } = useTheme();

  // Before the client has read localStorage, render the icon the server would
  // have implied. Colours are already correct by then (the inline script ran);
  // this only avoids a mismatched icon on the first frame.
  const TriggerIcon = !ready ? Monitor : resolved === 'dark' ? Moon : Sun;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t('label')}
          className="rounded-md p-2 text-fg-muted transition-ui hover:bg-surface-2 hover:text-fg"
        >
          <TriggerIcon className="size-4" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        {OPTIONS.map(({ value, icon: Icon }) => (
          <DropdownMenuItem key={value} onSelect={() => setPreference(value)}>
            <Icon className="size-4 shrink-0" aria-hidden />
            <span className="flex-1">{t(value)}</span>
            {ready && preference === value && (
              <Check className="size-3.5 shrink-0 text-accent" aria-hidden />
            )}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Full control for Settings, matching the timezone picker's affordance. */
export function ThemeSegmented() {
  const t = useTranslations('theme');
  const { preference, resolved, ready, setPreference } = useTheme();

  return (
    <div>
      <div className="flex flex-wrap gap-1.5">
        {OPTIONS.map(({ value, icon: Icon }) => (
          <button
            key={value}
            type="button"
            onClick={() => setPreference(value)}
            className={cn(
              'inline-flex items-center gap-2 rounded-lg px-3 py-1.5 text-[13px] font-medium transition-ui',
              ready && preference === value
                ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
                : 'border border-line text-fg-muted hover:bg-surface-2 hover:text-fg'
            )}
          >
            <Icon className="size-3.5" aria-hidden />
            {t(value)}
          </button>
        ))}
      </div>
      {ready && preference === 'system' && (
        <p className="mt-3 text-[12px] text-fg-subtle">
          {t('followingSystem', { theme: t(resolved) })}
        </p>
      )}
    </div>
  );
}

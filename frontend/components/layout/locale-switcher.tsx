'use client';

import { useLocale } from 'next-intl';
import { useParams } from 'next/navigation';
import { usePathname, useRouter } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import { locales, type Locale } from '@/i18n/routing';

/**
 * Switching language preserves the current route — a trader mid-analysis must
 * not be thrown back to the dashboard to read a term in the other language.
 */
export function LocaleSwitcher() {
  const locale = useLocale() as Locale;
  const router = useRouter();
  const pathname = usePathname();
  const params = useParams();

  return (
    <div
      className="flex items-center rounded-lg border border-line bg-surface p-0.5"
      role="group"
      aria-label="Language"
    >
      {locales.map((l) => (
        <button
          key={l}
          type="button"
          onClick={() =>
            router.replace(
              // @ts-expect-error -- pathname is a typed route at runtime
              { pathname, params },
              { locale: l, scroll: false }
            )
          }
          className={cn(
            'rounded-[6px] px-2 py-1 text-[12px] font-semibold uppercase transition-ui',
            locale === l ? 'bg-surface-3 text-fg' : 'text-fg-subtle hover:text-fg-muted'
          )}
        >
          {l}
        </button>
      ))}
    </div>
  );
}

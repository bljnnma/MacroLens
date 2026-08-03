'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';
import { Switch } from '@/components/ui/misc';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';

const ZONES = [
  { id: 'Asia/Ulaanbaatar', label: 'Улаанбаатар (UTC+8)' },
  { id: 'UTC', label: 'UTC' },
  { id: 'America/New_York', label: 'New York (UTC−5)' },
  { id: 'Europe/London', label: 'London (UTC+0)' },
];

export function SettingsPanel({
  sampleIso,
  engineVersion,
  profiles,
  lastCalculation,
}: {
  sampleIso: string;
  engineVersion: string;
  profiles: string[];
  lastCalculation: string;
}) {
  const t = useTranslations();
  const [zone, setZone] = useState('Asia/Ulaanbaatar');
  const [latinCodes, setLatinCodes] = useState(true);

  // Live preview: the setting is understood before it is committed, which
  // matters when the choice shifts every timestamp in the product by 12 hours.
  const preview = new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: zone,
  }).format(new Date(sampleIso));

  return (
    <div className="max-w-[720px] space-y-8">
      <Group title={t('settings.language')} hint={t('settings.languageHint')}>
        <LocaleSwitcher />
      </Group>

      <Group title={t('settings.timezone')} hint={t('settings.timezoneHint')}>
        <div className="flex flex-wrap gap-1.5">
          {ZONES.map((z) => (
            <button
              key={z.id}
              type="button"
              onClick={() => setZone(z.id)}
              className={cn(
                'rounded-lg px-3 py-1.5 text-[13px] font-medium transition-ui',
                zone === z.id
                  ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
                  : 'border border-line text-fg-muted hover:bg-surface-2 hover:text-fg'
              )}
            >
              {z.label}
            </button>
          ))}
        </div>
        <div className="mt-4 flex flex-wrap items-baseline gap-x-3 gap-y-1 rounded-[10px] border border-line bg-canvas px-4 py-3">
          <span className="text-[12px] text-fg-subtle">{t('settings.timezonePreview')}</span>
          <span data-numeric className="text-[13px] font-medium tabular-nums text-fg">
            {preview}
          </span>
        </div>
      </Group>

      <Group title={t('settings.display')} hint={t('settings.displayHint')}>
        <label className="flex items-start justify-between gap-6 rounded-[10px] border border-line bg-canvas px-4 py-3">
          <span className="min-w-0">
            <span className="block text-[13px] text-fg">{t('settings.latinCodes')}</span>
            <span className="mt-1 block text-[12px] text-fg-subtle">
              {t('settings.latinCodesHint')}
            </span>
          </span>
          <Switch checked={latinCodes} onCheckedChange={setLatinCodes} className="mt-0.5" />
        </label>
      </Group>

      <Group title={t('settings.about')} hint={t('settings.aboutHint')}>
        <dl className="divide-y divide-line overflow-hidden rounded-[10px] border border-line bg-canvas">
          <Row label={t('settings.engineVersion')} value={`v${engineVersion}`} mono />
          <Row label={t('settings.activeProfiles')} value={profiles.join(' · ')} />
          <Row label={t('settings.lastCalculation')} value={lastCalculation} />
          <Row label={t('settings.dataSource')} value={t('settings.dataSourceValue')} />
        </dl>
      </Group>
    </div>
  );
}

function Group({
  title,
  hint,
  children,
}: {
  title: string;
  hint: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-[12px] border border-line bg-surface p-5 sm:p-6">
      <h2 className="text-[14px] font-semibold text-fg">{title}</h2>
      <p className="mt-1 text-[13px] text-fg-muted">{hint}</p>
      <div className="mt-5">{children}</div>
    </section>
  );
}

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-3 px-4 py-3">
      <dt className="text-[13px] text-fg-subtle">{label}</dt>
      <dd
        data-numeric
        className={cn('text-[13px] text-fg-muted', mono && 'font-mono tabular-nums')}
      >
        {value}
      </dd>
    </div>
  );
}

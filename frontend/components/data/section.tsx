import type { ReactNode } from 'react';
import { ArrowRight } from 'lucide-react';
import { Link } from '@/i18n/routing';
import { cn } from '@/lib/utils';

export function SectionHeader({
  title,
  hint,
  action,
  className,
}: {
  title: string;
  hint?: string;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex items-end justify-between gap-4', className)}>
      <div className="min-w-0">
        <h2 className="text-[15px] font-semibold leading-tight text-fg">{title}</h2>
        {hint && <p className="mt-1 text-[13px] text-fg-muted">{hint}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export function SectionLink({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-[13px] font-medium text-fg-muted transition-ui hover:bg-surface-2 hover:text-fg"
    >
      {label}
      <ArrowRight className="size-3.5" aria-hidden />
    </Link>
  );
}

export function MetricCard({
  label,
  value,
  detail,
  tone = 'default',
  icon,
}: {
  label: string;
  value: string;
  detail?: string;
  tone?: 'default' | 'pos' | 'neg';
  icon?: ReactNode;
}) {
  return (
    <div className="rounded-[12px] border border-line bg-surface p-5">
      <div className="flex items-center gap-2">
        {icon && <span className="text-fg-subtle">{icon}</span>}
        <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
          {label}
        </span>
      </div>
      <div
        data-numeric
        className={cn(
          'mt-3 text-[22px] font-semibold leading-none tabular-nums',
          tone === 'pos' ? 'text-pos' : tone === 'neg' ? 'text-neg' : 'text-fg'
        )}
      >
        {value}
      </div>
      {detail && <div className="mt-2 text-[13px] text-fg-muted">{detail}</div>}
    </div>
  );
}

export function PageHeader({
  title,
  subtitle,
  subtitleEn,
  action,
}: {
  title: string;
  subtitle?: string;
  subtitleEn?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h1 className="text-2xl font-semibold tracking-[-0.02em] text-fg">{title}</h1>
          {subtitleEn && (
            <span className="text-[13px] font-medium uppercase tracking-wider text-fg-subtle">
              {subtitleEn}
            </span>
          )}
        </div>
        {subtitle && <p className="mt-2 max-w-2xl text-[14px] text-fg-muted">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { CalendarClock, Clock, Zap } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  MINUTES_PER_DAY,
  SESSIONS,
  formatDuration,
  formatUbMinutes,
  minutesSinceUbMidnight,
  overlapRanges,
  sessionStatus,
  type SessionState,
} from '@/lib/sessions';
import { SessionTimeline, type TimelineOverlap } from './session-timeline';

export interface SessionEvent {
  id: string;
  releasedAt: string;
  title: string;
  currency: string;
  importance: 'high' | 'medium' | 'low';
}

const STATE_TONE: Record<SessionState, string> = {
  open: 'bg-pos/10 text-pos ring-1 ring-inset ring-pos/25',
  closed: 'bg-neu/15 text-fg-muted ring-1 ring-inset ring-line-strong',
  weekend: 'bg-neu/15 text-fg-subtle ring-1 ring-inset ring-line-strong',
};

export function SessionsView({
  nowIso,
  events,
}: {
  nowIso: string;
  events: SessionEvent[];
}) {
  const t = useTranslations('sessions');

  // First render matches the server exactly, then the clock goes live on mount.
  // This is the one screen where a frozen timestamp would be a bug, not a nicety.
  const [now, setNow] = useState(() => new Date(nowIso));
  useEffect(() => {
    setNow(new Date());
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);

  const model = useMemo(() => {
    const statuses = SESSIONS.map((s) => ({ session: s, status: sessionStatus(s, now) }));
    const nowMinutes = minutesSinceUbMidnight(now);
    const overlaps = overlapRanges(statuses.map((s) => s.status));

    const eventMinutes = events.map((e) => ({
      ...e,
      minute: minutesSinceUbMidnight(new Date(e.releasedAt)),
    }));

    const withEvents = statuses.map(({ session, status }) => {
      const inWindow = (m: number) =>
        status.wraps
          ? m >= status.openUb || m < status.closeUb - MINUTES_PER_DAY
          : m >= status.openUb && m < status.closeUb;
      return {
        session,
        status,
        events: eventMinutes.filter((e) => inWindow(e.minute)),
      };
    });

    const open = withEvents.filter((s) => s.status.state === 'open');
    const next = [...withEvents]
      .filter((s) => s.status.state !== 'weekend')
      .sort((a, b) => a.status.minutesToBoundary - b.status.minutesToBoundary)[0];

    return { statuses: withEvents, nowMinutes, overlaps, open, next };
  }, [now, events]);

  const clock = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ulaanbaatar',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  }).format(now);

  const timelineOverlaps: TimelineOverlap[] = model.overlaps.map((o) => ({
    from: o.from,
    to: o.to,
    label: o.sessions.map((id) => t(`city.${id}`)).join(' + '),
    major: o.sessions.includes('london') && o.sessions.includes('newyork'),
  }));

  const majorOverlap = model.overlaps.find(
    (o) => o.sessions.includes('london') && o.sessions.includes('newyork')
  );

  return (
    <div className="space-y-8">
      {/* Now strip — answers "what is open right now" before anything else. */}
      <section className="grid grid-cols-1 gap-4 lg:grid-cols-[auto_1fr_auto]">
        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="flex items-center gap-2 text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            <Clock className="size-3.5" aria-hidden />
            {t('nowInUb')}
          </div>
          <div
            data-numeric
            className="mt-3 text-[32px] font-bold leading-none tabular-nums tracking-[-0.03em] text-fg"
          >
            {clock}
          </div>
        </div>

        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('openNow')}
          </div>
          {model.open.length === 0 ? (
            <p className="mt-3 text-[14px] text-fg-muted">{t('noneOpen')}</p>
          ) : (
            <div className="mt-3 flex flex-wrap gap-2">
              {model.open.map(({ session, status }) => {
                const d = formatDuration(status.minutesToBoundary);
                return (
                  <span
                    key={session.id}
                    className="inline-flex items-center gap-2 rounded-lg bg-pos/10 px-3 py-1.5 text-[13px] font-medium text-pos ring-1 ring-inset ring-pos/25"
                  >
                    <span className="size-1.5 rounded-full bg-pos" aria-hidden />
                    {t(`city.${session.id}`)}
                    <span data-numeric className="tabular-nums opacity-70">
                      {t('closesInShort', { hours: d.hours, minutes: d.minutes })}
                    </span>
                  </span>
                );
              })}
            </div>
          )}
        </div>

        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('nextEvent')}
          </div>
          {model.next && (
            <>
              <div className="mt-3 text-[15px] font-medium text-fg">
                {t(`city.${model.next.session.id}`)}
              </div>
              <div data-numeric className="mt-1 text-[13px] tabular-nums text-fg-muted">
                {(() => {
                  const d = formatDuration(model.next.status.minutesToBoundary);
                  return model.next.status.state === 'open'
                    ? t('closesIn', { hours: d.hours, minutes: d.minutes })
                    : t('opensIn', { hours: d.hours, minutes: d.minutes });
                })()}
              </div>
            </>
          )}
        </div>
      </section>

      <SessionTimeline
        rows={model.statuses.map(({ session, status }) => ({
          id: session.id,
          label: t(`city.${session.id}`),
          openUb: status.openUb,
          closeUb: status.closeUb,
          wraps: status.wraps,
          state: status.state,
        }))}
        overlaps={timelineOverlaps}
        nowMinutes={model.nowMinutes}
      />

      {majorOverlap && (
        <section className="rounded-[12px] border border-accent/30 bg-accent/5 p-5 sm:p-6">
          <div className="flex flex-wrap items-start gap-4">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-accent/15 text-accent">
              <Zap className="size-4" aria-hidden />
            </span>
            <div className="min-w-0 flex-1">
              <h3 className="text-[14px] font-semibold text-fg">{t('majorOverlapTitle')}</h3>
              <p className="mt-1.5 max-w-2xl text-[13px] leading-relaxed text-fg-muted">
                {t('majorOverlapBody')}
              </p>
            </div>
            <div
              data-numeric
              className="shrink-0 text-right text-[16px] font-semibold tabular-nums text-accent"
            >
              {formatUbMinutes(majorOverlap.from)}–{formatUbMinutes(majorOverlap.to)}
              <div className="mt-0.5 text-[11px] font-normal uppercase tracking-wider text-fg-subtle">
                UB
              </div>
            </div>
          </div>
        </section>
      )}

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {model.statuses.map(({ session, status, events: sessionEvents }) => {
          const d = formatDuration(status.minutesToBoundary);
          return (
            <div
              key={session.id}
              className={cn(
                'rounded-[12px] border bg-surface p-5',
                status.state === 'open' ? 'border-pos/30' : 'border-line'
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <span className="text-[15px] font-semibold text-fg">
                  {t(`city.${session.id}`)}
                </span>
                <span
                  className={cn(
                    'rounded-[6px] px-2 py-1 text-[11px] font-medium leading-none',
                    STATE_TONE[status.state]
                  )}
                >
                  {t(status.state)}
                </span>
              </div>

              <dl className="mt-4 space-y-2 text-[12px]">
                <Row
                  label={t('ubTime')}
                  value={`${formatUbMinutes(status.openUb)}–${formatUbMinutes(status.closeUb)}`}
                  strong
                />
                <Row
                  label={t('localTime')}
                  value={`${status.localOpen}–${status.localClose}`}
                />
              </dl>

              {status.state !== 'weekend' && (
                <p data-numeric className="mt-4 text-[12px] tabular-nums text-fg-muted">
                  {status.state === 'open'
                    ? t('closesIn', { hours: d.hours, minutes: d.minutes })
                    : t('opensIn', { hours: d.hours, minutes: d.minutes })}
                </p>
              )}

              {/* Ties the session clock back to the macro calendar rather than
                  leaving this as a standalone widget. */}
              <div className="mt-4 flex items-center gap-2 border-t border-line pt-3 text-[12px] text-fg-subtle">
                <CalendarClock className="size-3.5 shrink-0" aria-hidden />
                {sessionEvents.length > 0
                  ? t('eventsToday', { count: sessionEvents.length })
                  : t('noEventsToday')}
              </div>
            </div>
          );
        })}
      </section>
    </div>
  );
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-fg-subtle">{label}</dt>
      <dd
        data-numeric
        className={cn('tabular-nums', strong ? 'font-medium text-fg' : 'text-fg-muted')}
      >
        {value}
      </dd>
    </div>
  );
}

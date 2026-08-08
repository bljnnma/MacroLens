import type { LocalizedText } from '@/lib/mock/types';

export const UB_TIMEZONE = 'Asia/Ulaanbaatar';
export const MINUTES_PER_DAY = 1440;

export type SessionId = 'sydney' | 'tokyo' | 'london' | 'newyork';

export interface ForexSession {
  id: SessionId;
  city: LocalizedText;
  timeZone: string;
  /** Open/close in the session's OWN local time — never a fixed UTC offset. */
  openHour: number;
  closeHour: number;
}

/**
 * Sessions are defined in local wall-clock time against an IANA zone, so DST is
 * derived rather than assumed. London shifts twice a year, New York shifts on a
 * different schedule, and Sydney shifts in the opposite direction — hardcoding
 * UTC offsets would be wrong for roughly a third of the year.
 */
export const SESSIONS: ForexSession[] = [
  {
    id: 'sydney',
    timeZone: 'Australia/Sydney',
    openHour: 7,
    closeHour: 16,
    city: { mn: 'Сидней', en: 'Sydney' },
  },
  {
    id: 'tokyo',
    timeZone: 'Asia/Tokyo',
    openHour: 9,
    closeHour: 18,
    city: { mn: 'Токио', en: 'Tokyo' },
  },
  {
    id: 'london',
    timeZone: 'Europe/London',
    openHour: 8,
    closeHour: 17,
    city: { mn: 'Лондон', en: 'London' },
  },
  {
    id: 'newyork',
    timeZone: 'America/New_York',
    openHour: 8,
    closeHour: 17,
    city: { mn: 'Нью-Йорк', en: 'New York' },
  },
];

/** UTC offset in minutes for an IANA zone at a given instant. */
export function zoneOffsetMinutes(timeZone: string, at: Date): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hour12: false,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(at);

  const get = (type: string) => Number(parts.find((p) => p.type === type)?.value ?? 0);
  const asUtc = Date.UTC(
    get('year'),
    get('month') - 1,
    get('day'),
    get('hour') % 24,
    get('minute'),
    get('second')
  );

  return Math.round((asUtc - at.getTime()) / 60_000);
}

/** Weekday index (0=Sun) as observed in a given zone. */
export function zoneWeekday(timeZone: string, at: Date): number {
  const name = new Intl.DateTimeFormat('en-US', { timeZone, weekday: 'short' }).format(at);
  return ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].indexOf(name);
}

export function minutesSinceUbMidnight(at: Date): number {
  const offset = zoneOffsetMinutes(UB_TIMEZONE, at);
  const shifted = at.getTime() + offset * 60_000;
  const d = new Date(shifted);
  return d.getUTCHours() * 60 + d.getUTCMinutes();
}

export interface SessionWindow {
  id: SessionId;
  /** Open/close expressed as minutes from UB midnight. `close` may exceed 1440. */
  openUb: number;
  closeUb: number;
  /** True when the window runs past UB midnight — London and New York do. */
  wraps: boolean;
  localOpen: string;
  localClose: string;
}

function hhmm(totalMinutes: number): string {
  const m = ((totalMinutes % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
  return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
}

/** Projects each session's local hours onto the UB clock for the given date. */
export function sessionWindow(session: ForexSession, at: Date): SessionWindow {
  const ubOffset = zoneOffsetMinutes(UB_TIMEZONE, at);
  const sessionOffset = zoneOffsetMinutes(session.timeZone, at);
  const shift = ubOffset - sessionOffset;

  const openUb = (((session.openHour * 60 + shift) % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY;
  const duration = (session.closeHour - session.openHour) * 60;
  const closeRaw = openUb + duration;

  return {
    id: session.id,
    openUb,
    closeUb: closeRaw,
    wraps: closeRaw > MINUTES_PER_DAY,
    localOpen: hhmm(session.openHour * 60),
    localClose: hhmm(session.closeHour * 60),
  };
}

export type SessionState = 'open' | 'closed' | 'weekend';

export interface SessionStatus extends SessionWindow {
  state: SessionState;
  /** Minutes until this session's next open (closed) or close (open). */
  minutesToBoundary: number;
}

/**
 * Weekday is read in the SESSION'S own zone, which handles the Friday-night
 * edge correctly: at 02:00 Saturday in Ulaanbaatar, New York is still Friday
 * afternoon and the market is genuinely open.
 */
export function sessionStatus(session: ForexSession, at: Date): SessionStatus {
  const window = sessionWindow(session, at);
  const nowUb = minutesSinceUbMidnight(at);
  const localDay = zoneWeekday(session.timeZone, at);

  const start = window.openUb;
  const end = window.closeUb;
  const isWithin = window.wraps
    ? nowUb >= start || nowUb < end - MINUTES_PER_DAY
    : nowUb >= start && nowUb < end;

  const isWeekday = localDay >= 1 && localDay <= 5;
  const state: SessionState = !isWeekday ? 'weekend' : isWithin ? 'open' : 'closed';

  let minutesToBoundary: number;
  if (isWithin) {
    const closeAt = window.wraps && nowUb < start ? end - MINUTES_PER_DAY : end;
    minutesToBoundary = closeAt - nowUb;
  } else {
    minutesToBoundary = start - nowUb;
    if (minutesToBoundary < 0) minutesToBoundary += MINUTES_PER_DAY;
  }

  return { ...window, state, minutesToBoundary };
}

export interface OverlapRange {
  from: number;
  to: number;
  sessions: SessionId[];
}

/**
 * Contiguous stretches where two or more sessions are simultaneously open.
 * The London/New York overlap is where the day's liquidity concentrates, so it
 * is computed rather than annotated by hand.
 */
export function overlapRanges(windows: SessionWindow[]): OverlapRange[] {
  const openAt = (w: SessionWindow, minute: number) => {
    const end = w.closeUb;
    if (w.wraps) return minute >= w.openUb || minute < end - MINUTES_PER_DAY;
    return minute >= w.openUb && minute < end;
  };

  const ranges: OverlapRange[] = [];
  let current: OverlapRange | null = null;

  for (let m = 0; m < MINUTES_PER_DAY; m += 1) {
    const active = windows.filter((w) => openAt(w, m)).map((w) => w.id);
    if (active.length >= 2) {
      const key = active.join(',');
      if (current && current.sessions.join(',') === key && current.to === m) {
        current.to = m + 1;
      } else {
        if (current) ranges.push(current);
        current = { from: m, to: m + 1, sessions: active };
      }
    } else if (current) {
      ranges.push(current);
      current = null;
    }
  }
  if (current) ranges.push(current);

  // Sub-30-minute slivers are DST artefacts, not tradeable windows.
  return ranges.filter((r) => r.to - r.from >= 30);
}

export function formatUbMinutes(minutes: number): string {
  return hhmm(minutes);
}

export function formatDuration(totalMinutes: number): { hours: number; minutes: number } {
  const m = Math.max(0, Math.round(totalMinutes));
  return { hours: Math.floor(m / 60), minutes: m % 60 };
}

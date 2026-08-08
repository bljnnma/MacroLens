'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'theme';

interface ThemeContextValue {
  /** What the user chose. Defaults to `system`. */
  preference: ThemePreference;
  /** What `system` currently evaluates to — drives the toggle's icon. */
  resolved: ResolvedTheme;
  /** False until the client has read storage; used to avoid hydration mismatch. */
  ready: boolean;
  setPreference: (next: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function readStoredPreference(): ThemePreference {
  if (typeof window === 'undefined') return 'system';
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'light' || stored === 'dark' || stored === 'system') return stored;
  } catch {
    /* private mode — fall through to system */
  }
  return 'system';
}

function applyPreference(pref: ThemePreference) {
  const root = document.documentElement;
  // Removing the attribute hands control back to the media query rather than
  // pinning a resolved value, so the page keeps tracking the OS afterwards.
  if (pref === 'system') root.removeAttribute('data-theme');
  else root.setAttribute('data-theme', pref);
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  // Read synchronously so the preference is correct on the very first client
  // render. On the server this is always `system`, which is why consumers gate
  // their rendering on `ready` rather than on this value.
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference);
  const [systemDark, setSystemDark] = useState(false);
  const [ready, setReady] = useState(false);

  /**
   * The DOM attribute lives outside React's control, on an element React owns.
   * Re-asserting it here — on every mount, not just on change — is what makes it
   * survive a locale switch: changing the `[locale]` segment re-renders the root
   * layout from HTML that has no `data-theme`, and without this the theme would
   * silently revert to the system preference.
   */
  useEffect(() => {
    applyPreference(preference);
  }, [preference]);

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    setSystemDark(media.matches);
    setPreferenceState(readStoredPreference());
    setReady(true);

    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    media.addEventListener('change', onChange);

    // Keep tabs in agreement — a theme change in one should not leave another
    // showing a stale toggle state.
    const onStorage = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY) setPreferenceState(readStoredPreference());
    };
    window.addEventListener('storage', onStorage);

    return () => {
      media.removeEventListener('change', onChange);
      window.removeEventListener('storage', onStorage);
    };
  }, []);

  const setPreference = useCallback((next: ThemePreference) => {
    // State only. The effect above is the single writer to the DOM, so there is
    // no path where storage, React state and the attribute can disagree.
    setPreferenceState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      /* preference simply will not persist */
    }
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({
      preference,
      resolved: preference === 'system' ? (systemDark ? 'dark' : 'light') : preference,
      ready,
      setPreference,
    }),
    [preference, systemDark, ready, setPreference]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme must be used inside ThemeProvider');
  return ctx;
}

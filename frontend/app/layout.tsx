import type { ReactNode } from 'react';

// The real <html> / <body> lives in app/[locale]/layout.tsx so `lang` can be
// set per locale. Next requires a root layout to exist, so this is a passthrough.
export default function RootLayout({ children }: { children: ReactNode }) {
  return children;
}

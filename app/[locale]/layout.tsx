import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { notFound } from 'next/navigation';
import { NextIntlClientProvider } from 'next-intl';
import { getMessages, setRequestLocale } from 'next-intl/server';
import { Inter, JetBrains_Mono } from 'next/font/google';

import { routing, type Locale } from '@/i18n/routing';
import { AppShell } from '@/components/layout/app-shell';
import '../globals.css';

// Cyrillic subset is mandatory, not optional: Mongolian needs Ө, Ү and Ё.
// Faces that silently drop them render tofu boxes for the primary audience only.
const inter = Inter({
  subsets: ['latin', 'cyrillic'],
  variable: '--font-inter',
  display: 'swap',
});

const jetbrains = JetBrains_Mono({
  subsets: ['latin'],
  variable: '--font-mono-jb',
  display: 'swap',
  weight: ['400', '500'],
});

export const metadata: Metadata = {
  title: 'MacroScore — Макро оноололтын платформ',
  description:
    'Хамгийн хүчтэй ба сул хөрөнгийг ил тод макро эдийн засгийн оноогоор олоорой.',
};

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export default async function LocaleLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;

  if (!routing.locales.includes(locale as Locale)) {
    notFound();
  }

  setRequestLocale(locale);
  const messages = await getMessages();

  return (
    <html lang={locale} className={`${inter.variable} ${jetbrains.variable}`}>
      <body className="bg-canvas text-fg antialiased">
        <NextIntlClientProvider messages={messages}>
          <AppShell>{children}</AppShell>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}

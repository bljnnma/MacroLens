import { getTranslations, setRequestLocale } from 'next-intl/server';
import { PageHeader } from '@/components/data/section';
import { SimulatorView } from '@/components/simulator/simulator-view';

export default async function SimulatorPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();

  return (
    <div className="space-y-8">
      <PageHeader
        title={t('simulator.title')}
        subtitleEn={t('simulator.subtitleEn')}
        subtitle={t('simulator.subtitle')}
      />
      <SimulatorView />
    </div>
  );
}

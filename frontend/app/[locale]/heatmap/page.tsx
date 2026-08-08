import { getTranslations, setRequestLocale } from 'next-intl/server';
import { fetchHeatmap } from '@/lib/data';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { Heatmap, HeatmapLegend } from '@/components/heatmap/heatmap';

export default async function HeatmapPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const heatmap = await fetchHeatmap(lang);

  return (
    <div className="space-y-8">
      <PageHeader
        title={t('heatmap.title')}
        subtitleEn={t('heatmap.subtitleEn')}
        subtitle={t('heatmap.subtitle')}
      />

      <section>
        <Heatmap
          factors={heatmap.factors.map((f) => ({
            code: f.code,
            label: f.shortName,
            name: f.name,
          }))}
          rows={heatmap.rows.map((r) => ({
            symbol: r.symbol,
            name: r.name,
            score: r.score,
            bias: r.bias,
            cells: r.cells.map((c) => ({
              factorCode: c.factorCode,
              n: c.normalizedScore as never,
              rawLabel: c.rawLabel,
              weight: c.weight,
              contribution: c.contribution,
              inProfile: c.inProfile,
            })),
          }))}
        />
        <HeatmapLegend />
      </section>
    </div>
  );
}

import {notFound} from 'next/navigation';
import {getTranslations} from 'next-intl/server';
import {Link} from '@/i18n/navigation';
import {FiArrowLeft, FiCheckCircle} from 'react-icons/fi';

const featureKeys = ['organized-records', 'appointments', 'data-decisions', 'connected-team'] as const;
type FeatureKey = (typeof featureKeys)[number];

export const instant = false;

export default async function FeatureDetailPage({
  params
}: {
  params: Promise<{locale: string; feature: string}>;
}) {
  const {locale, feature: featureParam} = await params;
  const feature = featureParam as FeatureKey;
  if (!featureKeys.includes(feature)) {
    notFound();
  }

  const t = await getTranslations({locale, namespace: `FeatureDetails.${feature}`});

  return (
    <main className="mx-auto min-h-[70vh] w-[min(1180px,calc(100%-40px))] py-16">
      <Link href="/#features" className="inline-flex items-center gap-2 text-sm font-semibold text-muted hover:text-brand">
        <FiArrowLeft className="size-4 rtl:rotate-180" />
        {t('back')}
      </Link>
      <section className="mt-8 grid gap-10 lg:grid-cols-[1fr_.75fr]">
        <div>
          <span className="text-xs font-extrabold text-brand-strong">{t('eyebrow')}</span>
          <h1 className="mt-3 text-4xl font-black leading-tight sm:text-5xl">{t('title')}</h1>
          <p className="mt-5 max-w-2xl text-base leading-8 text-muted">{t('description')}</p>
          <ul className="mt-8 grid gap-4">
            {[0, 1, 2, 3].map((index) => (
              <li key={index} className="flex items-start gap-3 rounded-xl border border-border bg-surface p-4">
                <FiCheckCircle className="mt-0.5 size-5 shrink-0 text-brand" />
                <div>
                  <strong className="block text-sm">{t(`benefits.${index}.title`)}</strong>
                  <p className="mt-1 text-xs leading-6 text-muted">{t(`benefits.${index}.description`)}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
        <aside className="rounded-3xl border border-brand/20 bg-[#0b1c2f] p-7 text-[#f2f7fb]">
          <h2 className="text-xl font-bold">{t('workflowTitle')}</h2>
          <p className="mt-3 text-sm leading-7 text-[#9aafbf]">{t('workflowDescription')}</p>
          <Link href="/register" className="mt-6 inline-flex min-h-12 items-center rounded-xl bg-brand px-5 text-sm font-extrabold text-[#06211f]">
            {t('cta')}
          </Link>
        </aside>
      </section>
    </main>
  );
}

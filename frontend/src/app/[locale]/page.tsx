import {
  FiCheckCircle,
  FiDatabase,
  FiGlobe,
  FiLayout,
  FiLayers,
  FiType
} from 'react-icons/fi';
import {getLocale, getTranslations} from 'next-intl/server';
import {Header} from '@/components/layout/Header';

const setupItems = ['next', 'i18n', 'rtl', 'font', 'data', 'ui'] as const;

const setupIcons = {
  next: FiLayout,
  i18n: FiGlobe,
  rtl: FiLayers,
  font: FiType,
  data: FiDatabase,
  ui: FiCheckCircle
} as const;

export default async function HomePage() {
  const t = await getTranslations('Home');
  const locale = await getLocale();
  const direction = locale === 'ar' ? t('rtl') : t('ltr');

  return (
    <div className="flex min-h-screen flex-col">
      <Header />

      <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-10 px-4 py-12 sm:px-6 sm:py-16">
        <section className="relative overflow-hidden rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-10">
          <div
            aria-hidden
            className="pointer-events-none absolute -end-24 -top-24 h-64 w-64 rounded-full bg-teal-50 blur-3xl"
          />
          <div className="relative flex flex-col gap-5">
            <span className="inline-flex w-fit items-center gap-2 rounded-full border border-teal-200 bg-teal-50 px-3 py-1 text-xs font-semibold text-teal-800">
              <FiCheckCircle aria-hidden />
              {t('badge')}
            </span>

            <h1 className="max-w-2xl text-3xl font-extrabold leading-tight text-slate-950 sm:text-5xl">
              {t('title')}
            </h1>

            <p className="max-w-2xl text-base leading-8 text-slate-600 sm:text-lg">
              {t('description')}
            </p>

            <dl className="mt-2 flex flex-wrap gap-3 text-sm">
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-2">
                <dt className="text-xs font-medium text-slate-500">
                  {t('localeLabel')}
                </dt>
                <dd className="mt-0.5 font-bold tracking-wide text-slate-900">
                  {locale.toUpperCase()}
                </dd>
              </div>
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-2">
                <dt className="text-xs font-medium text-slate-500">
                  {t('directionLabel')}
                </dt>
                <dd className="mt-0.5 font-bold text-slate-900">{direction}</dd>
              </div>
            </dl>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
          <div className="max-w-3xl">
            <h2 className="text-xl font-bold text-slate-950 sm:text-2xl">
              {t('stackTitle')}
            </h2>
            <p className="mt-2 text-sm leading-7 text-slate-600">
              {t('stackDescription')}
            </p>
          </div>

          <ul className="mt-6 grid gap-3 sm:grid-cols-2">
            {setupItems.map((item) => {
              const Icon = setupIcons[item];

              return (
                <li
                  key={item}
                  className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-slate-50/60 p-4"
                >
                  <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-xl bg-teal-100 text-teal-700">
                    <Icon aria-hidden />
                  </span>
                  <span className="text-sm font-medium leading-6 text-slate-700">
                    {t(`items.${item}`)}
                  </span>
                </li>
              );
            })}
          </ul>

          <p className="mt-6 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm leading-7 text-amber-900">
            {t('note')}
          </p>
        </section>
      </main>
    </div>
  );
}

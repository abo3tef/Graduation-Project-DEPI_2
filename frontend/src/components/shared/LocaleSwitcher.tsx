'use client';

import {useTransition} from 'react';
import {FiGlobe} from 'react-icons/fi';
import {useLocale, useTranslations} from 'next-intl';
import {usePathname, useRouter} from '@/i18n/navigation';
import {routing, type Locale} from '@/i18n/routing';

export function LocaleSwitcher() {
  const t = useTranslations('LocaleSwitcher');
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function switchLocale(nextLocale: Locale) {
    if (nextLocale === locale) {
      return;
    }

    startTransition(() => {
      router.replace(pathname, {locale: nextLocale});
    });
  }

  return (
    <div className="flex items-center gap-2">
      <FiGlobe className="size-4 text-slate-500" aria-hidden />
      <div
        role="group"
        aria-label={t('label')}
        className="flex rounded-full border border-slate-200 bg-slate-50 p-0.5"
      >
        {routing.locales.map((item) => {
          const isActive = item === locale;

          return (
            <button
              key={item}
              type="button"
              onClick={() => switchLocale(item)}
              disabled={isPending}
              aria-pressed={isActive}
              className={`rounded-full px-3 py-1 text-xs font-semibold transition-colors ${
                isActive
                  ? 'bg-teal-600 text-white shadow-sm'
                  : 'text-slate-600 hover:bg-white hover:text-slate-900'
              } disabled:cursor-wait disabled:opacity-70`}
            >
              {t(item)}
            </button>
          );
        })}
      </div>
    </div>
  );
}

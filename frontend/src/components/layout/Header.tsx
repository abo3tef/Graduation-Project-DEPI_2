import {FiActivity} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {LocaleSwitcher} from '@/components/shared/LocaleSwitcher';

export function Header() {
  const t = useTranslations('Navigation');

  return (
    <header className="border-b border-slate-200 bg-white/90 backdrop-blur">
      <div className="mx-auto flex w-full max-w-5xl items-center justify-between gap-4 px-4 py-4 sm:px-6">
        <div className="flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-xl bg-teal-600 text-white">
            <FiActivity className="size-5" aria-hidden />
          </span>
          <div className="leading-tight">
            <p className="text-base font-extrabold text-slate-950">
              {t('brand')}
            </p>
            <p className="text-xs font-medium text-slate-500">
              {t('tagline')}
            </p>
          </div>
        </div>

        <LocaleSwitcher />
      </div>
    </header>
  );
}

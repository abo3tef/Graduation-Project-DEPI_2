import {FiActivity, FiArrowUpRight} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';

export function Footer() {
  const t = useTranslations('Footer');

  return (
    <footer className="relative mx-auto mb-[18px] w-[min(1180px,calc(100%-40px))] overflow-hidden rounded-[25px] bg-[#071522] px-14 pb-6 pt-13 text-[#f2f7fb] max-sm:px-6 max-sm:py-10" id="contact">
      <div className="pointer-events-none absolute -left-24 -top-28 size-[310px] rounded-full bg-[#2dd4bf25] blur-[65px]" aria-hidden />
      <div className="relative grid grid-cols-[1.6fr_repeat(3,1fr)] gap-9 max-sm:grid-cols-2 max-sm:gap-x-5 max-sm:gap-y-7">
        <div className="max-sm:col-span-full">
          <Link href="/" className="inline-flex items-center gap-2.5">
            <span className="grid size-9 place-items-center rounded-[10px] bg-brand text-[#06211f]">
              <FiActivity className="size-5" aria-hidden />
            </span>
            <span className="text-base font-black">{t('brand')}</span>
          </Link>
          <p className="mt-4 max-w-[280px] text-[11px] leading-[1.9] text-[#8da3b4]">{t('description')}</p>
        </div>
        <div>
          <h3 className="mb-4 mt-1 text-xs text-[#dce8ee]">{t('platform')}</h3>
          <div className="flex flex-col gap-2.5 text-[11px] text-[#8da3b4]">
            <Link href="/#features" className="transition hover:text-brand">{t('features')}</Link>
            <Link href="/#howItWorks" className="transition hover:text-brand">{t('howItWorks')}</Link>
            <Link href="/#security" className="transition hover:text-brand">{t('security')}</Link>
          </div>
        </div>
        <div>
          <h3 className="mb-4 mt-1 text-xs text-[#dce8ee]">{t('account')}</h3>
          <div className="flex flex-col gap-2.5 text-[11px] text-[#8da3b4]">
            <Link href="/#contact" className="transition hover:text-brand">{t('login')}</Link>
            <Link href="/#contact" className="transition hover:text-brand">{t('register')}</Link>
            <Link href="/#contact" className="transition hover:text-brand">{t('support')}</Link>
          </div>
        </div>
        <div>
          <h3 className="mb-4 mt-1 text-xs text-[#dce8ee]">{t('resources')}</h3>
          <div className="flex flex-col gap-2.5 text-[11px] text-[#8da3b4]">
            <Link href="/#features" className="transition hover:text-brand">{t('doctors')}</Link>
            <Link href="/#features" className="transition hover:text-brand">{t('patients')}</Link>
            <Link href="/#security" className="transition hover:text-brand">{t('privacy')}</Link>
          </div>
        </div>
      </div>
      <div className="relative mt-10 flex justify-between gap-4 border-t border-white/[.07] pt-[18px] text-[10px] text-[#718797] max-sm:flex-col">
        <span>{t('copyright')}</span>
        <span className="inline-flex items-center gap-1">
          {t('project')} <FiArrowUpRight aria-hidden />
        </span>
      </div>
    </footer>
  );
}

import type {ReactNode} from 'react';
import {FiArrowLeft} from 'react-icons/fi';
import {getTranslations} from 'next-intl/server';
import {Link} from '@/i18n/navigation';

type MedicalRecordsLayoutProps = {
  children: ReactNode;
  params: Promise<{locale: string}>;
};

export default async function MedicalRecordsLayout({
  children,
  params
}: MedicalRecordsLayoutProps) {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'MedicalRecords'});

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto w-[min(1100px,calc(100%-32px))] py-8 sm:py-12">
        <nav className="mb-8 flex flex-wrap items-center justify-between gap-3">
          <Link
            href="/"
            className="inline-flex items-center gap-2 text-sm font-semibold text-muted transition-colors hover:text-foreground"
          >
            <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
            {t('common.backHome')}
          </Link>
          <span className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">
            {t('meta.shortLabel')}
          </span>
        </nav>
        {children}
      </div>
    </div>
  );
}

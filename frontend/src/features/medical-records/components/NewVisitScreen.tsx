'use client';

import {FiArrowLeft} from 'react-icons/fi';
import {useSearchParams} from 'next/navigation';
import {useTranslations} from 'next-intl';
import {Link, useRouter} from '@/i18n/navigation';
import {UnifiedVisitForm} from './UnifiedVisitForm';

/**
 * Create-visit screen. The patient is chosen with the shared patient picker (F8); a
 * `?patientId=…` link pre-selects that patient.
 */
export function NewVisitScreen() {
  const t = useTranslations('MedicalRecords.visitForm');
  const commonT = useTranslations('MedicalRecords.common');
  const router = useRouter();
  const searchParams = useSearchParams();

  const initialPatientId = searchParams.get('patientId') ?? '';

  return (
    <main className="flex flex-col gap-6">
      <Link
        href="/medical-records"
        className="inline-flex w-fit items-center gap-2 text-sm font-semibold text-muted transition-colors hover:text-foreground"
      >
        <FiArrowLeft aria-hidden className="size-4 rtl:rotate-180" />
        {commonT('backToWorkspace')}
      </Link>

      <header className="mx-auto w-full max-w-5xl">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('createTitle')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {t('createDescription')}
        </p>
      </header>

      <div className="mx-auto w-full max-w-5xl">
        <section
          aria-labelledby="new-visit-title"
          className="rounded-2xl border border-border bg-surface p-5 sm:p-7"
        >
          <h2 id="new-visit-title" className="sr-only">
            {t('createTitle')}
          </h2>
          <UnifiedVisitForm
            initialPatientId={initialPatientId}
            onSuccess={(visit) =>
              router.push(`/medical-records/visits/${visit.id}`)
            }
          />
        </section>
      </div>
    </main>
  );
}

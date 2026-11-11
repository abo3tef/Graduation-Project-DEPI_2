'use client';

import {FiArrowLeft} from 'react-icons/fi';
import {useSearchParams} from 'next/navigation';
import {useTranslations} from 'next-intl';
import {Link, useRouter} from '@/i18n/navigation';
import {useDevToken} from '../use-dev-token';
import {DevTokenPanel} from './DevTokenPanel';
import {VisitForm} from './VisitForm';

/**
 * Create-visit screen. Until the shared patient picker exists (Q-04) the patient id arrives as
 * `?patientId=…` and is still editable + GUID-validated in the form (D-31).
 */
export function NewVisitScreen() {
  const t = useTranslations('MedicalRecords.visitForm');
  const commonT = useTranslations('MedicalRecords.common');
  const statesT = useTranslations('MedicalRecords.states');
  const router = useRouter();
  const searchParams = useSearchParams();
  const {token, isReady} = useDevToken();

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

      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('createTitle')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {t('createDescription')}
        </p>
      </header>

      <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <section
          aria-labelledby="new-visit-title"
          className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
        >
          <h2 id="new-visit-title" className="sr-only">
            {t('createTitle')}
          </h2>
          <VisitForm
            mode="create"
            initialPatientId={initialPatientId}
            onSuccess={(visit) =>
              router.push(`/medical-records/visits/${visit.id}`)
            }
          />
        </section>

        <aside className="flex flex-col gap-4">
          {isReady && !token ? (
            <p className="rounded-xl border border-border bg-surface-2 px-4 py-3 text-sm text-muted">
              {statesT('tokenRequiredNotice')}
            </p>
          ) : null}
          <DevTokenPanel />
        </aside>
      </div>
    </main>
  );
}

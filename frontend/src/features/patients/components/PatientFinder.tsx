'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import {FiSearch, FiUser} from 'react-icons/fi';
import {Link} from '@/i18n/navigation';
import {useActorGate} from '../hooks/use-actor-gate';
import {usePatientSearch} from '../hooks/use-patient-search';
import {PATIENT_SEARCH_MIN_LENGTH} from '../validation';
import {getPatientErrorKey, type PatientErrorKey} from '../services/patient-errors';
import {Alert} from './primitives/Alert';
import {IdentityGate} from './IdentityGate';
import {SkeletonRows} from './primitives/Skeleton';

/**
 * Entry screen: the acting doctor searches their own patients (F1-C4 lookup,
 * verified server-side scoping) and opens one. No doctor identity — no request.
 */
export function PatientFinder() {
  const t = useTranslations('Patients');
  const {identity, doctorId, ready} = useActorGate();
  const [text, setText] = useState('');
  const search = usePatientSearch(doctorId ?? '', text);

  if (identity.status === 'loading') {
    return <SkeletonRows rows={3} label={t('states.searchingTitle')} />;
  }

  if (identity.status === 'none') {
    return <IdentityGate />;
  }

  if (!ready) {
    return (
      <Alert tone="warning" title={t('states.forbiddenTitle')}>
        {t('states.forbiddenHint')}
      </Alert>
    );
  }

  const results = search.data ?? [];
  const errorKey: PatientErrorKey | null = search.isError
    ? getPatientErrorKey(search.error)
    : null;

  let status: string | null = null;

  if (!search.typedEnough) {
    status = t('finder.minChars', {count: PATIENT_SEARCH_MIN_LENGTH});
  } else if (errorKey !== null) {
    status = t(`errorKey.${errorKey}`);
  } else if (search.isWaiting || search.isFetching) {
    status = t('finder.searching');
  } else if (results.length === 0) {
    status = t('finder.none');
  }

  return (
    <main className="flex flex-col gap-6" data-testid="patient-finder">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('finder.eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('finder.title')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {t('finder.description')}
        </p>
      </header>

      <section
        className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
      >
        <label
          id="patient-search-label"
          htmlFor="patient-search"
          className="text-xs font-bold text-foreground"
        >
          {t('finder.searchLabel')}
        </label>
        <div className="relative mt-2">
          <FiSearch
            aria-hidden="true"
            className="pointer-events-none absolute inset-y-0 start-3 my-auto size-4 text-muted"
          />
          <input
            id="patient-search"
            type="text"
            autoComplete="off"
            value={text}
            placeholder={t('finder.searchPlaceholder')}
            onChange={(event) => setText(event.target.value)}
            className="min-h-10 w-full rounded-lg border border-border bg-background ps-9 pe-3 text-start text-sm text-foreground transition placeholder:text-muted focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand"
            dir="auto"
          />
        </div>

        {search.isPending && search.typedEnough ? (
          <div className="mt-4">
            <SkeletonRows rows={2} label={t('states.searchingTitle')} />
          </div>
        ) : null}

        {results.length > 0 ? (
          <ul
            aria-label={t('common.results')}
            className="mt-4 m-0 grid list-none gap-2 p-0"
          >
            {results.map((patient) => (
              <li key={patient.id}>
                <Link
                  href={`/patients/${patient.id}`}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-background px-4 py-3 text-sm transition hover:border-brand hover:bg-brand-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand"
                  data-testid="patient-result"
                >
                  <span className="inline-flex items-center gap-2 font-bold text-foreground" dir="auto">
                    <FiUser aria-hidden="true" className="size-4 text-brand" />
                    {patient.fullName}
                  </span>
                  {patient.phone ? (
                    <span className="text-xs text-muted" dir="ltr">
                      {patient.phone}
                    </span>
                  ) : null}
                  <span className="text-xs font-bold text-brand">
                    {t('finder.openHint')}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        ) : null}

        <p
          className="m-0 mt-3 min-h-5 text-[13px] leading-6 text-muted"
          role="status"
          aria-live="polite"
        >
          {status}
        </p>
      </section>
    </main>
  );
}

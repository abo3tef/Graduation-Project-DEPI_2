'use client';

import {useState} from 'react';
import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {useLocale, useTranslations} from 'next-intl';
import {FiArrowRight, FiClipboard, FiPlusCircle, FiUser} from 'react-icons/fi';
import {PatientSelect, type PatientOption} from '@/features/patients';
import {Link, useRouter} from '@/i18n/navigation';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {listPatientMedicalVisits} from '../api';
import {LoadingPanel, StatePanel} from './AsyncState';

function patientVisitsKey(patientId: string, pageNumber: number, pageSize: number) {
  return ['medical-records', 'patient-visits', patientId, pageNumber, pageSize] as const;
}

function formatVisitDate(value: string, locale: string): string {
  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(locale, {dateStyle: 'medium', timeStyle: 'short'}).format(date);
}

/**
 * Entry point for the medical-records feature: browse a selected patient's real visits and
 * open a visit or the patient's medical profile/timeline. Ordering is supplied by the API.
 */
export function MedicalRecordsWorkspace() {
  const t = useTranslations('MedicalRecords.workspace');
  const locale = useLocale();
  const router = useRouter();
  const [patient, setPatient] = useState<PatientOption | null>(null);
  const [pageNumber, setPageNumber] = useState(1);

  const visitsQuery = useQuery({
    queryKey: patientVisitsKey(patient?.id ?? '', pageNumber, DEFAULT_PAGE_SIZE),
    queryFn: ({signal}) =>
      listPatientMedicalVisits(patient?.id ?? '', pageNumber, DEFAULT_PAGE_SIZE, signal),
    enabled: patient !== null,
    placeholderData: keepPreviousData
  });

  function handlePatientChange(next: PatientOption | null) {
    setPatient(next);
    setPageNumber(1);
  }

  return (
    <main className="flex flex-col gap-6">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('title')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {t('description')}
        </p>
        <Link
          href="/medical-records/visits/new"
          className="mt-4 inline-flex w-fit items-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90"
        >
          <FiPlusCircle aria-hidden className="size-4" />
          {t('createVisit')}
        </Link>
      </header>

      <div className="flex max-w-4xl flex-col gap-6">
        <section
          aria-labelledby="browse-visits-title"
          className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
        >
          <h2
            id="browse-visits-title"
            className="flex items-center gap-2 text-sm font-bold"
          >
            <FiClipboard aria-hidden className="size-4 text-brand" />
            {t('browseVisitsTitle')}
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-muted">
            {t('browseVisitsHint')}
          </p>

          <div className="mt-4 flex flex-col gap-3">
            <label htmlFor="browse-visits-patient" className="text-sm font-medium">
              {t('browseVisitsTitle')}: {t('patientLabel')}
            </label>
            <PatientSelect
              id="browse-visits-patient"
              value={patient}
              onChange={handlePatientChange}
            />
          </div>

          {patient === null ? (
            <p role="status" className="mt-5 text-sm leading-relaxed text-muted">
              {t('selectPatientPrompt')}
            </p>
          ) : null}

          {patient !== null && visitsQuery.isPending ? (
            <div className="mt-5">
              <LoadingPanel label={t('visitsLoading')} />
            </div>
          ) : null}

          {patient !== null && visitsQuery.isError ? (
            <div className="mt-5">
              <StatePanel title={t('visitsErrorTitle')} tone="danger">
                <button
                  type="button"
                  onClick={() => {
                    void visitsQuery.refetch();
                  }}
                  className="inline-flex w-fit items-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-bold text-foreground transition-colors hover:border-brand hover:text-brand"
                >
                  {t('visitsRetry')}
                </button>
              </StatePanel>
            </div>
          ) : null}

          {patient !== null && visitsQuery.isSuccess ? (
            visitsQuery.data.items.length === 0 ? (
              <div className="mt-5">
                <StatePanel title={t('visitsEmpty')} />
              </div>
            ) : (
              <div className="mt-5">
                <p className="text-xs font-semibold text-muted">
                  {t('visitCount', {count: visitsQuery.data.totalCount})}
                </p>

                <div className="mt-2 overflow-x-auto">
                  <table className="w-full border-collapse text-sm">
                    <thead>
                      <tr className="border-b border-border text-xs text-muted">
                        <th scope="col" className="p-2 text-start font-semibold">
                          {t('visitDateLabel')}
                        </th>
                        <th scope="col" className="p-2 text-start font-semibold">
                          {t('chiefComplaintLabel')}
                        </th>
                        <th scope="col" className="p-2 text-start font-semibold">
                          <span className="sr-only">{t('openVisitAction')}</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {visitsQuery.data.items.map((visit) => (
                        <tr key={visit.id} className="border-b border-border/60">
                          <td className="p-2">
                            <time dateTime={visit.visitDate} dir="ltr" className="whitespace-nowrap">
                              {formatVisitDate(visit.visitDate, locale)}
                            </time>
                          </td>
                          <td className="p-2">
                            {visit.chiefComplaint?.trim() ? visit.chiefComplaint : t('notProvided')}
                          </td>
                          <td className="p-2 text-end">
                            <Link
                              href={`/medical-records/visits/${visit.id}`}
                              className="inline-flex items-center gap-1 font-bold text-brand hover:underline"
                            >
                              {t('openVisitAction')}
                              <FiArrowRight aria-hidden className="size-4 rtl:rotate-180" />
                            </Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <nav
                  aria-label={t('browseVisitsTitle')}
                  className="mt-4 flex flex-wrap items-center justify-between gap-3"
                >
                  <button
                    type="button"
                    onClick={() => {
                      setPageNumber((page) => Math.max(1, page - 1));
                    }}
                    disabled={pageNumber <= 1}
                    className="inline-flex w-fit items-center rounded-xl border border-border px-4 py-2 text-sm font-bold text-foreground transition-colors hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {t('previousPage')}
                  </button>
                  <span aria-current="page" className="text-sm font-semibold text-muted">
                    {t('pageStatus', {
                      page: pageNumber,
                      totalPages: visitsQuery.data.totalPages
                    })}
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      setPageNumber((page) => Math.min(visitsQuery.data.totalPages, page + 1));
                    }}
                    disabled={pageNumber >= visitsQuery.data.totalPages}
                    className="inline-flex w-fit items-center rounded-xl border border-border px-4 py-2 text-sm font-bold text-foreground transition-colors hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {t('nextPage')}
                  </button>
                </nav>
              </div>
            )
          ) : null}
        </section>

        <section
          aria-labelledby="open-patient-title"
          className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
        >
          <h2
            id="open-patient-title"
            className="flex items-center gap-2 text-sm font-bold"
          >
            <FiUser aria-hidden className="size-4 text-brand" />
            {t('openPatientTitle')}
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-muted">
            {t('openPatientHint')}
          </p>

          <div className="mt-4 flex flex-col gap-3">
            <label className="text-sm font-medium" htmlFor="open-patient-id">
              {t('patientIdLabel')}
            </label>
            {/* Choosing a patient opens their medical profile straight away. */}
            <PatientSelect
              id="open-patient-id"
              value={null}
              onChange={(profilePatient) => {
                if (profilePatient) {
                  router.push(`/medical-records/patients/${profilePatient.id}`);
                }
              }}
            />
          </div>
        </section>
      </div>
    </main>
  );
}

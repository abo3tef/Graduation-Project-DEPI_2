'use client';

import {useState} from 'react';
import {FiEdit3} from 'react-icons/fi';
import {useFormatter, useTranslations} from 'next-intl';
import type {MedicalVisitResponse} from '../types';
import {VisitForm} from './VisitForm';

type VisitDetailsProps = {
  visit: MedicalVisitResponse;
};

/**
 * Read-only visit summary with an inline edit toggle. The edit form reuses `VisitForm` and the
 * mutation hook seeds the detail query with the returned visit, so the summary updates without
 * a reload.
 */
export function VisitDetails({visit}: VisitDetailsProps) {
  const t = useTranslations('MedicalRecords.visit');
  const formatter = useFormatter();
  const [isEditing, setIsEditing] = useState(false);

  const notProvided = t('notProvided');

  const formatVisitDate = (value: string): string => {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return notProvided;
    }

    return formatter.dateTime(date, {
      dateStyle: 'medium',
      timeStyle: 'short'
    });
  };

  return (
    <section
      aria-labelledby="visit-title"
      className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
            {t('eyebrow')}
          </p>
          <h1 id="visit-title" className="mt-2 text-2xl font-black sm:text-3xl">
            {t('title')}
          </h1>
        </div>

        {!isEditing ? (
          <button
            type="button"
            onClick={() => setIsEditing(true)}
            className="inline-flex items-center gap-2 rounded-xl border border-brand/50 px-4 py-2 text-sm font-semibold text-brand transition-colors hover:bg-brand-soft"
          >
            <FiEdit3 aria-hidden className="size-4" />
            {t('editAction')}
          </button>
        ) : null}
      </div>

      {isEditing ? (
        <div className="mt-5">
          <VisitForm
            mode="edit"
            initialVisit={visit}
            onSuccess={() => setIsEditing(false)}
            onCancel={() => setIsEditing(false)}
          />
        </div>
      ) : (
        <dl className="mt-5 grid gap-5 sm:grid-cols-2">
          <Detail term={t('visitDate')}>
            {formatVisitDate(visit.visitDate)}
          </Detail>
          <Detail term={t('patientId')} mono>
            {visit.patientId}
          </Detail>
          <Detail term={t('appointmentId')} mono>
            {visit.appointmentId ?? t('notLinked')}
          </Detail>
          <Detail term={t('chiefComplaint')} wide>
            {visit.chiefComplaint || notProvided}
          </Detail>
          <Detail term={t('symptoms')} wide>
            {visit.symptoms || notProvided}
          </Detail>
          <Detail term={t('clinicalNotes')} wide>
            {visit.clinicalNotes || notProvided}
          </Detail>
        </dl>
      )}

      {!isEditing ? (
        <p className="mt-4 text-xs text-muted">{t('readOnlyNote')}</p>
      ) : null}
    </section>
  );
}

type DetailProps = {
  term: string;
  children: string;
  mono?: boolean;
  wide?: boolean;
};

function Detail({term, children, mono = false, wide = false}: DetailProps) {
  return (
    <div className={wide ? 'sm:col-span-2' : undefined}>
      <dt className="text-xs font-semibold uppercase tracking-wide text-muted">
        {term}
      </dt>
      <dd
        className={
          mono
            ? 'mt-1 break-all font-mono text-xs text-foreground'
            : 'mt-1 whitespace-pre-line text-sm text-foreground'
        }
      >
        {children}
      </dd>
    </div>
  );
}

'use client';

import {useFormatter, useTranslations} from 'next-intl';
import type {AllergyResponse, ChronicConditionResponse} from '../types';
import {parseDateOnlyUtc} from '../utils/format';

const SEVERITIES = new Set(['Mild', 'Moderate', 'Severe']);

const SEVERITY_TONE: Record<string, string> = {
  Mild: 'border-brand/40 text-brand-strong',
  Moderate:
    'border-amber-400/50 text-amber-300 [:root[data-theme=light]_&]:text-amber-800',
  Severe: 'border-red-400/50 text-red-300 [:root[data-theme=light]_&]:text-red-800'
};

/** Prominent allergy list; severity is a badge so dangerous rows stand out. */
export function AllergyList({allergies}: {allergies: readonly AllergyResponse[]}) {
  const t = useTranslations('Patients.status');
  const notProvided = t('notProvided');

  if (allergies.length === 0) {
    return <EmptyLine>{t('allergiesEmpty')}</EmptyLine>;
  }

  return (
    <ul aria-label={t('allergiesTitle')} className="m-0 grid list-none gap-2 p-0" data-testid="allergy-list">
      {allergies.map((allergy) => (
        <li
          key={allergy.id}
          className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-surface px-4 py-3"
        >
          <span className="font-bold text-foreground" dir="auto">
            {allergy.name}
          </span>
          <span
            className={`rounded-full border px-2.5 py-0.5 text-xs font-bold ${
              SEVERITY_TONE[allergy.severity] ?? 'border-border text-muted'
            }`}
          >
            {SEVERITIES.has(allergy.severity) ? t(`severity.${allergy.severity}`) : allergy.severity}
          </span>
          {allergy.description ? (
            <span className="w-full text-sm text-muted" dir="auto">
              {allergy.description}
            </span>
          ) : null}
          {allergy.notes ? (
            <span className="w-full text-xs text-muted" dir="auto">
              {`${t('allergyNotes')}: ${allergy.notes}`}
            </span>
          ) : null}
          {allergy.description === null && allergy.notes === null ? (
            <span className="sr-only">{notProvided}</span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}

/** Chronic condition list with status and (when present) the diagnosis date. */
export function ConditionList({
  conditions
}: {
  conditions: readonly ChronicConditionResponse[];
}) {
  const t = useTranslations('Patients.status');
  const formatter = useFormatter();
  const notProvided = t('notProvided');

  if (conditions.length === 0) {
    return <EmptyLine>{t('conditionsEmpty')}</EmptyLine>;
  }

  return (
    <ul
      aria-label={t('conditionsTitle')}
      className="m-0 grid list-none gap-2 p-0"
      data-testid="condition-list"
    >
      {conditions.map((condition) => {
        const diagnosed = condition.diagnosisDate ? parseDateOnlyUtc(condition.diagnosisDate) : null;

        return (
          <li
            key={condition.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-surface px-4 py-3"
          >
            <span className="font-bold text-foreground" dir="auto">
              {condition.name}
            </span>
            <span className="rounded-full border border-border px-2.5 py-0.5 text-xs font-bold text-muted">
              {condition.status}
            </span>
            <span className="w-full text-xs text-muted">
              {condition.diagnosisDate && diagnosed
                ? `${t('conditionDiagnosisDate')}: ${formatter.dateTime(diagnosed, {dateStyle: 'medium', timeZone: 'UTC'})}`
                : `${t('conditionDiagnosisDate')}: ${notProvided}`}
            </span>
            {condition.notes ? (
              <span className="w-full text-xs text-muted" dir="auto">
                {condition.notes}
              </span>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

function EmptyLine({children}: {children: React.ReactNode}) {
  return (
    <p role="status" className="m-0 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-muted">
      {children}
    </p>
  );
}

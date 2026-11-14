'use client';

import {useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {FiExternalLink} from 'react-icons/fi';
import {Link} from '@/i18n/navigation';
import type {PatientVisitResponse} from '../types';
import {usePatientHistory} from '../hooks/use-patient-history';
import {getPatientErrorKey} from '../services/patient-errors';
import {ErrorPanel} from './StatePanel';
import {SkeletonRows} from './primitives/Skeleton';
import {Pagination} from './primitives/Pagination';
import {parseInstant} from '../utils/format';

/**
 * Paged visit history (§3.2, newest first). Each visit is a compact card with
 * its diagnoses, medications, vital signs, visit notes and lab results; the
 * full record stays on F4's visit screen, reached by a plain link (no coupling).
 */
export function VisitHistorySection({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.status.history');
  const tRoot = useTranslations('Patients');
  const [pageNumber, setPageNumber] = useState(1);
  const history = usePatientHistory(patientId, pageNumber);

  if (history.isPending) {
    return <SkeletonRows rows={3} label={t('loading')} />;
  }

  if (history.isError) {
    return (
      <ErrorPanel
        title={t('errorTitle')}
        description={tRoot(`errorKey.${getPatientErrorKey(history.error)}`)}
        retryLabel={tRoot('common.retry')}
        onRetry={() => history.refetch()}
      />
    );
  }

  const page = history.data;

  if (page.items.length === 0 && pageNumber === 1) {
    return (
      <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
        {t('empty')}
      </p>
    );
  }

  return (
    <div className="grid gap-4" data-testid="visit-history">
      {page.items.length === 0 ? (
        <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
          {t('emptyBeyond')}
        </p>
      ) : (
        page.items.map((visit) => <VisitCard key={visit.visitId} visit={visit} />)
      )}

      <Pagination
        label={t('paginationLabel')}
        pageNumber={page.page}
        totalPages={page.totalPages}
        disabled={history.isPlaceholderData}
        onChange={setPageNumber}
      />
    </div>
  );
}

function VisitCard({visit}: {visit: PatientVisitResponse}) {
  const t = useTranslations('Patients.status.history');
  const formatter = useFormatter();
  const notRecorded = t('noneInGroup');

  const visitDate = parseInstant(visit.visitDate);

  return (
    <article className="grid gap-4 rounded-2xl border border-border bg-surface p-5">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="m-0 text-base font-black text-foreground" dir="auto">
            {visit.chiefComplaint ?? t('visit')}
          </h3>
          <p className="m-0 mt-1 text-xs text-muted">
            {visitDate
              ? formatter.dateTime(visitDate, {dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC'})
              : visit.visitDate}
            {' · '}
            <span dir="auto">{visit.doctorName}</span>
          </p>
        </div>
        <Link
          href={`/medical-records/visits/${visit.visitId}`}
          className="inline-flex items-center gap-2 rounded-full border border-brand/50 px-3 py-1.5 text-xs font-bold text-brand transition-colors hover:bg-brand-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand"
          data-testid="visit-link"
        >
          <FiExternalLink aria-hidden="true" className="size-3.5" />
          {t('openVisit')}
        </Link>
      </header>

      {visit.symptoms ? (
        <FieldLine term={t('symptoms')} value={visit.symptoms} />
      ) : null}
      {visit.clinicalNotes ? (
        <FieldLine term={t('clinicalNotes')} value={visit.clinicalNotes} />
      ) : null}

      <Group label={t('diagnoses')} emptyLabel={notRecorded} testId="visit-diagnoses">
        {visit.diagnoses.map((diagnosis) => (
          <li key={diagnosis.id} dir="auto">
            <span className="font-bold">{diagnosis.name}</span>
            {diagnosis.description ? (
              <span className="text-muted">{` — ${diagnosis.description}`}</span>
            ) : null}
          </li>
        ))}
      </Group>

      <Group label={t('medications')} emptyLabel={notRecorded} testId="visit-medications">
        {visit.medications.map((medication) => (
          <li key={medication.id} dir="auto">
            <span className="font-bold">{medication.name}</span>
            <span className="text-muted">
              {` — ${medication.dosage}, ${medication.frequency}, ${medication.duration}`}
            </span>
            {medication.instructions ? (
              <span className="block text-xs text-muted">{medication.instructions}</span>
            ) : null}
          </li>
        ))}
      </Group>

      <Group label={t('vitalSigns')} emptyLabel={notRecorded} testId="visit-vitals">
        {visit.vitalSigns.map((vital) => (
          <li key={vital.id} className="flex flex-wrap gap-x-4 gap-y-1 text-xs" dir="ltr">
            <span>{`${t('vitals.bloodPressure')}: ${vital.bloodPressure}`}</span>
            {vital.heartRate !== null ? (
              <span>{`${t('vitals.heartRate')}: ${vital.heartRate}`}</span>
            ) : null}
            {vital.temperature !== null ? (
              <span>{`${t('vitals.temperature')}: ${vital.temperature}`}</span>
            ) : null}
            {vital.respiratoryRate !== null ? (
              <span>{`${t('vitals.respiratoryRate')}: ${vital.respiratoryRate}`}</span>
            ) : null}
            {vital.weight !== null ? (
              <span>{`${t('vitals.weight')}: ${vital.weight}`}</span>
            ) : null}
            {vital.height !== null ? (
              <span>{`${t('vitals.height')}: ${vital.height}`}</span>
            ) : null}
            {vital.spO2 !== null ? (
              <span>{`${t('vitals.spO2')}: ${vital.spO2}`}</span>
            ) : null}
          </li>
        ))}
      </Group>

      <Group label={t('notes')} emptyLabel={notRecorded} testId="visit-notes">
        {visit.notes.map((note) => (
          <li key={note.id} dir="auto">
            <span>{note.content}</span>
            <span className="block text-xs text-muted">
              {`${note.doctorName} · ${
                parseInstant(note.createdAtUtc)
                  ? formatter.dateTime(parseInstant(note.createdAtUtc) as Date, {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                      timeZone: 'UTC'
                    })
                  : note.createdAtUtc
              }`}
            </span>
          </li>
        ))}
      </Group>

      <Group label={t('labResults')} emptyLabel={notRecorded} testId="visit-labs">
        {visit.labResults.map((labResult) => (
          <li key={labResult.id} dir="auto">
            <span className="font-bold">{labResult.testName}</span>
            <span className="text-muted">
              {` — ${labResult.value} ${labResult.unit}`}
              {labResult.referenceRange ? ` (${labResult.referenceRange})` : ''}
            </span>
            {labResult.notes ? (
              <span className="block text-xs text-muted">{labResult.notes}</span>
            ) : null}
          </li>
        ))}
      </Group>
    </article>
  );
}

function FieldLine({term, value}: {term: string; value: string}) {
  return (
    <p className="m-0 text-sm">
      <span className="font-bold text-foreground">{`${term}: `}</span>
      <span className="text-muted" dir="auto">
        {value}
      </span>
    </p>
  );
}

function Group({
  label,
  emptyLabel,
  testId,
  children
}: {
  label: string;
  emptyLabel: string;
  testId: string;
  children: React.ReactNode;
}) {
  const items = Array.isArray(children) ? children : [children];
  const hasItems = items.some((child) => child !== null && child !== false);

  return (
    <div>
      <h4 className="m-0 text-xs font-bold uppercase tracking-wide text-muted">{label}</h4>
      {hasItems ? (
        <ul className="m-0 mt-1 grid list-none gap-1 p-0 text-sm text-foreground" data-testid={testId}>
          {children}
        </ul>
      ) : (
        <p className="m-0 mt-1 text-xs text-muted">{emptyLabel}</p>
      )}
    </div>
  );
}

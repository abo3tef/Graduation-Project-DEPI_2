'use client';

import {useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {usePatientLabs} from '../hooks/use-patient-labs';
import {getPatientErrorKey} from '../services/patient-errors';
import {parseDateOnlyUtc} from '../utils/format';
import {ErrorPanel} from './StatePanel';
import {SkeletonRows} from './primitives/Skeleton';
import {Pagination} from './primitives/Pagination';
import {VisitLink} from './VisitLink';
import type {PatientLabResult} from '../types';

/**
 * Tab ⑤: every laboratory result of the patient — visit-linked and unlinked
 * (§3.4, newest test first), paged per the shared Q-04 contract.
 */
export function LabResultsSection({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.labs');
  const tRoot = useTranslations('Patients');
  const [pageNumber, setPageNumber] = useState(1);
  const labs = usePatientLabs(patientId, pageNumber);

  if (labs.isPending) {
    return <SkeletonRows rows={3} label={t('loading')} />;
  }

  if (labs.isError) {
    return (
      <ErrorPanel
        title={t('errorTitle')}
        description={tRoot(`errorKey.${getPatientErrorKey(labs.error)}`)}
        retryLabel={tRoot('common.retry')}
        onRetry={() => labs.refetch()}
      />
    );
  }

  const page = labs.data;

  if (page.items.length === 0 && pageNumber === 1) {
    return (
      <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
        {t('empty')}
      </p>
    );
  }

  return (
    <div className="grid gap-4" data-testid="patient-labs">
      {page.items.length === 0 ? (
        <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
          {t('emptyBeyond')}
        </p>
      ) : (
        page.items.map((labResult) => (
          <LabCard key={labResult.id} labResult={labResult} />
        ))
      )}

      <Pagination
        label={t('paginationLabel')}
        pageNumber={page.page}
        totalPages={page.totalPages}
        disabled={labs.isPlaceholderData}
        onChange={setPageNumber}
      />
    </div>
  );
}

function LabCard({labResult}: {labResult: PatientLabResult}) {
  const t = useTranslations('Patients.labs');
  const formatter = useFormatter();

  const testDate = parseDateOnlyUtc(labResult.testDate);

  return (
    <article className="grid gap-2 rounded-2xl border border-border bg-surface p-5">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="m-0 text-base font-black text-foreground" dir="auto" data-testid="lab-test-name">
          {labResult.testName}
        </h3>
        <p className="m-0 text-sm text-foreground" dir="ltr" data-testid="lab-value">
          {`${labResult.value} ${labResult.unit}`}
        </p>
      </div>

      <p className="m-0 text-xs text-muted">
        {t('testDate')}
        {': '}
        {testDate
          ? formatter.dateTime(testDate, {dateStyle: 'medium', timeZone: 'UTC'})
          : labResult.testDate}
        {labResult.referenceRange !== null
          ? ` · ${t('referenceRange')}: ${labResult.referenceRange}`
          : ''}
      </p>

      {labResult.notes ? (
        <p className="m-0 text-sm text-muted" dir="auto">
          {labResult.notes}
        </p>
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-2">
        <VisitLink visitId={labResult.visitId} linkLabel={t('link')} unlinkedLabel={t('unlinked')} />
      </div>
    </article>
  );
}

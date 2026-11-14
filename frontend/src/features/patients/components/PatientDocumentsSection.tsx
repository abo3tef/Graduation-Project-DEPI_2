'use client';

import {useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {usePatientDocuments} from '../hooks/use-patient-documents';
import {getPatientErrorKey} from '../services/patient-errors';
import {parseInstant} from '../utils/format';
import {ErrorPanel} from './StatePanel';
import {SkeletonRows} from './primitives/Skeleton';
import {Pagination} from './primitives/Pagination';

/** Metadata only: no path, URL, file viewer or download control. */
export function PatientDocumentsSection({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.documents');
  const root = useTranslations('Patients');
  const formatter = useFormatter();
  const [pageNumber, setPageNumber] = useState(1);
  const query = usePatientDocuments(patientId, pageNumber);
  if (query.isPending) return <SkeletonRows rows={3} label={t('loading')} />;
  if (query.isError) return <ErrorPanel title={t('errorTitle')} description={root(`errorKey.${getPatientErrorKey(query.error)}`)} retryLabel={root('common.retry')} onRetry={() => query.refetch()} />;
  const page = query.data;
  if (page.items.length === 0 && pageNumber === 1) return <p role="status" className="rounded-2xl border border-border bg-surface p-6 text-muted">{t('empty')}</p>;
  return <div className="grid gap-4" data-testid="patient-documents">
    {page.items.length === 0 ? <p role="status" className="rounded-2xl border border-border bg-surface p-6 text-muted">{t('emptyBeyond')}</p> : page.items.map(doc => {
      const date = parseInstant(doc.uploadDateUtc);
      return <article key={doc.id} className="grid gap-2 rounded-2xl border border-border bg-surface p-5">
        <div className="flex flex-wrap items-center gap-2"><h3 className="font-black text-foreground break-all" dir="auto">{doc.fileName}</h3><span className="rounded-full border border-border px-2 py-1 text-xs text-muted">{doc.type}</span></div>
        <time dateTime={doc.uploadDateUtc} className="text-xs text-muted">{t('uploaded')}: {date ? formatter.dateTime(date, {dateStyle: 'medium', timeZone: 'UTC'}) : doc.uploadDateUtc}</time>
        {doc.description && <p dir="auto" className="text-sm">{doc.description}</p>}
        {doc.uploadedByDoctorName && <p dir="auto" className="text-xs text-muted">{doc.uploadedByDoctorName}</p>}
      </article>;
    })}
    <p className="text-xs text-muted">{t('metadataOnly')}</p>
    <Pagination label={t('paginationLabel')} pageNumber={page.page} totalPages={page.totalPages} disabled={query.isPlaceholderData} onChange={setPageNumber} />
  </div>;
}

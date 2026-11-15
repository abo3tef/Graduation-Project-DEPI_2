'use client';

import {useId, useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {useVitalSigns} from '../hooks/use-vital-signs';
import {useDevToken} from '../use-dev-token';
import {VITAL_SIGNS_FIELDS} from '../vital-signs-validation';
import {toQueryFailureKind} from '../errors';
import {LoadingPanel, StatePanel} from './AsyncState';
import {secondaryButtonClassName} from './form-styles';

/** Read-only; successful backend reads establish data access, never permission to edit. */
export function VitalSignsSection({visitId}: {visitId: string}) {
  const {token, isReady, credentialScope} = useDevToken();
  if (!isReady || !token?.trim()) return null;
  return <VitalSignsPage key={`${visitId}:${credentialScope}`} visitId={visitId} />;
}

function VitalSignsPage({visitId}: {visitId: string}) {
  const t = useTranslations('MedicalRecords.vitalSigns');
  const errorT = useTranslations('MedicalRecords.errors');
  const formatter = useFormatter();
  const headingId = useId();
  const [page, setPage] = useState(1);
  const query = useVitalSigns(visitId, page);

  if (query.isPending) return <LoadingPanel label={t('loading')} />;
  if (query.isError) {
    const kind = toQueryFailureKind(query.error);
    const retryable = kind === 'network' || kind === 'server';
    return <StatePanel tone="danger" title={t('errorTitle')} description={errorT(kind)}>
      {retryable ? <button type="button" className={secondaryButtonClassName}
        onClick={() => {void query.refetch();}}>{t('retry')}</button> : null}
    </StatePanel>;
  }
  if (!query.data) return <LoadingPanel label={t('loading')} />;
  const data = query.data;

  return (
    <section aria-labelledby={headingId} className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
      <h2 id={headingId} className="text-lg font-bold">{t('title')}</h2>
      <p className="mt-1 text-sm text-muted">{t('orderingNote')}</p>
      <p role="status" className="mt-2 text-sm text-muted">
        {query.isFetching ? t('refreshing') : t('recordCount', {count: data.totalCount})}
      </p>
      {data.items.length === 0 ? <p className="mt-4 text-sm text-muted">{t('empty')}</p> : (
        <ol className="mt-4 flex flex-col gap-4">
          {data.items.map((record, index) => (
            <li key={record.id} className="rounded-xl border border-border p-4">
              <h3 className="font-semibold">{t('recordLabel', {number: (data.page - 1) * data.pageSize + index + 1})}</h3>
              <dl className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {VITAL_SIGNS_FIELDS.map(field => {
                  const value = record[field];
                  return <div key={field}>
                    <dt className="text-sm text-muted">{t(`fields.${field}`)}</dt>
                    <dd className="mt-1 text-sm font-semibold">
                      {value === null ? t('notProvided') : <>
                        <bdi dir="ltr">
                          {typeof value === 'number' ? formatter.number(value, {maximumFractionDigits: 2}) : value}
                          {' '}{field === 'bloodPressure' ? t('bloodPressureUnit') : t(`units.${field}`)}
                        </bdi>
                      </>}
                    </dd>
                  </div>;
                })}
              </dl>
            </li>
          ))}
        </ol>
      )}
      {data.totalPages > 1 || page > 1 ? (
        <nav aria-label={t('pagination')} className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <span className="text-sm text-muted">{t('pagePosition', {page: data.page, total: Math.max(1, data.totalPages)})}</span>
          <div className="flex gap-2">
            <button type="button" className={secondaryButtonClassName} disabled={query.isFetching || page <= 1}
              onClick={() => setPage(previous => Math.max(1, previous - 1))}>{t('previous')}</button>
            <button type="button" className={secondaryButtonClassName} disabled={query.isFetching || page >= data.totalPages}
              onClick={() => setPage(previous => previous + 1)}>{t('next')}</button>
          </div>
        </nav>
      ) : null}
    </section>
  );
}

'use client';

import {useEffect, useId, useRef, useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {useSession} from '@/features/auth/session';
import {useVitalSigns} from '../hooks/use-vital-signs';
import {useMedicalCredentials} from '../use-medical-credentials';
import {canWriteVitalSigns} from '../vital-signs-access';
import {VITAL_SIGNS_FIELDS} from '../vital-signs-validation';
import {toQueryFailureKind} from '../errors';
import type {VitalSignsResponse} from '../types';
import {LoadingPanel, StatePanel} from './AsyncState';
import {VitalSignsForm} from './VitalSignsForm';
import {secondaryButtonClassName, sectionActionClassName} from './form-styles';

type Props = {visitId: string; visitDoctorId: string};
type Editor = {mode: 'create'} | {mode: 'edit'; record: VitalSignsResponse};

export function VitalSignsSection(props: Props) {
  const session = useSession();
  const {token, isReady, credentialScope} = useMedicalCredentials();
  if (!isReady || !token?.trim()) return null;
  const canWrite = canWriteVitalSigns(session, props.visitDoctorId);
  const contextKey = [props.visitId, credentialScope, props.visitDoctorId.toLowerCase(), canWrite].join(':');
  return <VitalSignsPage key={contextKey} {...props} canWrite={canWrite} contextKey={contextKey} />;
}

function VitalSignsPage({visitId, visitDoctorId, canWrite, contextKey}: Props & {canWrite: boolean; contextKey: string}) {
  const t = useTranslations('MedicalRecords.vitalSigns');
  const errorT = useTranslations('MedicalRecords.errors');
  const headingId = useId();
  const addId = headingId + '-add';
  const editorId = headingId + '-editor';
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<Editor | null>(null);
  const [savedRecord, setSavedRecord] = useState<VitalSignsResponse | null>(null);
  const focusReturnId = useRef<string | null>(null);
  const restoreFocus = useRef(false);
  const query = useVitalSigns(visitId, page);

  useEffect(() => {
    if (editor || !restoreFocus.current) return;
    const target = document.getElementById(focusReturnId.current ?? addId) ?? document.getElementById(addId);
    if (target) {
      target.focus();
      restoreFocus.current = false;
    }
  }, [editor, addId, query.data]);

  useEffect(() => {
    if (editor) document.getElementById(editorId)?.querySelector<HTMLInputElement>('input')?.focus();
  }, [editor, editorId]);

  function closeEditor(record?: VitalSignsResponse) {
    if (record) setSavedRecord(record);
    restoreFocus.current = true;
    setEditor(null);
  }

  if (query.isPending && !editor) return <LoadingPanel label={t('loading')} />;
  const data = query.data;
  const failure = query.isError ? toQueryFailureKind(query.error) : null;
  const retryable = failure === 'network' || failure === 'server';
  // A background read failure must not discard an active draft.
  const error = failure ? <StatePanel tone="danger" title={t('errorTitle')} description={errorT(failure)}>
    {retryable ? <button type="button" className={secondaryButtonClassName}
      onClick={() => {void query.refetch();}}>{t('retry')}</button> : null}
  </StatePanel> : null;
  if (failure && (!editor || !retryable)) return error;
  if (!data && !editor) return <LoadingPanel label={t('loading')} />;

  return (
    <section aria-labelledby={headingId} className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id={headingId} className="text-lg font-bold">{t('title')}</h2>
        {canWrite && !editor ? <button id={addId} type="button" className={sectionActionClassName}
          onClick={() => {focusReturnId.current = addId; setEditor({mode: 'create'});}}>{t('addAction')}</button> : null}
      </div>
      <p className="mt-1 text-sm text-muted">{t('orderingNote')}</p>
      {error}
      {editor ? <div id={editorId} className="mt-4">
        <VitalSignsForm key={[contextKey, editor.mode, editor.mode === 'edit' ? editor.record.id : 'new'].join(':')}
          visitId={visitId} visitDoctorId={visitDoctorId}
          {...(editor.mode === 'edit' ? {mode: 'edit', vitalSigns: editor.record} as const : {mode: 'create'} as const)}
          onDone={record => closeEditor(record)} onCancel={() => closeEditor()} />
      </div> : null}
      {data ? <>
        <p role="status" className="mt-2 text-sm text-muted">
          {query.isFetching ? t('refreshing') : t('recordCount', {count: data.totalCount})}
        </p>
        {savedRecord && !data.items.some(record => record.id === savedRecord.id) ?
          <aside aria-label={t('savedPreviewTitle')} className="mt-4 rounded-xl border border-brand/40 p-4">
            <h3 className="font-semibold">{t('savedPreviewTitle')}</h3>
            <p className="mt-1 text-sm text-muted">{t('savedPreviewDescription')}</p>
            <Measurements record={savedRecord} />
          </aside> : null}
        {data.items.length === 0 ? <p className="mt-4 text-sm text-muted">{t('empty')}</p> : (
          <ol className="mt-4 flex flex-col gap-4">
            {data.items.map((record, index) => {
              const number = (data.page - 1) * data.pageSize + index + 1;
              const editId = headingId + '-edit-' + record.id;
              return <li key={record.id} className="rounded-xl border border-border p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h3 className="font-semibold">{t('recordLabel', {number})}</h3>
                  {canWrite && !editor ? <button id={editId} type="button" className={sectionActionClassName}
                    aria-label={t('editLabel', {number})} onClick={() => {
                      focusReturnId.current = editId;
                      setEditor({mode: 'edit', record: {...record}});
                    }}>{t('editAction')}</button> : null}
                </div>
                <Measurements record={record} />
              </li>;
            })}
          </ol>
        )}
        {data.totalPages > 1 || page > 1 ? (
          <nav aria-label={t('pagination')} className="mt-4 flex flex-wrap items-center justify-between gap-3">
            <span className="text-sm text-muted">{t('pagePosition', {page: data.page, total: Math.max(1, data.totalPages)})}</span>
            <div className="flex gap-2">
              <button type="button" className={secondaryButtonClassName} disabled={Boolean(editor) || query.isFetching || page <= 1}
                onClick={() => {setSavedRecord(null); setPage(previous => Math.max(1, previous - 1));}}>{t('previous')}</button>
              <button type="button" className={secondaryButtonClassName} disabled={Boolean(editor) || query.isFetching || page >= data.totalPages}
                onClick={() => {setSavedRecord(null); setPage(previous => previous + 1);}}>{t('next')}</button>
            </div>
          </nav>
        ) : null}
      </> : null}
    </section>
  );
}

function Measurements({record}: {record: VitalSignsResponse}) {
  const t = useTranslations('MedicalRecords.vitalSigns');
  const formatter = useFormatter();
  return <dl className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
    {VITAL_SIGNS_FIELDS.map(field => {
      const value = record[field];
      return <div key={field}>
        <dt className="text-sm text-muted">{t(`fields.${field}`)}</dt>
        <dd className="mt-1 text-sm font-semibold">
          {value === null ? t('notProvided') : <bdi dir="ltr">
            {typeof value === 'number' ? formatter.number(value, {maximumFractionDigits: 2}) : value}
            {' '}{field === 'bloodPressure' ? t('bloodPressureUnit') : t(`units.${field}`)}
          </bdi>}
        </dd>
      </div>;
    })}
  </dl>;
}

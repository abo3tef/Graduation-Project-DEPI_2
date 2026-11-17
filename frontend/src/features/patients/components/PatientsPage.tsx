'use client';

import {useState} from 'react';
import {FiChevronLeft, FiChevronRight, FiEdit2, FiPlus, FiTrash2} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {
  useDebouncedValue,
  useDeletePatient,
  usePatientActor,
  usePatientList,
  SEARCH_DEBOUNCE_MS
} from '../hooks/usePatients';
import {getPatientErrorKey} from '../services/patient-errors';
import {
  GENDERS,
  PATIENT_SEARCH_MAX_LENGTH,
  type Gender,
  type Patient,
  type PatientFilters
} from '../types/patient.types';
import {Alert, Button, Dialog, controlClass} from './ui';

/**
 * Patient list (F8): debounced search by name, phone or id, gender filter and the shared paging
 * envelope. Delete is offered to doctors only (the API also refuses a secretary) and a 409 from
 * the API (the patient has related records, Q-17) is explained inside the confirmation dialog.
 */
export function PatientsPage() {
  const t = useTranslations('Patients.list');
  const errorsT = useTranslations('Patients.errors');
  const genderT = useTranslations('Patients.gender');
  const statesT = useTranslations('Patients.states');
  const {identity, role} = usePatientActor();
  const [searchText, setSearchText] = useState('');
  const [gender, setGender] = useState<PatientFilters['gender']>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const [toDelete, setToDelete] = useState<Patient | null>(null);

  const search = useDebouncedValue(searchText.trim(), SEARCH_DEBOUNCE_MS);
  const filters: PatientFilters = {search, gender, pageNumber, pageSize: DEFAULT_PAGE_SIZE};
  const list = usePatientList(filters);
  const canDelete = role === 'Doctor';

  if (identity.status === 'loading') {
    return <p className="text-sm text-muted">{t('loading')}</p>;
  }

  if (identity.status === 'none' || identity.role === 'Admin') {
    return (
      <Alert title={statesT('identityTitle')}>{statesT('signInHint')}</Alert>
    );
  }

  const page = list.data;

  return (
    <div className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-1">
          <h1 className="m-0 text-3xl font-black">{t('title')}</h1>
          <p className="m-0 max-w-2xl text-sm leading-6 text-muted">{t('description')}</p>
        </div>
        <Link
          href="/patients/new"
          className="inline-flex min-h-10 items-center gap-2 rounded-full bg-brand px-5 text-sm font-bold text-[#06211f] transition hover:opacity-90"
        >
          <FiPlus aria-hidden className="size-4" />
          {t('new')}
        </Link>
      </header>

      <div className="grid gap-3 sm:grid-cols-[1fr_12rem]">
        <div className="grid gap-1.5">
          <label htmlFor="patients-search" className="text-xs font-bold">
            {t('searchLabel')}
          </label>
          <input
            id="patients-search"
            type="search"
            dir="auto"
            autoComplete="off"
            maxLength={PATIENT_SEARCH_MAX_LENGTH}
            value={searchText}
            placeholder={t('searchPlaceholder')}
            onChange={(event) => {
              setSearchText(event.target.value);
              setPageNumber(1);
            }}
            className={controlClass}
          />
        </div>
        <div className="grid gap-1.5">
          <label htmlFor="patients-gender" className="text-xs font-bold">
            {t('genderLabel')}
          </label>
          <select
            id="patients-gender"
            value={gender}
            onChange={(event) => {
              setGender(event.target.value as PatientFilters['gender']);
              setPageNumber(1);
            }}
            className={controlClass}
          >
            <option value="all">{t('genderAll')}</option>
            {GENDERS.map((value: Gender) => (
              <option key={value} value={value}>
                {genderT(value)}
              </option>
            ))}
          </select>
        </div>
      </div>

      {list.isError ? (
        <Alert
          tone="error"
          action={
            <Button size="sm" onClick={() => void list.refetch()}>
              {t('retry')}
            </Button>
          }
        >
          {errorsT(getPatientErrorKey(list.error))}
        </Alert>
      ) : null}

      {list.isPending ? <p className="text-sm text-muted">{t('loading')}</p> : null}

      {page && page.items.length === 0 ? (
        <p className="rounded-xl border border-border bg-surface p-6 text-sm text-muted">
          {search === '' && gender === 'all' ? t('empty') : t('emptySearch')}
        </p>
      ) : null}

      {page && page.items.length > 0 ? (
        <>
          <p className="m-0 text-xs text-muted" aria-live="polite">
            {t('summary', {count: page.totalCount})}
          </p>
          <div className="overflow-x-auto rounded-xl border border-border bg-surface">
            <table className="w-full min-w-[40rem] border-collapse text-start text-sm">
              <thead>
                <tr className="border-b border-border text-xs text-muted">
                  <th scope="col" className="p-3 text-start font-bold">{t('columns.name')}</th>
                  <th scope="col" className="p-3 text-start font-bold">{t('columns.dateOfBirth')}</th>
                  <th scope="col" className="p-3 text-start font-bold">{t('columns.gender')}</th>
                  <th scope="col" className="p-3 text-start font-bold">{t('columns.phone')}</th>
                  <th scope="col" className="p-3 text-start font-bold">{t('columns.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {page.items.map((patient) => (
                  <tr key={patient.id} className="border-b border-border/60 last:border-0">
                    <td className="p-3 font-bold" dir="auto">{patient.fullName}</td>
                    <td className="p-3" dir="ltr">{patient.dateOfBirth}</td>
                    <td className="p-3">{genderT(patient.gender)}</td>
                    <td className="p-3" dir="ltr">{patient.phone ?? '—'}</td>
                    <td className="p-3">
                      <div className="flex flex-wrap gap-2">
                        <Link
                          href={`/patients/${patient.id}/edit`}
                          aria-label={t('editNamed', {name: patient.fullName})}
                          className="inline-flex min-h-8 items-center gap-1.5 rounded-full border border-border px-3 text-xs font-bold transition hover:border-brand hover:text-brand"
                        >
                          <FiEdit2 aria-hidden className="size-3.5" />
                          {t('edit')}
                        </Link>
                        {canDelete ? (
                          <Button
                            size="sm"
                            aria-label={t('deleteNamed', {name: patient.fullName})}
                            onClick={() => setToDelete(patient)}
                          >
                            <FiTrash2 aria-hidden className="size-3.5" />
                            {t('delete')}
                          </Button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {page.totalPages > 1 ? (
            <nav aria-label={t('pagination.label')} className="flex flex-wrap items-center justify-between gap-3">
              <p className="m-0 text-xs text-muted" aria-live="polite">
                {t('pagination.position', {page: page.page, total: page.totalPages})}
              </p>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  disabled={list.isFetching || pageNumber <= 1}
                  onClick={() => setPageNumber(pageNumber - 1)}
                >
                  <FiChevronLeft aria-hidden className="size-3.5 rtl:hidden" />
                  <FiChevronRight aria-hidden className="size-3.5 ltr:hidden" />
                  {t('pagination.previous')}
                </Button>
                <Button
                  size="sm"
                  disabled={list.isFetching || pageNumber >= page.totalPages}
                  onClick={() => setPageNumber(pageNumber + 1)}
                >
                  {t('pagination.next')}
                  <FiChevronRight aria-hidden className="size-3.5 rtl:hidden" />
                  <FiChevronLeft aria-hidden className="size-3.5 ltr:hidden" />
                </Button>
              </div>
            </nav>
          ) : null}
        </>
      ) : null}

      <DeletePatientDialog patient={toDelete} onClose={() => setToDelete(null)} />
    </div>
  );
}

function DeletePatientDialog({patient, onClose}: {patient: Patient | null; onClose: () => void}) {
  const t = useTranslations('Patients.delete');
  const errorsT = useTranslations('Patients.errors');
  const mutation = useDeletePatient();

  function close() {
    mutation.reset();
    onClose();
  }

  return (
    <Dialog
      open={patient !== null}
      onClose={close}
      title={t('title')}
      description={patient ? t('description', {name: patient.fullName}) : undefined}
    >
      {mutation.isError ? <Alert tone="error">{errorsT(getPatientErrorKey(mutation.error))}</Alert> : null}
      <div className="flex flex-wrap justify-end gap-3">
        <Button onClick={close} disabled={mutation.isPending}>
          {t('cancel')}
        </Button>
        <Button
          variant="danger"
          loading={mutation.isPending}
          onClick={() => {
            if (patient) {
              mutation.mutate(patient.id, {onSuccess: close});
            }
          }}
        >
          {t('confirm')}
        </Button>
      </div>
    </Dialog>
  );
}

'use client';

import {useTranslations} from 'next-intl';
import {Alert} from './ui';
import {PatientForm} from './PatientForm';
import {usePatient, usePatientActor} from '../hooks/usePatients';
import {getPatientErrorKey} from '../services/patient-errors';

function Shell({title, description, children}: {title: string; description: string; children: React.ReactNode}) {
  return (
    <div className="grid max-w-3xl gap-6">
      <header className="grid gap-1">
        <h1 className="m-0 text-3xl font-black">{title}</h1>
        <p className="m-0 text-sm leading-6 text-muted">{description}</p>
      </header>
      <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">{children}</section>
    </div>
  );
}

function useIdentityGate() {
  const {identity} = usePatientActor();

  return identity;
}

export function NewPatientScreen() {
  const t = useTranslations('Patients.form');
  const statesT = useTranslations('Patients.states');
  const identity = useIdentityGate();

  if (identity.status === 'none' || identity.role === 'Admin') {
    return <Alert title={statesT('identityTitle')}>{statesT('signInHint')}</Alert>;
  }

  return (
    <Shell title={t('createTitle')} description={t('createDescription')}>
      {identity.status === 'ready' ? <PatientForm /> : null}
    </Shell>
  );
}

export function EditPatientScreen({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.form');
  const statesT = useTranslations('Patients.states');
  const errorsT = useTranslations('Patients.errors');
  const identity = useIdentityGate();
  const patient = usePatient(patientId);

  if (identity.status === 'none' || identity.role === 'Admin') {
    return <Alert title={statesT('identityTitle')}>{statesT('signInHint')}</Alert>;
  }

  return (
    <Shell title={t('editTitle')} description={t('editDescription')}>
      {patient.isPending ? <p className="m-0 text-sm text-muted">{statesT('loading')}</p> : null}
      {patient.isError ? <Alert tone="error">{errorsT(getPatientErrorKey(patient.error))}</Alert> : null}
      {patient.data ? <PatientForm key={patient.data.id} patient={patient.data} /> : null}
    </Shell>
  );
}

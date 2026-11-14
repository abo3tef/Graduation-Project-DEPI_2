'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import type {UseQueryResult} from '@tanstack/react-query';
import {useActorGate} from '../hooks/use-actor-gate';
import {usePatientProfile} from '../hooks/use-patient-profile';
import {getPatientErrorKey} from '../services/patient-errors';
import {isGuid} from '../validation';
import type {PatientMedicalProfileResponse} from '../types';
import {Alert} from './primitives/Alert';
import {Tabs} from './primitives/Tabs';
import {SkeletonRows} from './primitives/Skeleton';
import {IdentityGate} from './IdentityGate';
import {ErrorPanel, LoadingPanel, StatePanel} from './StatePanel';
import {PatientProfile} from './PatientProfile';
import {AllergyList, ConditionList} from './AllergyConditionLists';
import {VisitHistorySection} from './VisitHistorySection';
import {PatientNotesSection} from './PatientNotesSection';
import {LabResultsSection} from './LabResultsSection';

type TabId = 'profile' | 'allergies' | 'history' | 'notes' | 'labs';

/**
 * The doctor-facing patient medical status screen: one page, five tabs —
 * profile, allergies & chronic conditions, paged visit history, patient
 * notes, and lab results (D-4). Nothing renders (and nothing is requested)
 * until a doctor identity is ready. The profile query serves both the
 * profile and the allergies/conditions tab.
 */
export function PatientMedicalStatusScreen({patientId}: {patientId: string}) {
  const t = useTranslations('Patients');
  const {identity, ready} = useActorGate();
  const [activeTab, setActiveTab] = useState<TabId>('profile');
  const profile = usePatientProfile(patientId);

  if (!isGuid(patientId)) {
    return (
      <StatePanel
        title={t('states.invalidPatientTitle')}
        description={t('states.invalidPatientHint')}
      />
    );
  }

  if (identity.status === 'loading') {
    return <LoadingPanel label={t('states.loadingTitle')} />;
  }

  if (identity.status === 'none') {
    return <IdentityGate />;
  }

  if (!ready) {
    return (
      <Alert tone="warning" title={t('states.forbiddenTitle')}>
        {t('states.forbiddenHint')}
      </Alert>
    );
  }

  const heading =
    profile.status === 'success' ? (
      <h1 className="mt-2 text-3xl font-black sm:text-4xl" dir="auto">
        {profile.data.fullName}
      </h1>
    ) : (
      <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('status.title')}</h1>
    );

  return (
    <main className="flex flex-col gap-6" data-testid="patient-status">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('status.eyebrow')}
        </p>
        {heading}
        <p className="mt-1 break-all font-mono text-xs text-muted" dir="ltr">
          {patientId}
        </p>
      </header>

      <Tabs
        label={t('status.tabsLabel')}
        active={activeTab}
        onChange={(id) => setActiveTab(id as TabId)}
        tabs={[
          {id: 'profile', label: t('status.tabs.profile')},
          {id: 'allergies', label: t('status.tabs.allergies')},
          {id: 'history', label: t('status.tabs.history')},
          {id: 'notes', label: t('status.tabs.notes')},
          {id: 'labs', label: t('status.tabs.labs')}
        ]}
      >
        {(tabId) =>
          tabId === 'profile' ? (
            <ProfilePanel profile={profile} />
          ) : tabId === 'allergies' ? (
            <AllergiesPanel profile={profile} />
          ) : tabId === 'history' ? (
            <VisitHistorySection patientId={patientId} />
          ) : tabId === 'notes' ? (
            <PatientNotesSection patientId={patientId} />
          ) : (
            <LabResultsSection patientId={patientId} />
          )
        }
      </Tabs>
    </main>
  );
}

/** Profile tab: the Class Spec §4.2 attributes, loading / error / data. */
function ProfilePanel({
  profile
}: {
  profile: UseQueryResult<PatientMedicalProfileResponse, unknown>;
}) {
  const t = useTranslations('Patients');

  if (profile.isPending) {
    return <SkeletonRows rows={4} label={t('states.loadingTitle')} />;
  }

  if (profile.isError) {
    return <ProfileError profile={profile} />;
  }

  return <PatientProfile profile={profile.data} />;
}

/** Allergies & chronic-conditions tab: prominent lists from the same profile query. */
function AllergiesPanel({
  profile
}: {
  profile: UseQueryResult<PatientMedicalProfileResponse, unknown>;
}) {
  const t = useTranslations('Patients.status');

  if (profile.isPending) {
    return <SkeletonRows rows={3} label={t('loadingSection')} />;
  }

  if (profile.isError) {
    return (
      <Alert tone="error" title={t('errorSection')}>
        <ErrorKeyText error={profile.error} />
      </Alert>
    );
  }

  return (
    <div className="grid gap-6" data-testid="allergies-panel">
      <section aria-labelledby="allergies-title">
        <h2 id="allergies-title" className="m-0 mb-3 text-lg font-black text-foreground">
          {t('allergiesTitle')}
        </h2>
        <AllergyList allergies={profile.data.allergies} />
      </section>
      <section aria-labelledby="conditions-title">
        <h2 id="conditions-title" className="m-0 mb-3 text-lg font-black text-foreground">
          {t('conditionsTitle')}
        </h2>
        <ConditionList conditions={profile.data.chronicConditions} />
      </section>
    </div>
  );
}

/** Single error-key message source (`Patients.errorKey`) shared by all sections. */
function ErrorKeyText({error}: {error: unknown}) {
  const t = useTranslations('Patients');

  return <>{t(`errorKey.${getPatientErrorKey(error)}`)}</>;
}

/** The profile-level failure states: 404 is its own panel; everything else retries. */
function ProfileError({
  profile
}: {
  profile: UseQueryResult<PatientMedicalProfileResponse, unknown>;
}) {
  const t = useTranslations('Patients');
  const key = getPatientErrorKey(profile.error);

  if (key === 'notFound') {
    return (
      <StatePanel
        tone="danger"
        title={t('states.notFoundTitle')}
        description={t('states.notFoundHint')}
      />
    );
  }

  return (
    <ErrorPanel
      title={t(`errorKey.${key}`)}
      description={t('states.errorHint')}
      retryLabel={t('common.retry')}
      onRetry={() => profile.refetch()}
    />
  );
}

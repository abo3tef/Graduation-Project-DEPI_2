'use client';

import type {ReactNode} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import type {
  PatientMedicalProfileResponse,
  PatientAllergyResponse,
  PatientChronicConditionResponse
} from '../types';

type PatientMedicalProfileSummaryProps = {
  profile: PatientMedicalProfileResponse;
};

export function PatientMedicalProfileSummary({
  profile
}: PatientMedicalProfileSummaryProps) {
  const t = useTranslations('MedicalTimeline.profile');
  const formatter = useFormatter();
  const formatDate = (value: string | null) => {
    if (value === null) {
      return t('notProvided');
    }

    const date = new Date(`${value}T00:00:00.000Z`);

    return Number.isNaN(date.getTime())
      ? t('notProvided')
      : formatter.dateTime(date, {dateStyle: 'medium', timeZone: 'UTC'});
  };
  const display = (value: string | null) => value ?? t('notProvided');

  return (
    <section
      aria-labelledby="patient-profile-title"
      className="flex flex-col gap-5 rounded-2xl border border-border bg-surface p-5 sm:p-6"
    >
      <div>
        <h2 id="patient-profile-title" className="text-xl font-bold text-foreground">
          {profile.fullName}
        </h2>
        <p className="mt-1 break-all font-mono text-xs text-muted" dir="ltr">
          {profile.patientId}
        </p>
      </div>

      <ProfileSection title={t('demographics')}>
        <dl className="m-0 grid gap-2 sm:grid-cols-2">
          <ProfileField label={t('dateOfBirth')} value={formatDate(profile.dateOfBirth)} />
          <ProfileField label={t('age')} value={String(profile.age)} />
          <ProfileField label={t('gender')} value={profile.gender} />
          <ProfileField label={t('bloodType')} value={display(profile.bloodType)} />
        </dl>
      </ProfileSection>

      <ProfileSection title={t('contact')}>
        <dl className="m-0 grid gap-2 sm:grid-cols-2">
          <ProfileField label={t('phone')} value={display(profile.phone)} />
          <ProfileField label={t('address')} value={display(profile.address)} />
          <ProfileField
            label={t('emergencyContact')}
            value={display(profile.emergencyContact)}
          />
        </dl>
      </ProfileSection>

      <ProfileSection title={t('allergies')}>
        {profile.allergies.length === 0 ? (
          <p className="text-sm text-muted">{t('emptyAllergies')}</p>
        ) : (
          <ul className="grid gap-3 sm:grid-cols-2">
            {profile.allergies.map((allergy) => (
              <li
                key={allergy.id}
                className="rounded-xl border border-border bg-background p-3"
              >
                <AllergyDetails allergy={allergy} display={display} t={t} />
              </li>
            ))}
          </ul>
        )}
      </ProfileSection>

      <ProfileSection title={t('chronicConditions')}>
        {profile.chronicConditions.length === 0 ? (
          <p className="text-sm text-muted">{t('emptyChronicConditions')}</p>
        ) : (
          <ul className="grid gap-3 sm:grid-cols-2">
            {profile.chronicConditions.map((condition) => (
              <li
                key={condition.id}
                className="rounded-xl border border-border bg-background p-3"
              >
                <ConditionDetails condition={condition} display={display} formatDate={formatDate} t={t} />
              </li>
            ))}
          </ul>
        )}
      </ProfileSection>
    </section>
  );
}

type ProfileSectionProps = {
  title: string;
  children: ReactNode;
};

function ProfileSection({title, children}: ProfileSectionProps) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="border-b border-border pb-2 text-sm font-bold">{title}</h3>
      {children}
    </section>
  );
}

type ProfileFieldProps = {
  label: string;
  value: string;
};

function ProfileField({label, value}: ProfileFieldProps) {
  return (
    <div className="flex flex-wrap gap-x-2 gap-y-0.5">
      <dt className="text-xs font-semibold text-muted">{label}</dt>
      <dd className="m-0 break-words text-sm text-foreground">{value}</dd>
    </div>
  );
}

type ProfileTranslations = ReturnType<typeof useTranslations<'MedicalTimeline.profile'>>;

function AllergyDetails({
  allergy,
  display,
  t
}: {
  allergy: PatientAllergyResponse;
  display: (value: string | null) => string;
  t: ProfileTranslations;
}) {
  return (
    <dl className="m-0 flex flex-col gap-2">
      <ProfileField label={t('name')} value={allergy.name} />
      <ProfileField label={t('severity')} value={display(allergy.severity)} />
      <ProfileField label={t('description')} value={display(allergy.description)} />
      <ProfileField label={t('notes')} value={display(allergy.notes)} />
    </dl>
  );
}

function ConditionDetails({
  condition,
  display,
  formatDate,
  t
}: {
  condition: PatientChronicConditionResponse;
  display: (value: string | null) => string;
  formatDate: (value: string | null) => string;
  t: ProfileTranslations;
}) {
  return (
    <dl className="m-0 flex flex-col gap-2">
      <ProfileField label={t('name')} value={condition.name} />
      <ProfileField
        label={t('diagnosisDate')}
        value={formatDate(condition.diagnosisDate)}
      />
      <ProfileField label={t('status')} value={display(condition.status)} />
      <ProfileField label={t('notes')} value={display(condition.notes)} />
    </dl>
  );
}

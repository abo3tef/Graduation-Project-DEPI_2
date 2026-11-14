'use client';

import {useFormatter, useTranslations} from 'next-intl';
import type {PatientMedicalProfileResponse} from '../types';
import {parseDateOnlyUtc} from '../utils/format';

/**
 * The known enum values the API can send for a few display fields. Anything
 * else is rendered verbatim rather than guessed at — the API stays the source
 * of truth for the domain vocabulary.
 */
const GENDERS = new Set(['Female', 'Male']);
const BLOOD_TYPES = new Set([
  'APositive', 'ANegative', 'BPositive', 'BNegative',
  'ABPositive', 'ABNegative', 'OPositive', 'ONegative'
]);

/**
 * Read-only profile tab: the Class Spec §4.2 patient attributes the F3-C1
 * contract returns, including blood type and emergency contact.
 */
export function PatientProfile({profile}: {profile: PatientMedicalProfileResponse}) {
  const t = useTranslations('Patients.status');
  const formatter = useFormatter();
  const notProvided = t('notProvided');

  const birthDate = parseDateOnlyUtc(profile.dateOfBirth);
  const gender =
    profile.gender === ''
      ? null
      : GENDERS.has(profile.gender)
        ? t(`gender.${profile.gender}`)
        : profile.gender;
  const bloodType =
    profile.bloodType === null || profile.bloodType === ''
      ? notProvided
      : BLOOD_TYPES.has(profile.bloodType)
        ? t(`bloodTypes.${profile.bloodType}`)
        : profile.bloodType;

  return (
    <section aria-labelledby="patient-profile-title" data-testid="patient-profile">
      <dl className="m-0 grid gap-4 sm:grid-cols-2">
        <Detail term={t('fields.fullName')} wide>
          <span dir="auto">{profile.fullName}</span>
        </Detail>
        <Detail term={t('fields.dateOfBirth')}>
          {birthDate
            ? formatter.dateTime(birthDate, {dateStyle: 'medium', timeZone: 'UTC'})
            : profile.dateOfBirth}
        </Detail>
        <Detail term={t('fields.age')}>{profile.age}</Detail>
        <Detail term={t('fields.gender')}>
          {gender ?? notProvided}
        </Detail>
        <Detail term={t('fields.phone')}>
          {profile.phone ? <span dir="ltr">{profile.phone}</span> : notProvided}
        </Detail>
        <Detail term={t('fields.address')}>
          {profile.address ?? notProvided}
        </Detail>
        <Detail term={t('fields.bloodType')}>
          {bloodType ?? notProvided}
        </Detail>
        <Detail term={t('fields.emergencyContact')} wide>
          {profile.emergencyContact ?? notProvided}
        </Detail>
      </dl>
    </section>
  );
}

function Detail({
  term,
  wide,
  children
}: {
  term: string;
  wide?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className={wide ? 'sm:col-span-2' : undefined}>
      <dt className="text-xs font-bold uppercase tracking-wide text-muted">{term}</dt>
      <dd className="m-0 mt-1 text-sm text-foreground">{children}</dd>
    </div>
  );
}

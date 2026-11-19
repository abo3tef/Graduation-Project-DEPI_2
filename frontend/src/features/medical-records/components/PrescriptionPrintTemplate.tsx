'use client';

import {useLocale, useTranslations} from 'next-intl';
import type {Patient} from '@/features/patients';
import type {MedicalVisitResponse} from '../types';

type PrescriptionPrintTemplateProps = {
  visit: MedicalVisitResponse;
  patient: Patient | null | undefined;
  doctorName: string | null;
  doctorSpecialization: string | null;
};

export function PrescriptionPrintTemplate({
  visit,
  patient,
  doctorName,
  doctorSpecialization
}: PrescriptionPrintTemplateProps) {
  const locale = useLocale();
  const isArabic = locale === 'ar';
  const t = useTranslations('MedicalRecords.prescription');

  const visitDate = formatDate(visit.visitDate, locale);
  const patientName = patient?.fullName ?? t('patientNameUnavailable');
  const patientAge = patient ? calculateAge(patient.dateOfBirth, new Date(visit.visitDate)) : null;
  const gender = patient
    ? patient.gender === 'Male'
      ? t('genderMale')
      : t('genderFemale')
    : t('notProvided');

  const clinicalSummary = [visit.chiefComplaint, visit.symptoms, visit.clinicalNotes]
    .filter((value): value is string => Boolean(value?.trim()))
    .join(' · ');

  return (
    <article
      data-prescription-print="true"
      dir={isArabic ? 'rtl' : 'ltr'}
      className="prescription-print-template mx-auto w-full max-w-[210mm] bg-white px-7 py-8 text-black shadow-xl sm:px-10"
    >
      <header className="flex items-start justify-between gap-5 border-b-2 border-black pb-5">
        <div>
          <div className="flex items-center gap-3">
            <span className="grid size-12 place-items-center rounded-full border-2 border-black text-xl font-black">
              Rx
            </span>
            <div>
              <h1 className="text-2xl font-black tracking-tight">Medilog</h1>
              <p className="mt-1 text-xs font-semibold">{t('title')}</p>
            </div>
          </div>
        </div>
        <dl className="min-w-44 text-xs leading-6">
          <div className="flex justify-between gap-4">
            <dt className="font-bold">{t('date')}</dt>
            <dd>{visitDate}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="font-bold">{t('visit')}</dt>
            <dd className="font-mono text-[10px]">{visit.id}</dd>
          </div>
        </dl>
      </header>

      <dl className="grid grid-cols-1 gap-3 border-b border-black py-5 text-xs sm:grid-cols-2">
        <div>
          <dt className="font-bold text-black/65">{t('doctor')}</dt>
          <dd className="mt-1 text-sm font-bold">{doctorName || t('notProvided')}</dd>
          {doctorSpecialization ? (
            <dd className="mt-0.5 text-xs">{doctorSpecialization}</dd>
          ) : null}
        </div>
        <div>
          <dt className="font-bold text-black/65">{t('patient')}</dt>
          <dd className="mt-1 text-sm font-bold">{patientName}</dd>
          <dd className="mt-0.5 text-xs">
            {patientAge !== null ? `${t('age')}: ${patientAge} · ` : ''}
            {t('gender')}: {gender} · {t('patientId')}: {visit.patientId}
          </dd>
        </div>
      </dl>

      {(visit.diagnoses.length > 0 || clinicalSummary) ? (
        <section className="prescription-print-section border-b border-black py-4">
          <h2 className="text-sm font-black">{t('diagnosisAndSummary')}</h2>
          {visit.diagnoses.length > 0 ? (
            <p className="mt-2 text-xs leading-5">
              <span className="font-bold">{t('diagnosis')}: </span>
              {visit.diagnoses.map((diagnosis) => diagnosis.diagnosisName).join(' · ')}
            </p>
          ) : null}
          {clinicalSummary ? (
            <p className="mt-1 whitespace-pre-line text-xs leading-5">
              <span className="font-bold">{t('clinicalSummary')}: </span>
              {clinicalSummary}
            </p>
          ) : null}
        </section>
      ) : null}

      <section className="prescription-print-section py-5">
        <h2 className="text-lg font-black">{t('medications')}</h2>
        {visit.medications.length === 0 ? (
          <p className="mt-4 border-y border-black/40 py-5 text-center text-sm">
            {t('noMedications')}
          </p>
        ) : (
          <table className="prescription-medication-table mt-3 w-full table-fixed border-collapse text-xs">
            <thead>
              <tr className="border-y-2 border-black">
                <th className="w-[28%] px-2 py-2 text-start">{t('medicationName')}</th>
                <th className="w-[17%] px-2 py-2 text-start">{t('dosage')}</th>
                <th className="w-[17%] px-2 py-2 text-start">{t('frequency')}</th>
                <th className="w-[14%] px-2 py-2 text-start">{t('duration')}</th>
                <th className="px-2 py-2 text-start">{t('instructions')}</th>
              </tr>
            </thead>
            <tbody>
              {visit.medications.map((medication) => (
                <tr key={medication.id} className="border-b border-black/40 align-top">
                  <td className="break-words px-2 py-3 font-bold">{medication.medicationName}</td>
                  <td className="break-words px-2 py-3">{medication.dosage}</td>
                  <td className="break-words px-2 py-3">{medication.frequency}</td>
                  <td className="break-words px-2 py-3">{medication.duration || t('notProvided')}</td>
                  <td className="break-words whitespace-pre-line px-2 py-3">
                    {medication.instructions || t('notProvided')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <footer className="mt-10 flex justify-end border-t border-black/50 pt-8">
        <div className="w-44 text-center">
          <div className="mb-2 border-b border-black" />
          <p className="text-xs font-bold">{t('signature')}</p>
          {doctorName ? <p className="mt-1 text-xs">{doctorName}</p> : null}
        </div>
      </footer>
    </article>
  );
}

function formatDate(value: string, locale: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  return new Intl.DateTimeFormat(locale === 'ar' ? 'ar-EG' : 'en-US', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }).format(date);
}

function calculateAge(dateOfBirth: string, visitDate: Date): number | null {
  const birthDate = new Date(dateOfBirth);
  if (Number.isNaN(birthDate.getTime()) || Number.isNaN(visitDate.getTime())) {
    return null;
  }

  let age = visitDate.getUTCFullYear() - birthDate.getUTCFullYear();
  const monthDifference = visitDate.getUTCMonth() - birthDate.getUTCMonth();
  if (monthDifference < 0 || (monthDifference === 0 && visitDate.getUTCDate() < birthDate.getUTCDate())) {
    age--;
  }
  return age >= 0 ? age : null;
}

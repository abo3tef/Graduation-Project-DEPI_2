'use client';

import {FiPrinter} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {useSession} from '@/features/auth';
import {usePatient} from '@/features/patients';
import type {MedicalVisitResponse} from '../types';
import {MedicationSection} from './MedicationSection';
import {PrescriptionPrintTemplate} from './PrescriptionPrintTemplate';

type PrescriptionSectionProps = {
  visit: MedicalVisitResponse;
};

export function PrescriptionSection({visit}: PrescriptionSectionProps) {
  const t = useTranslations('MedicalRecords.prescription');
  const session = useSession();
  const patient = usePatient(visit.patientId);

  if (!session.isDoctor) {
    return null;
  }

  function printPrescription() {
    window.print();
  }

  return (
    <section className="flex flex-col gap-4">
      <div className="flex justify-end print:hidden">
        <button
          type="button"
          onClick={printPrescription}
          className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-brand/40 bg-brand-soft px-4 text-sm font-bold text-brand-strong"
        >
          <FiPrinter aria-hidden className="size-4" />
          {t('print')}
        </button>
      </div>

      <div className="print:hidden">
        <MedicationSection visitId={visit.id} medications={visit.medications} />
      </div>

      <PrescriptionPrintTemplate
        visit={visit}
        patient={patient.data}
        doctorName={session.user?.fullName ?? null}
        doctorSpecialization={session.user?.specialization ?? null}
      />
    </section>
  );
}

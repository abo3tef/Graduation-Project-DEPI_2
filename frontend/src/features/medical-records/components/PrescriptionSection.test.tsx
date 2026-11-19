import type {ReactNode} from 'react';
import {render, screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {NextIntlClientProvider} from 'next-intl';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';
import type {MedicalVisitResponse} from '../types';
import {PrescriptionSection} from './PrescriptionSection';

vi.mock('@/features/auth', () => ({
  useSession: () => ({
    isDoctor: true,
    user: {
      id: 'doctor-1',
      fullName: 'Dr. Sarah Khaled',
      email: 'sarah@example.test',
      role: 'Doctor',
      doctorId: 'doctor-1',
      specialization: 'Internal Medicine'
    }
  })
}));

vi.mock('@/features/patients', () => ({
  usePatient: () => ({
    data: {
      id: 'patient-1',
      doctorId: 'doctor-1',
      fullName: 'Ahmed Mohamed',
      dateOfBirth: '1990-01-15',
      gender: 'Male',
      phone: null,
      address: null,
      emergencyContact: null,
      bloodType: null,
      createdAt: '2026-01-01T00:00:00Z'
    }
  })
}));

vi.mock('./MedicationSection', () => ({
  MedicationSection: () => <div>Medication composer</div>
}));

const visit: MedicalVisitResponse = {
  id: '11111111-1111-4111-8111-111111111111',
  patientId: 'patient-1',
  doctorId: 'doctor-1',
  appointmentId: null,
  visitDate: '2026-11-20T10:30:00Z',
  chiefComplaint: 'Persistent headache',
  symptoms: 'Mild dizziness',
  clinicalNotes: 'Follow-up in two weeks',
  diagnoses: [{
    id: 'diagnosis-1',
    visitId: '11111111-1111-4111-8111-111111111111',
    patientId: 'patient-1',
    diagnosisName: 'Migraine',
    description: null,
    diagnosisDate: '2026-11-20'
  }],
  medications: [{
    id: 'medication-1',
    visitId: '11111111-1111-4111-8111-111111111111',
    patientId: 'patient-1',
    medicationName: 'Paracetamol',
    dosage: '500 mg',
    frequency: 'Every 8 hours',
    duration: '5 days',
    instructions: 'Take after food'
  }],
  vitalSigns: []
};

function setup(locale: 'en' | 'ar' = 'en') {
  function wrapper({children}: {children: ReactNode}) {
    return (
      <NextIntlClientProvider
        locale={locale}
        messages={locale === 'ar' ? ar : en}
        onError={(error) => {throw error;}}
      >
        {children}
      </NextIntlClientProvider>
    );
  }
  return {wrapper};
}

beforeEach(() => {
  vi.restoreAllMocks();
  Object.defineProperty(window, 'print', {
    configurable: true,
    value: vi.fn()
  });
});

describe('PrescriptionSection', () => {
  it('renders a dedicated prescription and triggers window.print only', async () => {
    const {wrapper} = setup();
    const user = userEvent.setup();
    render(<PrescriptionSection visit={visit} />, {wrapper});

    expect(screen.getByText('Medication composer')).toBeInTheDocument();
    expect(screen.getByText('Ahmed Mohamed')).toBeInTheDocument();
    expect(screen.getAllByText('Dr. Sarah Khaled')).not.toHaveLength(0);
    expect(screen.getByText('Internal Medicine')).toBeInTheDocument();
    expect(screen.getByText('Migraine')).toBeInTheDocument();
    expect(screen.getByText('Paracetamol')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Print Prescription'}));
    expect(window.print).toHaveBeenCalledOnce();
  });

  it('renders RTL Arabic prescription metadata', () => {
    const {wrapper} = setup('ar');
    render(<PrescriptionSection visit={visit} />, {wrapper});

    const template = document.querySelector('[data-prescription-print="true"]');
    expect(template).toHaveAttribute('dir', 'rtl');
    expect(screen.getByRole('button', {name: 'طباعة الروشتة'})).toBeInTheDocument();
    expect(screen.getByText('الروشتة الطبية')).toBeInTheDocument();
  });
});

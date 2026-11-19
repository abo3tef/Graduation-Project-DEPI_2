import type {ReactNode} from 'react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {render, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {NextIntlClientProvider} from 'next-intl';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';
import type {MedicalVisitResponse} from '../types';
import {UnifiedVisitForm} from './UnifiedVisitForm';

vi.mock('@/features/patients', () => ({
  PatientSelect: ({onChange}: {onChange: (patient: {id: string; fullName: string; phone: string | null} | null) => void}) =>
    <button type="button" onClick={() => onChange({id: '11111111-1111-4111-8111-111111111111', fullName: 'Ahmed Mohamed', phone: null})}>
      Choose Ahmed Mohamed
    </button>,
  usePatient: () => ({data: undefined})
}));

vi.mock('../use-medical-credentials', () => ({
  readMedicalToken: () => 'doctor-token'
}));

vi.mock('../api', () => ({
  createMedicalVisit: vi.fn(),
  createDiagnosis: vi.fn(),
  createVitalSigns: vi.fn(),
  createMedication: vi.fn()
}));

import {
  createDiagnosis,
  createMedicalVisit,
  createMedication,
  createVitalSigns
} from '../api';

const visit: MedicalVisitResponse = {
  id: '11111111-1111-4111-8111-111111111111',
  patientId: '11111111-1111-4111-8111-111111111111',
  doctorId: 'doctor-1',
  appointmentId: null,
  visitDate: '2026-11-20T10:30:00Z',
  chiefComplaint: 'Persistent headache',
  symptoms: null,
  clinicalNotes: null,
  diagnoses: [],
  medications: [],
  vitalSigns: []
};

function setup(locale: 'en' | 'ar' = 'en') {
  const client = new QueryClient({defaultOptions: {queries: {retry: false}, mutations: {retry: false}}});
  function wrapper({children}: {children: ReactNode}) {
    return (
      <QueryClientProvider client={client}>
        <NextIntlClientProvider locale={locale} messages={locale === 'ar' ? ar : en}
          onError={(error) => {throw error;}}>
          {children}
        </NextIntlClientProvider>
      </QueryClientProvider>
    );
  }
  return {wrapper};
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(createMedicalVisit).mockResolvedValue(visit);
  vi.mocked(createDiagnosis).mockResolvedValue({
    id: 'diagnosis-1',
    visitId: visit.id,
    patientId: visit.patientId,
    diagnosisName: 'Migraine',
    description: null,
    diagnosisDate: '2026-11-20'
  });
  vi.mocked(createVitalSigns).mockResolvedValue({
    id: 'vitals-1',
    bloodPressure: '120/80',
    heartRate: 72,
    temperature: 37,
    respiratoryRate: 16,
    spO2: 98,
    weight: 70,
    height: 175
  });
  vi.mocked(createMedication).mockResolvedValue({
    id: 'medication-1',
    visitId: visit.id,
    patientId: visit.patientId,
    medicationName: 'Paracetamol',
    dosage: '500 mg',
    frequency: 'Every 8 hours',
    duration: '5 days',
    instructions: 'After food'
  });
});

describe('UnifiedVisitForm', () => {
  it('creates visit, diagnosis, vitals, and prescription through one save action', async () => {
    const {wrapper} = setup();
    const onSuccess = vi.fn();
    const user = userEvent.setup();
    render(<UnifiedVisitForm onSuccess={onSuccess} />, {wrapper});

    await user.click(screen.getByRole('button', {name: 'Choose Ahmed Mohamed'}));
    await user.type(screen.getByLabelText('Chief complaint'), 'Persistent headache');
    await user.type(screen.getByLabelText('Diagnosis'), 'Migraine');
    await user.type(screen.getByLabelText('Blood pressure'), '120/80');
    await user.type(screen.getByLabelText(/Medication/), 'Paracetamol');
    await user.type(screen.getByLabelText(/Dosage/), '500 mg');
    await user.type(screen.getByLabelText(/Frequency/), 'Every 8 hours');

    await user.click(screen.getByRole('button', {name: 'Save complete visit'}));

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith(visit));
    expect(createMedicalVisit).toHaveBeenCalledOnce();
    expect(createDiagnosis).toHaveBeenCalledWith(visit.id, expect.objectContaining({
      diagnosisName: 'Migraine'
    }));
    expect(createVitalSigns).toHaveBeenCalledWith(
      visit.id,
      expect.objectContaining({bloodPressure: '120/80'}),
      'doctor-token'
    );
    expect(createMedication).toHaveBeenCalledOnce();
  });

  it('renders a centered RTL workflow in Arabic', () => {
    const {wrapper} = setup('ar');
    render(<UnifiedVisitForm onSuccess={vi.fn()} />, {wrapper});

    expect(screen.getByText('تفاصيل الزيارة')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'حفظ الزيارة كاملة'})).toBeInTheDocument();
  });
});

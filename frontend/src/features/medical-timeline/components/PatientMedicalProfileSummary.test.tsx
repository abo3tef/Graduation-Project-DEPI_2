import {render, screen} from '@testing-library/react';
import {NextIntlClientProvider} from 'next-intl';
import {describe, expect, it} from 'vitest';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import type {PatientMedicalProfileResponse} from '../types';
import {PatientMedicalProfileSummary} from './PatientMedicalProfileSummary';

const PROFILE: PatientMedicalProfileResponse = {
  patientId: '0b6f0000-0000-4000-8000-000000000001',
  fullName: 'Test Patient',
  dateOfBirth: '1990-04-12',
  age: 36,
  gender: 'Female',
  phone: null,
  address: '12 Main Street',
  bloodType: 'APositive',
  emergencyContact: 'Test emergency contact',
  allergies: [
    {
      id: '1',
      name: 'Penicillin',
      severity: 'Mild',
      description: null,
      notes: 'Avoid this medication'
    }
  ],
  chronicConditions: [
    {
      id: '2',
      name: 'Diabetes mellitus type 2',
      diagnosisDate: null,
      status: 'Active',
      notes: null
    }
  ]
};

function renderWithMessages(
  locale: 'en' | 'ar' = 'en',
  profile: PatientMedicalProfileResponse = PROFILE
) {
  const messages = locale === 'ar' ? arMessages : enMessages;

  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={messages}
      onError={(error) => {
        throw error;
      }}
    >
      <PatientMedicalProfileSummary profile={profile} />
    </NextIntlClientProvider>
  );
}

describe('PatientMedicalProfileSummary', () => {
  it('renders only the profile, contact, allergy, and condition response data', () => {
    renderWithMessages();

    expect(screen.getByRole('heading', {name: 'Test Patient'})).toBeInTheDocument();
    expect(screen.getByText(PROFILE.patientId)).toHaveAttribute('dir', 'ltr');
    expect(screen.getByText('Apr 12, 1990')).toBeInTheDocument();
    expect(screen.getByText('36')).toBeInTheDocument();
    expect(screen.getByText('Female')).toBeInTheDocument();
    expect(screen.getByText('APositive')).toBeInTheDocument();
    expect(screen.getByText('12 Main Street')).toBeInTheDocument();
    expect(screen.getByText('Test emergency contact')).toBeInTheDocument();
    expect(screen.getByText('Penicillin')).toBeInTheDocument();
    expect(screen.getByText('Mild')).toBeInTheDocument();
    expect(screen.getByText('Avoid this medication')).toBeInTheDocument();
    expect(screen.getByText('Diabetes mellitus type 2')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getAllByText('Not provided')).toHaveLength(4);
  });

  it('localizes profile labels and empty collections in Arabic', () => {
    renderWithMessages('ar', {...PROFILE, allergies: [], chronicConditions: []});

    expect(screen.getByText('البيانات الشخصية')).toBeInTheDocument();
    expect(screen.getByText('معلومات التواصل')).toBeInTheDocument();
    expect(screen.getByText('لا توجد حساسيات مسجلة.')).toBeInTheDocument();
    expect(screen.getByText('لا توجد أمراض مزمنة مسجلة.')).toBeInTheDocument();
  });
});

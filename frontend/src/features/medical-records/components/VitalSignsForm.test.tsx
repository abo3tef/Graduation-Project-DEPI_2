import {setMedicalTestSession, clearMedicalTestSession} from '../test-session';
import {act, fireEvent, render, renderHook, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactNode} from 'react';
import {beforeEach, describe, expect, it, vi} from 'vitest';
import en from '@/messages/en.json';
import ar from '@/messages/ar.json';
import {ApiError} from '@/lib/api';
import {createVitalSigns, updateVitalSigns} from '../api';
import {useMedicalCredentials} from '../use-medical-credentials';
import {VitalSignsForm} from './VitalSignsForm';
import type {VitalSignsResponse} from '../types';

vi.mock('../api', () => ({createVitalSigns: vi.fn(), updateVitalSigns: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify: {success: vi.fn(), error: vi.fn()}}));
const existing: VitalSignsResponse = {id: 'measurement', bloodPressure: '120/80', heartRate: 72,
  temperature: 37, respiratoryRate: 16, spO2: 98, weight: 70, height: 175};
const doctorId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const visitId = '11111111-1111-4111-8111-111111111111';

function setup(locale: 'en' | 'ar' = 'en') {
  const client = new QueryClient({defaultOptions: {mutations: {retry: false}}});
  function wrapper({children}: {children: ReactNode}) {
    return <QueryClientProvider client={client}><NextIntlClientProvider locale={locale}
      messages={locale === 'ar' ? ar : en} onError={error => {throw error;}}>{children}</NextIntlClientProvider></QueryClientProvider>;
  }
  const identity = renderHook(() => useMedicalCredentials());
  act(() => {clearMedicalTestSession(); setMedicalTestSession('test-form-token', {id: doctorId, doctorId});});
  return {wrapper, identity, client};
}
beforeEach(() => {vi.clearAllMocks();});

describe('VitalSignsForm', () => {
  it('rejects empty submission, focuses the first field, and submits optional measurements in create mode', async () => {
    const {wrapper} = setup();
    const done = vi.fn();
    vi.mocked(createVitalSigns).mockResolvedValue({...existing, heartRate: 80});
    render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={done} onCancel={vi.fn()} />, {wrapper});
    const user = userEvent.setup();
    await user.click(screen.getByRole('button', {name: 'Save vital signs'}));
    expect(screen.getByRole('alert')).toHaveTextContent('Enter at least one measurement.');
    expect(screen.getByLabelText('Blood pressure')).toHaveFocus();
    expect(createVitalSigns).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText('Heart rate'), '80');
    await user.click(screen.getByRole('button', {name: 'Save vital signs'}));
    await waitFor(() => expect(done).toHaveBeenCalledOnce());
    expect(createVitalSigns).toHaveBeenCalledWith(visitId, {bloodPressure: null, heartRate: 80,
      temperature: null, respiratoryRate: null, spO2: null, weight: null, height: null}, 'test-form-token');
    expect(screen.getByRole('status')).toHaveTextContent('Vital signs saved.');
  });

  it('edit sends a full replacement including null-cleared values', async () => {
    const {wrapper} = setup();
    vi.mocked(updateVitalSigns).mockResolvedValue(existing);
    render(<VitalSignsForm visitDoctorId={doctorId} mode="edit" vitalSigns={existing} visitId={visitId} onDone={vi.fn()} onCancel={vi.fn()} />, {wrapper});
    const user = userEvent.setup();
    for (const label of ['Blood pressure', 'Temperature', 'Respiratory rate', 'Oxygen saturation', 'Weight', 'Height']) {
      await user.clear(screen.getByLabelText(label));
    }
    await user.click(screen.getByRole('button', {name: 'Update vital signs'}));
    await waitFor(() => expect(updateVitalSigns).toHaveBeenCalledOnce());
    expect(updateVitalSigns).toHaveBeenCalledWith(visitId, existing.id, {bloodPressure: null, heartRate: 72,
      temperature: null, respiratoryRate: null, spO2: null, weight: null, height: null}, 'test-form-token');
  });

  it('locks duplicate submits and disables controls while pending', async () => {
    const {wrapper} = setup();
    let finish!: (value: VitalSignsResponse) => void;
    vi.mocked(createVitalSigns).mockReturnValue(new Promise(resolve => {finish = resolve;}));
    const done = vi.fn();
    const {container} = render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={done} onCancel={vi.fn()} />, {wrapper});
    fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
    fireEvent.submit(container.querySelector('form')!);
    fireEvent.submit(container.querySelector('form')!);
    await waitFor(() => expect(createVitalSigns).toHaveBeenCalledOnce());
    expect(screen.getByRole('button', {name: 'Saving…'})).toBeDisabled();
    expect(screen.getByLabelText('Heart rate')).toBeDisabled();
    await act(async () => {finish(existing);});
    await waitFor(() => expect(done).toHaveBeenCalledOnce());
  });

  it('shows backend field errors and focuses the rejected field', async () => {
    const {wrapper} = setup();
    vi.mocked(createVitalSigns).mockRejectedValue(new ApiError({kind: 'validation', status: 400,
      fieldErrors: {heartRate: ['Server rejected this measurement.']}}));
    render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={vi.fn()} onCancel={vi.fn()} />, {wrapper});
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('Heart rate'), '80');
    await user.click(screen.getByRole('button', {name: 'Save vital signs'}));
    await screen.findByText(en.MedicalRecords.vitalSigns.validation.serverRejected);
    expect(screen.queryByText('Server rejected this measurement.')).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Heart rate')).toHaveFocus());
    expect(screen.getByLabelText('Heart rate')).toHaveAttribute('aria-invalid', 'true');
    const describedBy = screen.getByLabelText('Heart rate').getAttribute('aria-describedby')!.split(' ');
    expect(describedBy.some(id => document.getElementById(id)?.textContent === en.MedicalRecords.vitalSigns.validation.serverRejected)).toBe(true);
  });

  it.each(['en', 'ar'] as const)('supports keyboard order, localized client errors and accessible descriptions in %s', async locale => {
    const {wrapper} = setup(locale);
    const messages = (locale === 'ar' ? ar : en).MedicalRecords.vitalSigns;
    const {container} = render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={vi.fn()} onCancel={vi.fn()} />, {wrapper});
    const user = userEvent.setup();
    for (const field of ['bloodPressure', 'heartRate', 'temperature', 'respiratoryRate', 'spO2', 'weight', 'height'] as const) {
      await user.tab();
      const input = screen.getByLabelText(messages.fields[field]);
      expect(input).toHaveFocus();
      expect(input).toHaveAttribute('dir', 'ltr');
      expect(input).toHaveAttribute('aria-invalid', 'false');
      expect(document.getElementById(input.getAttribute('aria-describedby')!)?.textContent).toBe(messages.units[field]);
    }
    await user.tab();
    expect(screen.getByRole('button', {name: messages.createSubmit})).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText(messages.fields.bloodPressure)).toHaveFocus();
    const summaryId = container.querySelector('form')!.getAttribute('aria-describedby')!;
    expect(document.getElementById(summaryId)).toHaveTextContent(messages.validation.atLeastOne);
    await user.type(screen.getByLabelText(messages.fields.bloodPressure), '80/80');
    await user.click(screen.getByRole('button', {name: messages.createSubmit}));
    const input = screen.getByLabelText(messages.fields.bloodPressure);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent(messages.validation.bloodPressureInvalid);
  });

  it('uses matching Arabic messages and cancels without saving', async () => {
    const {wrapper} = setup('ar');
    const cancel = vi.fn();
    render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={vi.fn()} onCancel={cancel} />, {wrapper});
    expect(screen.getByLabelText('ضغط الدم')).toHaveAttribute('dir', 'ltr');
    await userEvent.click(screen.getByRole('button', {name: ar.MedicalRecords.common.cancel}));
    expect(cancel).toHaveBeenCalledOnce();
    expect(createVitalSigns).not.toHaveBeenCalled();
  });

  it('hides an open form after credential change and does not call onDone for the old request', async () => {
    const {wrapper} = setup();
    let finish!: (value: VitalSignsResponse) => void;
    vi.mocked(createVitalSigns).mockReturnValue(new Promise(resolve => {finish = resolve;}));
    const done = vi.fn();
    const {container} = render(<VitalSignsForm visitDoctorId={doctorId} mode="create" visitId={visitId} onDone={done} onCancel={vi.fn()} />, {wrapper});
    fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
    fireEvent.submit(container.querySelector('form')!);
    await waitFor(() => expect(createVitalSigns).toHaveBeenCalledOnce());
    act(() => {setMedicalTestSession('different-test-token');});
    expect(container).toBeEmptyDOMElement();
    await act(async () => {finish(existing);});
    expect(done).not.toHaveBeenCalled();
  });
});

describe('VitalSignsForm submission context', () => {
  it('rechecks ownership in the submission handler before React removes the stale form', () => {
    const {wrapper} = setup();
    const {container} = render(<VitalSignsForm mode="create" visitId={visitId} visitDoctorId={doctorId} onDone={vi.fn()} onCancel={vi.fn()} />, {wrapper});
    fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
    const form = container.querySelector('form')!;
    act(() => {
      setMedicalTestSession('test-form-token', {id: doctorId, doctorId, role: 'Secretary'});
      fireEvent.submit(form);
    });
    expect(createVitalSigns).not.toHaveBeenCalled();
    expect(container).toBeEmptyDOMElement();
  });

  it('ignores a pending completion when the visit changes without a remount', async () => {
    const {wrapper} = setup();
    let finish!: (value: VitalSignsResponse) => void;
    vi.mocked(createVitalSigns).mockReturnValue(new Promise(resolve => {finish = resolve;}));
    const done = vi.fn();
    const {container, rerender} = render(<VitalSignsForm mode="create" visitId={visitId} visitDoctorId={doctorId} onDone={done} onCancel={vi.fn()} />, {wrapper});
    fireEvent.change(screen.getByLabelText('Heart rate'), {target: {value: '80'}});
    fireEvent.submit(container.querySelector('form')!);
    await waitFor(() => expect(createVitalSigns).toHaveBeenCalledOnce());
    rerender(<VitalSignsForm mode="create" visitId={doctorId} visitDoctorId={doctorId} onDone={done} onCancel={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
    await act(async () => {finish(existing);});
    expect(done).not.toHaveBeenCalled();
  });
});

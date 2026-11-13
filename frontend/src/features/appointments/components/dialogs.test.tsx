import {fireEvent, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {
  actAs,
  FakeApi,
  makeAppointment,
  problem,
  renderWithProviders,
  SECRETARY_ID
} from '../test/test-utils';
import {CancelDialog} from './CancelDialog';
import {RescheduleDialog} from './RescheduleDialog';
import {StatusDialog} from './StatusDialog';

const notify = vi.hoisted(() => ({success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn()}));
vi.mock('@/lib/utils/toast', () => ({notify}));

const SERVER_TEXT = 'English server text that must never be shown';

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  actAs('Secretary');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  notify.success.mockReset();
});

describe('CancelDialog', () => {
  it('requires a reason and sends nothing while it is empty or blank', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderWithProviders(<CancelDialog appointment={makeAppointment()} onClose={onClose} />);

    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));
    expect(await screen.findByText('This field is required.')).toBeInTheDocument();

    await user.type(screen.getByLabelText(/Reason for cancelling/), '   ');
    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));

    expect(api.calls).toHaveLength(0);
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByLabelText(/Reason for cancelling/)).toHaveFocus();
    expect(screen.getByLabelText(/Reason for cancelling/)).toBeInvalid();
  });

  it('rejects a reason longer than 500 characters and shows a live counter', async () => {
    const user = userEvent.setup();
    renderWithProviders(<CancelDialog appointment={makeAppointment()} onClose={vi.fn()} />);
    const reason = screen.getByLabelText(/Reason for cancelling/);

    fireEvent.change(reason, {target: {value: 'a'.repeat(501)}});
    expect(screen.getByText('501/500')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));

    expect(await screen.findByText('The cancellation reason is longer than 500 characters.')).toBeInTheDocument();
    expect(api.calls).toHaveLength(0);
  });

  it('cancels with the trimmed reason, sends the identity headers, notifies and closes', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const appointment = makeAppointment();
    api.on('POST', `/api/appointments/${appointment.id}/cancel`, {
      body: {...appointment, status: 'Cancelled', cancellationReason: 'Patient asked'}
    });
    renderWithProviders(<CancelDialog appointment={appointment} onClose={onClose} />);

    await user.type(screen.getByLabelText(/Reason for cancelling/), '  Patient asked  ');
    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    const [call] = api.callsTo('POST', `/api/appointments/${appointment.id}/cancel`);
    expect(call?.body).toEqual({cancellationReason: 'Patient asked'});
    expect(call?.headers['X-User-Id']).toBe(SECRETARY_ID);
    expect(call?.headers['X-User-Role']).toBe('Secretary');
    expect(notify.success).toHaveBeenCalledWith('Appointment cancelled.');
  });

  it('shows a translated message for a server failure and never the server text', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const appointment = makeAppointment();
    api.on('POST', `/api/appointments/${appointment.id}/cancel`, problem(400, 'appointment.not_open'));
    renderWithProviders(<CancelDialog appointment={appointment} onClose={onClose} />);

    await user.type(screen.getByLabelText(/Reason for cancelling/), 'reason');
    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));

    expect(
      await screen.findByText('This appointment can no longer be changed because it is completed, cancelled or a no-show.')
    ).toBeInTheDocument();
    expect(screen.queryByText(SERVER_TEXT)).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('maps a server-side length failure to the reason field', async () => {
    const user = userEvent.setup();
    const appointment = makeAppointment();
    api.on(
      'POST',
      `/api/appointments/${appointment.id}/cancel`,
      problem(400, 'validation.failed', {
        errors: {cancellationReason: ['too long']},
        errorCodes: {cancellationReason: ['MaximumLengthValidator']}
      })
    );
    renderWithProviders(<CancelDialog appointment={appointment} onClose={vi.fn()} />);

    await user.type(screen.getByLabelText(/Reason for cancelling/), 'reason');
    await user.click(screen.getByRole('button', {name: 'Cancel appointment'}));

    expect(await screen.findByText('The cancellation reason is longer than 500 characters.')).toBeInTheDocument();
  });

  it('can be dismissed without sending anything, and renders in Arabic', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderWithProviders(<CancelDialog appointment={makeAppointment()} onClose={onClose} />, 'ar');

    expect(screen.getByRole('heading', {name: 'إلغاء الموعد'})).toBeInTheDocument();
    expect(screen.getByLabelText(/سبب الإلغاء/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'إبقاء الموعد'}));

    expect(onClose).toHaveBeenCalled();
    expect(api.calls).toHaveLength(0);
  });

  it('renders nothing while no appointment is selected', () => {
    renderWithProviders(<CancelDialog appointment={null} onClose={vi.fn()} />);

    expect(screen.queryByLabelText(/Reason for cancelling/)).not.toBeInTheDocument();
  });
});

describe('RescheduleDialog', () => {
  const appointment = makeAppointment({appointmentDate: '2099-01-02', startTime: '09:00:00', endTime: '09:30:00'});

  it('starts with the current date and times', () => {
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={vi.fn()} />);

    expect(screen.getByLabelText(/^Date/)).toHaveValue('2099-01-02');
    expect(screen.getByLabelText(/Start time/)).toHaveValue('09:00');
    expect(screen.getByLabelText(/End time/)).toHaveValue('09:30');
  });

  it('validates before sending: missing end, end not after start, past date', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={vi.fn()} />);
    const save = () => screen.getByRole('button', {name: 'Save new time'});

    fireEvent.change(screen.getByLabelText(/End time/), {target: {value: ''}});
    await user.click(save());
    expect(await screen.findByText('This field is required.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/End time/), {target: {value: '09:00'}});
    await user.click(save());
    expect(await screen.findByText('The end time must be after the start time.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/End time/), {target: {value: '09:30'}});
    fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: '2001-01-01'}});
    await user.click(save());
    expect(await screen.findByText('The time cannot be in the past.')).toBeInTheDocument();

    expect(api.calls).toHaveLength(0);
  });

  it('sends only date and times (HH:mm:ss), notifies and closes', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    api.on('PUT', `/api/appointments/${appointment.id}/schedule`, {
      body: {...appointment, appointmentDate: '2099-01-05', startTime: '10:00:00', endTime: '10:45:00'}
    });
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={onClose} />);

    fireEvent.change(screen.getByLabelText(/^Date/), {target: {value: '2099-01-05'}});
    fireEvent.change(screen.getByLabelText(/Start time/), {target: {value: '10:00'}});
    fireEvent.change(screen.getByLabelText(/End time/), {target: {value: '10:45'}});
    await user.click(screen.getByRole('button', {name: 'Save new time'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    const [call] = api.callsTo('PUT', `/api/appointments/${appointment.id}/schedule`);
    expect(call?.body).toEqual({appointmentDate: '2099-01-05', startTime: '10:00:00', endTime: '10:45:00'});
    expect(notify.success).toHaveBeenCalledWith('Appointment rescheduled.');
  });

  it('reports an overlap in the dialog and keeps it open', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    api.on('PUT', `/api/appointments/${appointment.id}/schedule`, problem(409, 'appointment.overlap'));
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={onClose} />);

    await user.click(screen.getByRole('button', {name: 'Save new time'}));

    expect(await screen.findByText('The doctor already has an appointment that overlaps the selected time.')).toBeInTheDocument();
    expect(screen.queryByText(SERVER_TEXT)).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('maps server field errors back to the fields', async () => {
    const user = userEvent.setup();
    api.on(
      'PUT',
      `/api/appointments/${appointment.id}/schedule`,
      problem(400, 'validation.failed', {
        errors: {endTime: ['required']},
        errorCodes: {endTime: ['NotNullValidator']}
      })
    );
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={vi.fn()} />);

    await user.click(screen.getByRole('button', {name: 'Save new time'}));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(screen.getByLabelText(/End time/)).toHaveFocus();
  });

  it('is available in Arabic', () => {
    renderWithProviders(<RescheduleDialog appointment={appointment} onClose={vi.fn()} />, 'ar');

    expect(screen.getByRole('heading', {name: 'إعادة جدولة الموعد'})).toBeInTheDocument();
    expect(screen.getByLabelText(/وقت البدء/)).toBeInTheDocument();
  });
});

describe('StatusDialog', () => {
  const appointment = makeAppointment({status: 'Confirmed'});

  it.each([
    ['Confirmed', 'Confirm appointment', 'Confirm', 'Appointment confirmed.'],
    ['NoShow', 'Record a no-show', 'Record no-show', 'No-show recorded.'],
    ['Completed', 'Complete appointment', 'Complete appointment', 'Appointment completed.']
  ] as const)('%s: asks for confirmation, then updates', async (status, title, submit, toast) => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    api.on('PUT', `/api/appointments/${appointment.id}/status`, {body: {...appointment, status}});
    renderWithProviders(<StatusDialog state={{appointment, status}} onClose={onClose} />);

    expect(screen.getByRole('heading', {name: title})).toBeInTheDocument();
    expect(screen.getByText(appointment.patientName)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: submit}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(api.callsTo('PUT', `/api/appointments/${appointment.id}/status`)[0]?.body).toEqual({status});
    expect(notify.success).toHaveBeenCalledWith(toast);
  });

  it('explains a role refusal from the server in the dialog', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    api.on('PUT', `/api/appointments/${appointment.id}/status`, problem(403, 'appointment.complete_requires_doctor'));
    renderWithProviders(<StatusDialog state={{appointment, status: 'Completed'}} onClose={onClose} />);

    await user.click(screen.getByRole('button', {name: 'Complete appointment'}));

    expect(await screen.findByText('Only the doctor can mark an appointment as completed.')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('shows a network failure as a translated message', async () => {
    const user = userEvent.setup();
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    renderWithProviders(<StatusDialog state={{appointment, status: 'NoShow'}} onClose={vi.fn()} />, 'ar');

    await user.click(screen.getByRole('button', {name: 'تسجيل عدم الحضور'}));

    expect(await screen.findByText('تعذّر الاتصال بالخادم. تحقق من الاتصال وحاول مرة أخرى.')).toBeInTheDocument();
  });
});

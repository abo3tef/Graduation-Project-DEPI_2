import {screen, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {describe, expect, it, vi} from 'vitest';
import {APPOINTMENT_STATUSES} from '../types/appointment.types';
import {makeAppointment, renderWithProviders} from '../test/test-utils';
import {AppointmentStatusBadge} from './AppointmentStatusBadge';
import {AppointmentsTable} from './AppointmentsTable';

describe('AppointmentStatusBadge', () => {
  it.each([
    ['Scheduled', 'Scheduled', 'محجوز'],
    ['Confirmed', 'Confirmed', 'مؤكد'],
    ['Completed', 'Completed', 'مكتمل'],
    ['Cancelled', 'Cancelled', 'ملغي'],
    ['NoShow', 'No-show', 'لم يحضر']
  ] as const)('%s is written out in words in English and Arabic (never color alone)', (status, en, ar) => {
    const english = renderWithProviders(<AppointmentStatusBadge status={status} />, 'en');
    expect(english.container).toHaveTextContent(en);
    english.unmount();

    const arabic = renderWithProviders(<AppointmentStatusBadge status={status} />, 'ar');
    expect(arabic.container).toHaveTextContent(ar);
  });

  it('has a style for every status', () => {
    for (const status of APPOINTMENT_STATUSES) {
      const {container, unmount} = renderWithProviders(<AppointmentStatusBadge status={status} />);
      expect(container.firstElementChild?.className).toMatch(/border-/);
      unmount();
    }
  });
});

describe('AppointmentsTable', () => {
  const scheduled = makeAppointment({status: 'Scheduled', patientName: 'Mona Said'});
  const confirmed = makeAppointment({status: 'Confirmed', patientName: 'Omar Hassan'});
  const completed = makeAppointment({status: 'Completed', patientName: 'Sara Ibrahim'});
  const cancelled = makeAppointment({status: 'Cancelled', patientName: 'Khaled Mostafa'});
  const noShow = makeAppointment({status: 'NoShow', patientName: 'Mariam Samir'});
  const items = [scheduled, confirmed, completed, cancelled, noShow];

  function table() {
    return within(screen.getByTestId('appointments-table'));
  }

  const buttonNames = (rowText: string) =>
    within(table().getByText(rowText).closest('tr') as HTMLElement)
      .queryAllByRole('button')
      .map((button) => button.getAttribute('aria-label'));

  it('offers a secretary reschedule, cancel, confirm and no-show — never complete', () => {
    renderWithProviders(<AppointmentsTable items={items} role="Secretary" onAction={vi.fn()} />);

    expect(buttonNames('Mona Said')).toEqual([
      'Reschedule — Mona Said',
      'Cancel appointment — Mona Said',
      'Confirm — Mona Said',
      'No-show — Mona Said'
    ]);
    expect(buttonNames('Omar Hassan')).toEqual([
      'Reschedule — Omar Hassan',
      'Cancel appointment — Omar Hassan',
      'No-show — Omar Hassan'
    ]);
    expect(screen.queryByRole('button', {name: /^Complete/})).not.toBeInTheDocument();
  });

  it('also offers a doctor Complete', () => {
    renderWithProviders(<AppointmentsTable items={[scheduled, confirmed]} role="Doctor" onAction={vi.fn()} />);

    expect(buttonNames('Mona Said')).toContain('Complete — Mona Said');
    expect(buttonNames('Omar Hassan')).toContain('Complete — Omar Hassan');
  });

  it('offers nothing on final statuses and says so', () => {
    renderWithProviders(<AppointmentsTable items={items} role="Doctor" onAction={vi.fn()} />);

    for (const name of ['Sara Ibrahim', 'Khaled Mostafa', 'Mariam Samir']) {
      expect(buttonNames(name)).toEqual([]);
    }
    expect(table().getAllByText('No actions')).toHaveLength(3);
  });

  it('reports the chosen action with its appointment', async () => {
    const user = userEvent.setup();
    const onAction = vi.fn();
    renderWithProviders(<AppointmentsTable items={[scheduled]} role="Secretary" onAction={onAction} />);

    await user.click(table().getByRole('button', {name: 'Reschedule — Mona Said'}));
    await user.click(table().getByRole('button', {name: 'No-show — Mona Said'}));

    expect(onAction).toHaveBeenNthCalledWith(1, 'reschedule', scheduled);
    expect(onAction).toHaveBeenNthCalledWith(2, 'noShow', scheduled);
  });

  it('renders the same data as a table and as cards, with a labelled header row', () => {
    renderWithProviders(<AppointmentsTable items={[scheduled]} role="Secretary" onAction={vi.fn()} />);

    expect(table().getAllByRole('columnheader').map((header) => header.textContent)).toEqual([
      'Date', 'Time', 'Patient', 'Doctor', 'Status', 'Actions'
    ]);
    expect(within(screen.getByTestId('appointments-cards')).getByText('Mona Said')).toBeInTheDocument();
    expect(table().getByText('01012345601')).toHaveAttribute('dir', 'ltr');
  });

  it('formats the date and time for the locale without shifting the day', () => {
    renderWithProviders(
      <AppointmentsTable
        items={[makeAppointment({appointmentDate: '2099-01-02', startTime: '09:00:00', endTime: '09:30:00'})]}
        role="Secretary"
        onAction={vi.fn()}
      />
    );

    const text = table().getByText(/2099/).textContent ?? '';
    expect(text).toMatch(/Jan 2, 2099/);
    expect(screen.getAllByText(/9:00/).length).toBeGreaterThan(0);
  });

  it('uses Arabic headers and keeps phone numbers and times left-to-right', () => {
    renderWithProviders(<AppointmentsTable items={[scheduled]} role="Secretary" onAction={vi.fn()} />, 'ar');

    expect(table().getAllByRole('columnheader').map((header) => header.textContent)).toEqual([
      'التاريخ', 'الوقت', 'المريض', 'الطبيب', 'الحالة', 'الإجراءات'
    ]);
    expect(table().getByText('01012345601')).toHaveAttribute('dir', 'ltr');
    expect(table().getByRole('button', {name: 'إعادة جدولة — Mona Said'})).toBeInTheDocument();
  });

  it('marks the list busy while refreshing', () => {
    const {container} = renderWithProviders(<AppointmentsTable items={[scheduled]} role="Secretary" onAction={vi.fn()} busy />);

    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true');
  });
});

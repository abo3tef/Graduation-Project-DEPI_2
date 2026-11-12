import {describe, expect, it} from 'vitest';
import {APPOINTMENT_STATUSES, holdsSlot, isAppointmentStatus} from '../types/appointment.types';
import {getAllowedActions} from './status-actions';

describe('getAllowedActions', () => {
  it('offers a secretary everything except Complete on a Scheduled appointment', () => {
    expect(getAllowedActions('Scheduled', 'Secretary')).toEqual(['reschedule', 'cancel', 'confirm', 'noShow']);
  });

  it('does not offer Confirm again once Confirmed', () => {
    expect(getAllowedActions('Confirmed', 'Secretary')).toEqual(['reschedule', 'cancel', 'noShow']);
  });

  it('offers a doctor Complete as well', () => {
    expect(getAllowedActions('Scheduled', 'Doctor')).toEqual(['reschedule', 'cancel', 'confirm', 'noShow', 'complete']);
    expect(getAllowedActions('Confirmed', 'Doctor')).toEqual(['reschedule', 'cancel', 'noShow', 'complete']);
  });

  it.each(['Completed', 'Cancelled', 'NoShow'] as const)('offers nothing on a final status (%s)', (status) => {
    expect(getAllowedActions(status, 'Doctor')).toEqual([]);
    expect(getAllowedActions(status, 'Secretary')).toEqual([]);
  });

  it('offers nothing without a role', () => {
    for (const status of APPOINTMENT_STATUSES) {
      expect(getAllowedActions(status, null)).toEqual([]);
    }
  });

  it('never offers Complete to a secretary, for any status', () => {
    for (const status of APPOINTMENT_STATUSES) {
      expect(getAllowedActions(status, 'Secretary')).not.toContain('complete');
    }
  });
});

describe('status helpers', () => {
  it('knows the five contract statuses', () => {
    expect([...APPOINTMENT_STATUSES]).toEqual(['Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'NoShow']);
    expect(isAppointmentStatus('NoShow')).toBe(true);
    expect(isAppointmentStatus('No-Show')).toBe(false);
    expect(isAppointmentStatus(2)).toBe(false);
  });

  it('frees the slot only for Cancelled and NoShow', () => {
    expect(APPOINTMENT_STATUSES.filter((status) => !holdsSlot(status))).toEqual(['Cancelled', 'NoShow']);
  });
});

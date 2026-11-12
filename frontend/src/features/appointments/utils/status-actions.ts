import type {AppointmentStatus} from '../types/appointment.types';

export type AppointmentAction =
  | 'reschedule'
  | 'cancel'
  | 'confirm'
  | 'noShow'
  | 'complete';

export type ActionRole = 'Doctor' | 'Secretary' | null;

/**
 * Which actions to OFFER for an appointment. This only decides what the screen
 * shows; the backend enforces the real rules and answers 400/403 otherwise.
 *
 * - Open appointments (Scheduled, Confirmed) can be rescheduled and cancelled.
 * - Scheduled → Confirmed; Scheduled/Confirmed → No-Show.
 * - Complete is the doctor's decision and is never offered to a secretary.
 * - Completed, Cancelled and NoShow are final: no actions.
 */
export function getAllowedActions(
  status: AppointmentStatus,
  role: ActionRole
): readonly AppointmentAction[] {
  if (role === null) {
    return [];
  }

  const actions: AppointmentAction[] = [];

  switch (status) {
    case 'Scheduled':
      actions.push('reschedule', 'cancel', 'confirm', 'noShow');
      break;
    case 'Confirmed':
      actions.push('reschedule', 'cancel', 'noShow');
      break;
    default:
      return [];
  }

  if (role === 'Doctor') {
    actions.push('complete');
  }

  return actions;
}

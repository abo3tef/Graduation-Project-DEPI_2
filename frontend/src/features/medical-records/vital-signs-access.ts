import type {SessionState} from '@/features/auth';
import {isGuid} from './validation';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

/** UX permission only. The API must still authorize every read and write. */
export function canWriteVitalSigns(state: Pick<SessionState, 'status' | 'session'>, visitDoctorId: string): boolean {
  if (state.status !== 'authenticated' || !state.session) return false;
  const user = state.session.user;
  const doctorId = user.doctorId;
  if (user.role !== 'Doctor' || !doctorId || !isGuid(doctorId) || doctorId.trim().toLowerCase() === EMPTY_GUID) return false;
  if (!isGuid(user.id) || !isGuid(visitDoctorId)) return false;
  return user.id.trim().toLowerCase() === doctorId.trim().toLowerCase() &&
    doctorId.trim().toLowerCase() === visitDoctorId.trim().toLowerCase();
}

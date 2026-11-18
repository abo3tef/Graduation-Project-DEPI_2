import {describe, expect, it} from 'vitest';
import type {AuthSession, SessionState, SessionUser} from '@/features/auth';
import {canWriteVitalSigns} from './vital-signs-access';

const doctorId = 'abcdefab-abcd-4abc-8abc-abcdefabcdef';
const session: AuthSession = {accessToken: 'synthetic-token', expiresAtUtc: '2099-01-01T00:00:00Z',
  user: {id: doctorId, doctorId, role: 'Doctor', fullName: 'Synthetic Doctor', email: 'doctor@example.test'}};
function state(user: Partial<SessionUser> = {}): SessionState {
  return {status: 'authenticated', session: {...session, user: {...session.user, ...user}}, signedOutReason: null};
}

describe('vital signs ownership UX', () => {
  it('permits the owning doctor and compares GUIDs case-insensitively', () => {
    expect(canWriteVitalSigns(state(), doctorId)).toBe(true);
    expect(canWriteVitalSigns(state({id: doctorId.toUpperCase()}), doctorId.toUpperCase())).toBe(true);
  });

  it.each(['loading', 'anonymous'] as const)('rejects %s even when stale session data is supplied', status => {
    expect(canWriteVitalSigns({...state(), status}, doctorId)).toBe(false);
  });

  it('rejects missing session data', () => {
    expect(canWriteVitalSigns({...state(), session: null}, doctorId)).toBe(false);
  });

  it.each<Partial<SessionUser>>([
    {role: 'Secretary'}, {role: 'Admin'}, {doctorId: null}, {doctorId: ''}, {doctorId: 'not-a-guid'},
    {doctorId: '00000000-0000-0000-0000-000000000000', id: '00000000-0000-0000-0000-000000000000'},
    {id: 'invalid'}, {id: '11111111-1111-4111-8111-111111111111'}
  ])('fails closed for invalid identity %j', user => {
    expect(canWriteVitalSigns(state(user), doctorId)).toBe(false);
  });

  it.each(['', 'invalid', '11111111-1111-4111-8111-111111111111'])('rejects an invalid or different visit owner %s', owner => {
    expect(canWriteVitalSigns(state(), owner)).toBe(false);
  });
});

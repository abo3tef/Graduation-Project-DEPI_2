import {clearSession, getSessionState, setSession} from '../auth/session/session-store';

/** Synthetic sessions for the F9 integration tests only. */
export function setMedicalTestSession(token: string): void {
  if (getSessionState().session?.accessToken === token) return;
  setSession({accessToken: token, expiresAtUtc: new Date(Date.now() + 3600000).toISOString(),
    user: {id: token, fullName: 'Test Doctor', email: 'doctor@example.test', role: 'Doctor', doctorId: token}});
}
export {clearSession as clearMedicalTestSession};

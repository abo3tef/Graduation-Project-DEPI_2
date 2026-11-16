// Test-only bridge to the auth feature's session module. Component tests are allowed to reach
// into the module the production code reaches through `@/features/auth`; production code must
// keep using the feature's public index.
import {resetSessionForTests as resetSession, setSession} from '../../auth/session/session-store';

export {registerSessionAuth} from '../../auth/session/session-auth';

export const TEST_USER_IDS = {
  Secretary: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  Doctor: '11111111-1111-4111-8111-111111111111',
  Admin: '99999999-9999-4999-8999-999999999999'
} as const;

export type TestSessionRole = keyof typeof TEST_USER_IDS;

const NAMES: Record<TestSessionRole, string> = {
  Secretary: 'Sara Dev',
  Doctor: 'Dr. Dev One',
  Admin: 'Admin Dev'
};

/**
 * Signs in as `role` with a token that does not expire during the test. The token value is
 * predictable (`token-<Role>`) so tests can assert the `Authorization` header.
 */
export function signInForTests(role: TestSessionRole, userId: string = TEST_USER_IDS[role]): void {
  setSession({
    accessToken: `token-${role}`,
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: {
      id: userId,
      fullName: NAMES[role],
      email: `${role.toLowerCase()}@medilog.test`,
      role,
      doctorId: role === 'Doctor' ? userId : null
    }
  });
}

/** Forgets the session (and every subscriber) so each test starts from a clean store. */
export const resetSessionForTests = resetSession;

'use client';

import {useActingIdentity} from '@/features/auth';

export type ActorGate = {
  /** Raw identity state; `loading` renders the server-render placeholder. */
  identity: ReturnType<typeof useActingIdentity>;
  /** Stable cache-key component; 'anonymous' until an identity is known. */
  actor: string;
  /** The acting user's id once known (server-derived identity, D-3). */
  doctorId: string | null;
  /** True only for a ready *doctor* — medical data never loads otherwise. */
  ready: boolean;
};

/**
 * Doctor-only gate for the patient medical screens. Requests are enabled only
 * when `ready` is true, so a secretary (or nobody) never triggers a medical
 * request. The backend stays the enforcement boundary; this is UX gating.
 */
export function useActorGate(): ActorGate {
  const identity = useActingIdentity();
  const isReady = identity.status === 'ready';
  const isDoctor = isReady && identity.role === 'Doctor';

  return {
    identity,
    actor: identity.userId ?? 'anonymous',
    doctorId: isReady ? identity.userId : null,
    ready: isDoctor
  };
}

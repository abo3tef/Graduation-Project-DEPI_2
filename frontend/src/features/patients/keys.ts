const patientsRoot = ['patients'] as const;

/**
 * Actor-scoped query keys: switching the acting identity (dev identity today,
 * real sign-in later) can never surface another user's cached medical data.
 */
export const patientKeys = {
  all: (actor: string) => [...patientsRoot, actor] as const,
  search: (actor: string, search: string) =>
    [...patientKeys.all(actor), 'lookup', search] as const,
  profile: (actor: string, patientId: string) =>
    [...patientKeys.all(actor), 'profile', patientId] as const,
  history: (actor: string, patientId: string, pageNumber: number) =>
    [...patientKeys.all(actor), 'history', patientId, pageNumber] as const,
  notes: (actor: string, patientId: string, pageNumber: number) =>
    [...patientKeys.all(actor), 'notes', patientId, pageNumber] as const,
  timeline: (actor: string, patientId: string, pageNumber: number) =>
    [...patientKeys.all(actor), 'timeline', patientId, pageNumber] as const,
  documents: (actor: string, patientId: string, pageNumber: number) =>
    [...patientKeys.all(actor), 'documents', patientId, pageNumber] as const,
  labs: (actor: string, patientId: string, pageNumber: number) =>
    [...patientKeys.all(actor), 'labs', patientId, pageNumber] as const
};

const medicalRecordsRoot = ['medical-records'] as const;

/**
 * Query keys for the medical-records feature. Child mutations invalidate the parent visit
 * because the visit response already carries its diagnoses and medications (D-24).
 */
export const medicalRecordKeys = {
  all: medicalRecordsRoot,
  visit: (visitId: string) => [...medicalRecordsRoot, 'visit', visitId] as const,
  visitDetail: (visitId: string, scope: number) =>
    [...medicalRecordsRoot, 'visit', visitId, scope] as const,
  vitalSigns: (visitId: string) => [...medicalRecordsRoot, 'vital-signs', visitId] as const,
  vitalSignsPage: (visitId: string, scope: number, page: number, pageSize: number) =>
    [...medicalRecordsRoot, 'vital-signs', visitId, scope, page, pageSize] as const
};

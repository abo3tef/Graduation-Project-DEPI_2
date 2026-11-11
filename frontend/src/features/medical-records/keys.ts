const medicalRecordsRoot = ['medical-records'] as const;

/**
 * Query keys for the medical-records feature. Child mutations invalidate the parent visit
 * because the visit response already carries its diagnoses and medications (D-24).
 */
export const medicalRecordKeys = {
  all: medicalRecordsRoot,
  visit: (visitId: string) => [...medicalRecordsRoot, 'visit', visitId] as const
};

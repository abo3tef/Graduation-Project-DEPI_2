// Public API for the medical timeline feature.
// Cross-feature imports must go through this barrel only.
export {usePatientTimeline} from './hooks/use-patient-timeline';
export {useMedicalProfile} from './hooks/use-medical-profile';
export {TimelineEventList} from './components/TimelineEventList';
export {TimelineFilters} from './components/TimelineFilters';
export {PatientTimelinePanel} from './components/PatientTimelinePanel';
export {PatientMedicalHistoryScreen} from './components/PatientMedicalHistoryScreen';
export type {TimelineFiltersController, TimelineTypeFilter} from './hooks/use-timeline-filters';
export type {
  PatientAllergyResponse,
  PatientChronicConditionResponse,
  PatientMedicalProfileResponse,
  PatientTimelineEventResponse,
  PatientTimelineEventType,
  PatientTimelineQueryParams
} from './types';

// Public API for the medical records feature.
// Cross-feature imports must go through this barrel only.
export {MedicalRecordsWorkspace} from './components/MedicalRecordsWorkspace';
export {MedicalVisitScreen} from './components/MedicalVisitScreen';
export {NewVisitScreen} from './components/NewVisitScreen';
export {isGuid} from './validation';
export {LoadingPanel, StatePanel} from './components/AsyncState';
export {toQueryFailureKind} from './errors';
export type {QueryFailureKind} from './errors';

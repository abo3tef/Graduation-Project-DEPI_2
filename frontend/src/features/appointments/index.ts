// Public API for the appointments feature (secretary appointment & booking management).
// Route files import only these page-level components; everything else is internal.
export {AppointmentsPage} from './components/AppointmentsPage';
export {BookingForm} from './components/BookingForm';
export {PageFallback} from './components/PageFallback';
export {listAppointments} from './services/appointments.api';
export {getClinicNow, toUtcDate} from './utils/date-time';
export type {Appointment} from './types/appointment.types';

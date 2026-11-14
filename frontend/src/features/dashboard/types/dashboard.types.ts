/**
/// <summary>
/// TypeScript contracts for the Doctor Clinic Dashboard (Feature 5).
/// Matches DoctorDashboardContracts.cs in the backend and camelCase JSON serialization.
/// </summary>
*/

export type AppointmentStatus =
  | 'Scheduled'
  | 'Confirmed'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow'
  | string;

export interface TodayAppointmentSummary {
  appointmentId: string;
  patientId: string;
  patientName: string;
  startTime: string; // "HH:mm:ss"
  endTime: string | null; // "HH:mm:ss" or null
  reason: string | null;
  status: AppointmentStatus;
}

export interface DoctorDashboardMetrics {
  todayAppointmentsCount: number;
  todayVisitsCount: number;
  totalVisitsCount: number;
  totalPatientsCount: number;
  totalRevenue: number | null;
}

export interface DoctorDashboardResponse {
  doctorId: string;
  doctorName: string;
  specialization: string | null;
  metrics: DoctorDashboardMetrics;
  todayAppointments: TodayAppointmentSummary[];
}


import {apiRequest} from '@/lib/api';
import type {DoctorDashboardResponse} from '../types/dashboard.types';

const DASHBOARD_ENDPOINT = '/api/dashboard/doctor';

/**
 * Fetches the aggregated dashboard metrics and today's schedule for the acting doctor.
 * The JWT bearer credential is attached by the single shared API transport.
 */
export async function getDoctorDashboard(signal?: AbortSignal): Promise<DoctorDashboardResponse> {
  return apiRequest<DoctorDashboardResponse>(DASHBOARD_ENDPOINT, {
    method: 'GET',
    signal
  });
}


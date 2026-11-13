'use client';

import {useCallback, useMemo, useState} from 'react';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {
  type AppointmentFilters,
  type AppointmentStatus
} from '../types/appointment.types';
import {getClinicNow, isValidIsoDate} from '../utils/date-time';

type FilterState = Omit<AppointmentFilters, 'pageSize'>;

function createInitialState(): FilterState {
  // Default view: today onward in the clinic time zone, every doctor and status.
  return {
    doctorId: '',
    status: 'all',
    dateFrom: getClinicNow().date,
    dateTo: '',
    pageNumber: 1
  };
}

/** Local filter state (like the payments feature); every change returns to page 1. */
export function useAppointmentFilters() {
  // Computed once per mount: the default "today" must not change while the page is open.
  const [initial] = useState<FilterState>(createInitialState);
  const [state, setState] = useState<FilterState>(initial);

  const setDoctorId = useCallback((doctorId: string) => {
    setState((current) => ({...current, doctorId, pageNumber: 1}));
  }, []);

  const setStatus = useCallback((status: AppointmentStatus | 'all') => {
    setState((current) => ({...current, status, pageNumber: 1}));
  }, []);

  const setDateFrom = useCallback((dateFrom: string) => {
    setState((current) => ({...current, dateFrom, pageNumber: 1}));
  }, []);

  const setDateTo = useCallback((dateTo: string) => {
    setState((current) => ({...current, dateTo, pageNumber: 1}));
  }, []);

  const setPageNumber = useCallback((pageNumber: number) => {
    if (!Number.isFinite(pageNumber)) {
      return;
    }

    setState((current) => ({...current, pageNumber: Math.max(1, Math.trunc(pageNumber))}));
  }, []);

  const reset = useCallback(() => setState(initial), [initial]);

  const invalidRange =
    isValidIsoDate(state.dateFrom) &&
    isValidIsoDate(state.dateTo) &&
    state.dateTo < state.dateFrom;

  const filters = useMemo<AppointmentFilters>(
    () => ({
      doctorId: state.doctorId,
      status: state.status,
      // Malformed or half-typed dates are not sent to the API.
      dateFrom: isValidIsoDate(state.dateFrom) ? state.dateFrom : '',
      dateTo: isValidIsoDate(state.dateTo) ? state.dateTo : '',
      pageNumber: state.pageNumber,
      pageSize: DEFAULT_PAGE_SIZE
    }),
    [state]
  );

  const hasChanges =
    state.doctorId !== '' ||
    state.status !== 'all' ||
    state.dateFrom !== initial.dateFrom ||
    state.dateTo !== '';

  return {
    filters,
    state,
    invalidRange,
    hasChanges,
    setDoctorId,
    setStatus,
    setDateFrom,
    setDateTo,
    setPageNumber,
    reset
  };
}

export type AppointmentFiltersController = ReturnType<typeof useAppointmentFilters>;

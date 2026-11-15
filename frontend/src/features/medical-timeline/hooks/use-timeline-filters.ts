'use client';

import {useCallback, useMemo, useState} from 'react';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import type {PatientTimelineEventType, PatientTimelineQueryParams} from '../types';

/** `'all'` means "no type filter" (the backend parameter stays absent). */
export type TimelineTypeFilter = PatientTimelineEventType | 'all';

type TimelineFilterState = {
  eventType: TimelineTypeFilter;
  /** Raw `<input type="datetime-local">` values; empty string means unset. */
  fromInput: string;
  toInput: string;
  pageNumber: number;
};

function createInitialState(): TimelineFilterState {
  return {eventType: 'all', fromInput: '', toInput: '', pageNumber: 1};
}

/** A datetime-local value becomes an ISO instant; empty or malformed stays unset. */
function toInstant(value: string): string | undefined {
  if (value === '') {
    return undefined;
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

/**
 * Timeline filter state, following the appointments filter convention: every
 * change returns to page 1, the request only carries values the inputs can
 * express, and an invalid range is never sent — the backend (`from <= to`)
 * stays authoritative, the panel shows an inline message instead.
 */
export function useTimelineFilters() {
  const [initial] = useState<TimelineFilterState>(createInitialState);
  const [state, setState] = useState<TimelineFilterState>(initial);

  const setEventType = useCallback((eventType: TimelineTypeFilter) => {
    setState((current) => ({...current, eventType, pageNumber: 1}));
  }, []);

  const setFromInput = useCallback((fromInput: string) => {
    setState((current) => ({...current, fromInput, pageNumber: 1}));
  }, []);

  const setToInput = useCallback((toInput: string) => {
    setState((current) => ({...current, toInput, pageNumber: 1}));
  }, []);

  const setPageNumber = useCallback((pageNumber: number) => {
    if (!Number.isFinite(pageNumber)) {
      return;
    }

    setState((current) => ({...current, pageNumber: Math.max(1, Math.trunc(pageNumber))}));
  }, []);

  const reset = useCallback(() => setState(initial), [initial]);

  // `toISOString()` output is a fixed-format UTC instant, so lexicographic
  // order is chronological order.
  const fromInstant = toInstant(state.fromInput);
  const toInstantValue = toInstant(state.toInput);
  const invalidRange =
    fromInstant !== undefined && toInstantValue !== undefined && fromInstant > toInstantValue;

  const hasActiveFilters =
    state.eventType !== 'all' || state.fromInput !== '' || state.toInput !== '';

  const params = useMemo<PatientTimelineQueryParams>(
    () => ({
      eventType: state.eventType === 'all' ? undefined : state.eventType,
      // An invalid range is held back entirely so no request can be rejected
      // for `from > to`; the inline message asks the user to fix it.
      from: invalidRange ? undefined : fromInstant,
      to: invalidRange ? undefined : toInstantValue,
      pageNumber: state.pageNumber,
      pageSize: DEFAULT_PAGE_SIZE
    }),
    [state.eventType, fromInstant, toInstantValue, invalidRange, state.pageNumber]
  );

  return {
    state,
    params,
    invalidRange,
    hasActiveFilters,
    setEventType,
    setFromInput,
    setToInput,
    setPageNumber,
    reset
  };
}

export type TimelineFiltersController = ReturnType<typeof useTimelineFilters>;

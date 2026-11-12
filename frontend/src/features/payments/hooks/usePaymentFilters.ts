'use client';

import {useCallback, useEffect, useMemo, useState} from 'react';
import {DEFAULT_PAYMENT_FILTERS} from '../types/payment.types';
import type {
  PaymentFilters,
  PaymentMethod,
  PaymentStatus
} from '../types/payment.types';



const SEARCH_DEBOUNCE_MS = 300;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

type FilterState = {
  search: string;
  status: PaymentStatus | 'all';
  method: PaymentMethod | 'all';
  page: number;
};

const INITIAL_STATE: FilterState = {
  search: DEFAULT_PAYMENT_FILTERS.search ?? '',
  status: DEFAULT_PAYMENT_FILTERS.status ?? 'all',
  method: DEFAULT_PAYMENT_FILTERS.method ?? 'all',
  page: DEFAULT_PAYMENT_FILTERS.page
};

export function usePaymentFilters() {
  const [state, setState] = useState<FilterState>(INITIAL_STATE);
  const debouncedSearch = useDebouncedValue(state.search, SEARCH_DEBOUNCE_MS);

  const setSearch = useCallback((search: string) => {
    setState((current) => ({...current, search, page: 1}));
  }, []);

  const setStatus = useCallback((status: FilterState['status']) => {
    setState((current) => ({...current, status, page: 1}));
  }, []);

  const setMethod = useCallback((method: FilterState['method']) => {
    setState((current) => ({...current, method, page: 1}));
  }, []);

  const setPage = useCallback((page: number) => {
    if (!Number.isFinite(page)) {
      return;
    }

    setState((current) => ({...current, page: Math.max(1, Math.trunc(page))}));
  }, []);

  const resetFilters = useCallback(() => {
    setState(INITIAL_STATE);
  }, []);

  
  const filters = useMemo<PaymentFilters>(
    () => ({
      search: debouncedSearch.trim(),
      status: state.status,
      method: state.method,
      page: state.page,
      pageSize: DEFAULT_PAYMENT_FILTERS.pageSize
    }),
    [debouncedSearch, state.status, state.method, state.page]
  );

  const hasActiveFilters =
    state.search.trim() !== '' ||
    state.status !== 'all' ||
    state.method !== 'all';

  return {
    filters,
    search: state.search,
    status: state.status,
    method: state.method,
    page: state.page,
    hasActiveFilters,
    setSearch,
    setStatus,
    setMethod,
    setPage,
    resetFilters
  };
}

export type PaymentFiltersController = ReturnType<typeof usePaymentFilters>;
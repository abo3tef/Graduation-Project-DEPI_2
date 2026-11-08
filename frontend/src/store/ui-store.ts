'use client';

import {create} from 'zustand';

type UiState = {
  isSidebarOpen: boolean;
  openSidebar: () => void;
  closeSidebar: () => void;
  toggleSidebar: () => void;
};

/**
 * Global UI state only. Business state should live in feature stores
 * (for example `features/auth/store`).
 */
export const useUiStore = create<UiState>((set) => ({
  isSidebarOpen: false,
  openSidebar: () => set({isSidebarOpen: true}),
  closeSidebar: () => set({isSidebarOpen: false}),
  toggleSidebar: () =>
    set((state) => ({isSidebarOpen: !state.isSidebarOpen}))
}));

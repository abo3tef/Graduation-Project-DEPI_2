'use client';

import {useId} from 'react';
import type {ReactNode} from 'react';

export type TabId = string;

type TabsProps = {
  label: string;
  tabs: readonly {readonly id: TabId; readonly label: string}[];
  active: TabId;
  onChange: (id: TabId) => void;
  children: (activeId: TabId) => ReactNode;
};

const ARROW_KEYS = new Set(['ArrowLeft', 'ArrowRight']);

/**
 * Accessible tab list (WAI-ARIA tabs pattern): roving tabindex, ArrowLeft /
 * ArrowRight move among the tabs in list order (RTL mirroring is handled by the
 * browser's visual layout — the order stays the document order), Home/End jump.
 * Only the active panel is rendered, so each tab's query fetches on first open.
 */
export function Tabs({label, tabs, active, onChange, children}: TabsProps) {
  const baseId = useId();
  const activeIndex = Math.max(
    0,
    tabs.findIndex((tab) => tab.id === active)
  );

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (ARROW_KEYS.has(event.key) || event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
    } else {
      return;
    }

    const lastIndex = tabs.length - 1;
    let next = activeIndex;

    if (event.key === 'ArrowRight') {
      next = activeIndex === lastIndex ? 0 : activeIndex + 1;
    } else if (event.key === 'ArrowLeft') {
      next = activeIndex === 0 ? lastIndex : activeIndex - 1;
    } else if (event.key === 'Home') {
      next = 0;
    } else {
      next = lastIndex;
    }

    onChange(tabs[next]?.id ?? active);
  }

  return (
    <div className="grid gap-4">
      <div
        role="tablist"
        aria-label={label}
        onKeyDown={handleKeyDown}
        className="flex flex-wrap gap-2"
      >
        {tabs.map((tab, index) => {
          const isActive = tab.id === active;
          const panelId = `${baseId}-panel-${tab.id}`;

          return (
            <button
              key={tab.id}
              id={`${baseId}-tab-${tab.id}`}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls={panelId}
              tabIndex={isActive ? 0 : -1}
              onClick={() => onChange(tab.id)}
              className={`rounded-full border px-4 py-1.5 text-sm font-bold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand ${
                isActive
                  ? 'border-brand bg-brand-soft text-brand-strong'
                  : 'border-border bg-transparent text-muted hover:text-foreground'
              }`}
            >
              {tab.label}
              <span aria-hidden="true" className="ms-2 text-[11px] opacity-70">
                {index + 1}
              </span>
            </button>
          );
        })}
      </div>

      <div
        id={`${baseId}-panel-${active}`}
        role="tabpanel"
        aria-labelledby={`${baseId}-tab-${active}`}
        tabIndex={0}
      >
        {children(active)}
      </div>
    </div>
  );
}

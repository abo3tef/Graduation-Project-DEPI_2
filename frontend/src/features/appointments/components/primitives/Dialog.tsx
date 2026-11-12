'use client';

import {useEffect, useId, useRef, type ReactNode} from 'react';

type DialogProps = {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
};

/**
 * Accessible modal built on the native `<dialog>`: the browser provides the
 * focus trap, Escape handling, inert background and focus return. Content is
 * mounted only while open, so forms start fresh every time. Clicking the
 * backdrop closes it.
 */
export function Dialog({open, onClose, title, description, children}: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const dialog = ref.current;

    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onClose={onClose}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          event.currentTarget.close();
        }
      }}
      className="m-auto w-[min(34rem,calc(100%-2rem))] rounded-2xl border border-border bg-surface p-0 text-foreground shadow-2xl backdrop:bg-black/60"
    >
      {open ? (
        <div className="grid gap-4 p-5 sm:p-6">
          <header className="grid gap-1">
            <h2 id={titleId} className="m-0 text-lg font-black">
              {title}
            </h2>
            {description ? (
              <p id={descriptionId} className="m-0 text-sm leading-6 text-muted">
                {description}
              </p>
            ) : null}
          </header>
          {children}
        </div>
      ) : null}
    </dialog>
  );
}

'use client';

import {useId, useRef, useState, type KeyboardEvent} from 'react';
import {useTranslations} from 'next-intl';
import {usePatientLookup} from '../hooks/usePatients';
import {getPatientErrorKey} from '../services/patient-errors';
import {PATIENT_SEARCH_MIN_LENGTH, type PatientOption} from '../types/patient.types';
import {Button, controlClass, fieldAria} from './ui';

export type PatientSelectProps = {
  /** Id of the text input; the surrounding label points at it. */
  id: string;
  value: PatientOption | null;
  onChange: (patient: PatientOption | null) => void;
  /**
   * Narrows the search to one doctor's patients (the appointment form). Leave undefined when the
   * caller's own scope is wanted (a doctor's visit form).
   */
  doctorId?: string;
  /** True when a doctor must be chosen before searching (the appointment form). */
  requireDoctor?: boolean;
  error?: string | null;
  disabled?: boolean;
};

/**
 * Reusable searchable patient picker (F8) following the ARIA combobox/listbox pattern: arrow keys
 * move, Enter chooses, Escape closes. It shows only id-free, minimal data (name and phone) and
 * never exposes medical fields. Used by the appointment (F1) and visit (F4) screens instead of
 * typing a patient identifier.
 */
export function PatientSelect({
  id,
  value,
  onChange,
  doctorId = '',
  requireDoctor = false,
  error,
  disabled = false
}: PatientSelectProps) {
  const t = useTranslations('Patients.select');
  const errorsT = useTranslations('Patients.errors');
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const blocked = requireDoctor && doctorId === '';
  const search = usePatientLookup(text, doctorId);

  const results = blocked ? [] : (search.data ?? []);
  const showList = open && !value && !blocked && search.typedEnough && results.length > 0;
  const optionId = (index: number) => `${listId}-option-${index}`;

  function choose(patient: PatientOption) {
    onChange(patient);
    setText('');
    setOpen(false);
    setActiveIndex(-1);
  }

  function clear() {
    onChange(null);
    setText('');
    setActiveIndex(-1);
    inputRef.current?.focus();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (value) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        setOpen(true);
        setActiveIndex((index) => (results.length === 0 ? -1 : Math.min(index + 1, results.length - 1)));
        break;
      case 'ArrowUp':
        event.preventDefault();
        setActiveIndex((index) => (results.length === 0 ? -1 : Math.max(index - 1, 0)));
        break;
      case 'Enter': {
        const active = results[activeIndex];

        if (showList && active) {
          event.preventDefault(); // choosing an option must not submit the surrounding form
          choose(active);
        }
        break;
      }
      case 'Escape':
        if (showList) {
          event.preventDefault();
          setOpen(false);
          setActiveIndex(-1);
        }
        break;
    }
  }

  // One polite status line explains why there is nothing to pick.
  let status: string | null = null;

  if (!value) {
    if (blocked) {
      status = t('needsDoctor');
    } else if (!search.typedEnough) {
      status = t('minChars', {count: PATIENT_SEARCH_MIN_LENGTH});
    } else if (search.isError) {
      status = errorsT(getPatientErrorKey(search.error));
    } else if (search.isWaiting || search.isFetching) {
      status = t('searching');
    } else if (results.length === 0) {
      status = t('none');
    }
  }

  return (
    <div className="grid gap-2">
      <div className="flex gap-2">
        <input
          ref={inputRef}
          type="text"
          role="combobox"
          aria-expanded={showList}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={showList && activeIndex >= 0 ? optionId(activeIndex) : undefined}
          autoComplete="off"
          disabled={disabled || blocked}
          readOnly={value !== null}
          value={value ? value.fullName : text}
          placeholder={t('placeholder')}
          onChange={(event) => {
            setText(event.target.value);
            setOpen(true);
            setActiveIndex(-1);
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => {
            setOpen(false);
            setActiveIndex(-1);
          }}
          onKeyDown={handleKeyDown}
          className={controlClass}
          dir="auto"
          {...fieldAria(id, error)}
        />
        {value ? (
          <Button onClick={clear} disabled={disabled}>
            {t('change')}
          </Button>
        ) : null}
      </div>

      {showList ? (
        <ul
          id={listId}
          role="listbox"
          aria-label={t('results')}
          className="m-0 grid max-h-64 list-none gap-1 overflow-auto rounded-xl border border-border bg-surface p-1 shadow-lg"
        >
          {results.map((patient, index) => (
            <li
              key={patient.id}
              id={optionId(index)}
              role="option"
              aria-selected={index === activeIndex}
              // mousedown (not click) so the input does not blur and close the list first
              onMouseDown={(event) => {
                event.preventDefault();
                choose(patient);
              }}
              onMouseEnter={() => setActiveIndex(index)}
              className={`flex cursor-pointer flex-wrap items-baseline justify-between gap-2 rounded-lg px-3 py-2 text-sm ${
                index === activeIndex ? 'bg-brand-soft text-brand' : ''
              }`}
            >
              <span className="font-bold" dir="auto">
                {patient.fullName}
              </span>
              {patient.phone ? (
                <span className="text-xs text-muted" dir="ltr">
                  {patient.phone}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}

      <p className="m-0 min-h-4 text-[11px] leading-5 text-muted" role="status" aria-live="polite">
        {value ? (value.phone ?? '') : status}
      </p>
    </div>
  );
}

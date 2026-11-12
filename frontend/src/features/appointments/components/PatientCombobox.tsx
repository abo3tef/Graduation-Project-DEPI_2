'use client';

import {useId, useRef, useState, type KeyboardEvent} from 'react';
import {useTranslations} from 'next-intl';
import {usePatientSearch} from '../hooks/useLookups';
import {PATIENT_SEARCH_MIN_LENGTH, type PatientLookup} from '../types/appointment.types';
import {useErrorMessage} from '../hooks/useAppointmentMessages';
import {Button} from './primitives/Button';
import {controlClass, fieldAria} from './primitives/Field';

type PatientComboboxProps = {
  /** Id of the text input; the surrounding `Field` label points at it. */
  id: string;
  doctorId: string;
  value: PatientLookup | null;
  onChange: (patient: PatientLookup | null) => void;
  error?: string | null;
  disabled?: boolean;
};

/**
 * Searchable patient picker following the ARIA combobox/listbox pattern
 * (arrow keys, Enter to choose, Escape to close). It only searches the chosen
 * doctor's patients and shows id-free, minimal data (name and phone).
 */
export function PatientCombobox({id, doctorId, value, onChange, error, disabled = false}: PatientComboboxProps) {
  const t = useTranslations('Appointments.patientSearch');
  const errorMessage = useErrorMessage();
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const search = usePatientSearch(doctorId, text);

  const results = search.data ?? [];
  const hasDoctor = doctorId !== '';
  const showList = open && !value && hasDoctor && search.typedEnough && results.length > 0;
  const optionId = (index: number) => `${listId}-option-${index}`;

  function choose(patient: PatientLookup) {
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
          event.preventDefault(); // choosing an option must not submit the form
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
    if (!hasDoctor) {
      status = t('needsDoctor');
    } else if (!search.typedEnough) {
      status = t('minChars', {count: PATIENT_SEARCH_MIN_LENGTH});
    } else if (search.isError) {
      status = errorMessage(search.error);
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
          disabled={disabled || !hasDoctor}
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

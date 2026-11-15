'use client';

import {useId} from 'react';
import {controlClassName, fieldErrorClassName, hintClassName} from './auth-styles';

type AuthFieldProps = {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'tel';
  autoComplete?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  inputMode?: 'text' | 'email' | 'tel';
};

/**
 * Label + input + translated error/hint with the accessibility wiring the other forms use:
 * `aria-invalid`, `aria-describedby`, and an error that is announced with the field.
 */
export function AuthField({
  label,
  name,
  value,
  onChange,
  type = 'text',
  autoComplete,
  error,
  hint,
  required = true,
  disabled = false,
  inputMode
}: AuthFieldProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = error ? errorId : hint ? hintId : undefined;

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-bold text-foreground">
        {label}
      </label>
      <input
        id={id}
        name={name}
        type={type}
        inputMode={inputMode}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        autoComplete={autoComplete}
        required={required}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={controlClassName}
      />
      {error ? (
        <p id={errorId} className={fieldErrorClassName}>
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className={hintClassName}>
          {hint}
        </p>
      ) : null}
    </div>
  );
}

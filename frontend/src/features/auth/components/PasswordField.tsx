'use client';

import {useId, useState} from 'react';
import {FiEye, FiEyeOff} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {controlClassName, fieldErrorClassName, hintClassName} from './auth-styles';

type PasswordFieldProps = {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete?: string;
  error?: string;
  hint?: string;
  disabled?: boolean;
  required?: boolean;
};

/**
 * A password input with a show/hide toggle. The toggle is a button with a translated accessible
 * name, so the field stays usable from the keyboard and with a screen reader.
 */
export function PasswordField({
  label,
  name,
  value,
  onChange,
  autoComplete = 'current-password',
  error,
  hint,
  disabled = false,
  required = true
}: PasswordFieldProps) {
  const t = useTranslations('Auth.common');
  const id = useId();
  const [visible, setVisible] = useState(false);
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = error ? errorId : hint ? hintId : undefined;

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-bold text-foreground">
        {label}
      </label>
      <div className="relative">
        <input
          id={id}
          name={name}
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          autoComplete={autoComplete}
          required={required}
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          className={`${controlClassName} pe-10`}
        />
        <button
          type="button"
          onClick={() => setVisible((current) => !current)}
          aria-label={visible ? t('hidePassword') : t('showPassword')}
          aria-pressed={visible}
          tabIndex={-1}
          className="absolute inset-y-0 end-0 grid w-10 place-items-center text-muted transition-colors hover:text-foreground"
        >
          {visible ? (
            <FiEyeOff aria-hidden className="size-4" />
          ) : (
            <FiEye aria-hidden className="size-4" />
          )}
        </button>
      </div>
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

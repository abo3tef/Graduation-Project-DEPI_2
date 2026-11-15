import {describe, expect, it} from 'vitest';
import {isPathAllowedForRole, landingPathFor, resolveRedirectTarget} from './landing';

describe('landingPathFor', () => {
  it.each([
    ['Doctor', '/dashboard'],
    ['Secretary', '/appointments'],
    ['Admin', '/']
  ] as const)('%s lands on %s', (role, expected) => {
    expect(landingPathFor(role)).toBe(expected);
  });

  it('sends an unknown role to the public home page', () => {
    expect(landingPathFor(null)).toBe('/');
  });
});

describe('isPathAllowedForRole', () => {
  it('lets a doctor open the doctor areas', () => {
    expect(isPathAllowedForRole('Doctor', '/dashboard')).toBe(true);
    expect(isPathAllowedForRole('Doctor', '/appointments')).toBe(true);
    expect(isPathAllowedForRole('Doctor', '/appointments/new')).toBe(true);
    expect(isPathAllowedForRole('Doctor', '/medical-records/visits/new')).toBe(true);
  });

  it('keeps a secretary out of the clinical areas', () => {
    expect(isPathAllowedForRole('Secretary', '/appointments')).toBe(true);
    expect(isPathAllowedForRole('Secretary', '/medical-records')).toBe(false);
    expect(isPathAllowedForRole('Secretary', '/dashboard')).toBe(false);
  });

  it('gives an admin no signed-in area until F11', () => {
    expect(isPathAllowedForRole('Admin', '/dashboard')).toBe(false);
    expect(isPathAllowedForRole('Admin', '/')).toBe(false);
  });

  it('does not treat a longer path with the same prefix as allowed', () => {
    expect(isPathAllowedForRole('Secretary', '/appointments-archive')).toBe(false);
  });

  it('ignores the query and fragment when checking the path', () => {
    expect(isPathAllowedForRole('Doctor', '/dashboard?tab=1#top')).toBe(true);
  });

  it.each(['https://evil.example/appointments', '//evil.example/appointments', 'appointments', ''])(
    'rejects the non-local target %s',
    (path) => {
      expect(isPathAllowedForRole('Doctor', path)).toBe(false);
    }
  );
});

describe('resolveRedirectTarget', () => {
  it('honours a next value the role may open', () => {
    expect(resolveRedirectTarget('Doctor', '/appointments/new')).toBe('/appointments/new');
  });

  it('ignores a next value the role may not open', () => {
    expect(resolveRedirectTarget('Secretary', '/medical-records')).toBe('/appointments');
  });

  it('ignores an off-site next value', () => {
    expect(resolveRedirectTarget('Doctor', '//evil.example')).toBe('/dashboard');
  });

  it.each([null, undefined, '', '   '])('falls back to the landing page for %s', (next) => {
    expect(resolveRedirectTarget('Secretary', next)).toBe('/appointments');
  });

  it('sends an unknown role to the login screen', () => {
    expect(resolveRedirectTarget(null, '/dashboard')).toBe('/login');
  });
});

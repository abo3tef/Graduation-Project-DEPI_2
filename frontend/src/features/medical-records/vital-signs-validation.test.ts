import {describe, expect, it} from 'vitest';
import {initialVitalSignsValues, toVitalSignsRequest, validateVitalSignsForm, type VitalSignsField} from './vital-signs-validation';

describe('temporary Q-19 form validation (Pending Tech Lead Approval)', () => {
  it('rejects all-empty values and converts empty inputs to seven explicit nulls', () => {
    const values = initialVitalSignsValues();
    expect(validateVitalSignsForm(values).form?.key).toBe('atLeastOne');
    expect(toVitalSignsRequest(values)).toEqual({bloodPressure: null, heartRate: null, temperature: null,
      respiratoryRate: null, spO2: null, weight: null, height: null});
    expect(validateVitalSignsForm({...values, weight: '0.3'})).toEqual({});
  });

  it.each([
    ['heartRate', 20, 300, 19, 301], ['temperature', 30, 45, 29.99, 45.01],
    ['respiratoryRate', 4, 80, 3, 81], ['spO2', 50, 100, 49, 101],
    ['weight', 0.3, 500, 0.29, 500.01], ['height', 20, 260, 19.99, 260.01]
  ] as const)('%s accepts inclusive bounds and rejects out-of-range values', (field, min, max, below, above) => {
    for (const value of [min, max]) expect(validateVitalSignsForm({...initialVitalSignsValues(), [field]: String(value)})).toEqual({});
    for (const value of [below, above]) expect(validateVitalSignsForm({...initialVitalSignsValues(), [field]: String(value)})[field]?.key).toBe('range');
  });

  it.each(['heartRate', 'respiratoryRate', 'spO2'] as VitalSignsField[])('%s rejects fractions', field => {
    expect(validateVitalSignsForm({...initialVitalSignsValues(), [field]: '50.5'})[field]?.key).toBe('integer');
  });

  it.each([
    ['50/20', true], ['300/200', true], ['120/80', true], ['49/20', false], ['301/80', false],
    ['120/19', false], ['250/201', false], ['80/80', false], ['80/120', false],
    ['120', false], ['120-80', false], ['120/80/70', false], ['+120/80', false],
    ['120.5/80', false], ['abc/80', false], ['120 /80', false],
    ['00000000000000120/80', true], ['000000000000000120/80', false]
  ])('blood pressure %s validity is %s', (text, valid) => {
    expect(Object.keys(validateVitalSignsForm({...initialVitalSignsValues(), bloodPressure: String(text)})).length === 0).toBe(valid);
  });

  it.each(['NaN', 'Infinity', '0x50', '1e2', 'text'])('rejects non-decimal input %s', text => {
    expect(validateVitalSignsForm({...initialVitalSignsValues(), weight: text}).weight).toBeDefined();
  });

  it('creates a full replacement payload and preserves zero-like strings for validation', () => {
    const values = {...initialVitalSignsValues(), bloodPressure: ' 120/80 ', heartRate: '72', temperature: '37.2',
      respiratoryRate: '16', spO2: '98', weight: '70.5', height: '175'};
    expect(toVitalSignsRequest(values)).toEqual({bloodPressure: '120/80', heartRate: 72, temperature: 37.2,
      respiratoryRate: 16, spO2: 98, weight: 70.5, height: 175});
    expect(validateVitalSignsForm({...initialVitalSignsValues(), weight: '0'}).weight).toBeDefined();
  });
});

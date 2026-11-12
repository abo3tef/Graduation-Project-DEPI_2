import {readdirSync, readFileSync, statSync} from 'node:fs';
import {join} from 'node:path';
import {describe, expect, it} from 'vitest';

const ROOTS = ['src/features/appointments', 'src/features/auth', 'src/app/[locale]/appointments'];

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);

    if (statSync(path).isDirectory()) {
      return sourceFiles(path);
    }

    return /\.tsx$/.test(path) && !/\.test\.tsx$/.test(path) && !path.includes(`${join('appointments', 'test')}`)
      ? [path]
      : [];
  });
}

// Physical-direction utilities break the Arabic (RTL) layout. Logical ones
// (ms-, me-, ps-, pe-, start-, end-, text-start, text-end, rounded-s/e, border-s/e)
// flip automatically; explicit `ltr:` / `rtl:` variants are allowed.
const PHYSICAL_CLASS = /(?:^|[\s"'`{])(?:-?(?:ml|mr|pl|pr|left|right)-[\w[\].\/%-]+|text-(?:left|right)|border-[lr](?:-\w+)?|rounded-[lr](?:-\w+)?|float-(?:left|right))(?=$|[\s"'`}])/;

describe('RTL/LTR conventions', () => {
  const files = ROOTS.flatMap(sourceFiles);

  it('the check itself flags physical utilities and accepts logical ones', () => {
    for (const bad of [
      'className="ml-2 flex"', 'className="flex pr-4"', 'className="text-left"', 'className="absolute left-0"',
      'className="rounded-l-lg"', 'className="border-r border-border"', 'className="-ml-1"', 'className="float-right"'
    ]) {
      expect(PHYSICAL_CLASS.test(bad), bad).toBe(true);
    }

    for (const good of [
      'className="ms-2 flex"', 'className="pe-4 ps-2"', 'className="text-start text-end"', 'className="absolute start-0"',
      'className="rounded-s-lg"', 'className="border-s border-border"', 'className="ltr:hidden rtl:block"',
      'className="size-3.5 rtl:hidden"', 'className="mx-auto gap-2"'
    ]) {
      expect(PHYSICAL_CLASS.test(good), good).toBe(false);
    }
  });

  it('finds the feature sources to check', () => {
    expect(files.length).toBeGreaterThan(10);
  });

  it.each(files)('%s uses logical (not physical left/right) Tailwind utilities', (file) => {
    const offending = readFileSync(file, 'utf8')
      .split(/\r?\n/)
      .map((line, index) => ({line, number: index + 1}))
      .filter(({line}) => !line.trimStart().startsWith('//') && !line.trimStart().startsWith('*'))
      .filter(({line}) => PHYSICAL_CLASS.test(line));

    expect(offending.map(({number, line}) => `${number}: ${line.trim()}`)).toEqual([]);
  });
});

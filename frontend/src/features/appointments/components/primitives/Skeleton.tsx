type SkeletonRowsProps = {
  rows?: number;
  label: string;
};

/** Placeholder rows shown while a list loads; announced once via the label. */
export function SkeletonRows({rows = 5, label}: SkeletonRowsProps) {
  return (
    <div role="status" aria-label={label} className="grid gap-3">
      {Array.from({length: rows}, (_, index) => (
        <div
          key={index}
          aria-hidden="true"
          className="h-14 animate-pulse rounded-xl border border-border bg-surface-2"
        />
      ))}
    </div>
  );
}

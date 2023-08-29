const STARS = [1, 2, 3, 4, 5];

export function StarRating({ value, size = 'text-base' }: { value: number; size?: string }) {
  const rounded = Math.round(value * 2) / 2;
  return (
    <span className={`inline-flex ${size} leading-none`} role="img" aria-label={`${value.toFixed(1)} out of 5 stars`}>
      {STARS.map((n) => (
        <span key={n} aria-hidden className={n <= rounded ? 'text-amber-500' : n - 0.5 === rounded ? 'text-amber-300' : 'text-slate-300'}>★</span>
      ))}
    </span>
  );
}

export function StarInput({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Rating">
      {STARS.map((n) => (
        <button key={n} type="button" role="radio" aria-checked={value === n} aria-label={`${n} star${n > 1 ? 's' : ''}`}
          className={`text-2xl leading-none ${n <= value ? 'text-amber-500' : 'text-slate-300 hover:text-amber-300'}`}
          onClick={() => onChange(n)}>★</button>
      ))}
    </div>
  );
}

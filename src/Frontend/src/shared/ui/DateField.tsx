import React, { useEffect, useState } from 'react';
import { CalendarDays } from 'lucide-react';

interface DateFieldProps {
  /** Giá trị dạng yyyy-mm-dd (định dạng của input date). */
  value: string;
  onChange: (value: string) => void;
  min?: string;
  max?: string;
  required?: boolean;
  className?: string;
}

const toDisplay = (iso: string) => (iso ? iso.split('-').reverse().join('/') : '');

/** Chuyển dd/mm/yyyy sang yyyy-mm-dd, trả về '' nếu không hợp lệ. */
const fromDisplay = (text: string) => {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text.trim());
  if (!match) return '';
  const [, d, m, y] = match.map(Number);
  const date = new Date(y, m - 1, d);
  if (date.getFullYear() !== y || date.getMonth() !== m - 1 || date.getDate() !== d) return '';
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
};

/**
 * Ô ngày luôn hiển thị dd/mm/yyyy, không phụ thuộc cài đặt ngôn ngữ của trình duyệt.
 * Người dùng có thể gõ trực tiếp hoặc bấm biểu tượng lịch để chọn.
 */
export const DateField: React.FC<DateFieldProps> = ({ value, onChange, min, max, required, className = '' }) => {
  const [text, setText] = useState(toDisplay(value));

  useEffect(() => {
    setText(toDisplay(value));
  }, [value]);

  return (
    <div className="relative">
      <input
        type="text"
        inputMode="numeric"
        placeholder="dd/mm/yyyy"
        required={required}
        pattern="\d{2}/\d{2}/\d{4}"
        value={text}
        onChange={(e) => {
          const next = e.target.value;
          setText(next);
          const iso = fromDisplay(next);
          if (iso && (!min || iso >= min) && (!max || iso <= max)) onChange(iso);
        }}
        onBlur={() => setText(toDisplay(value))}
        className={`${className} pr-9`}
      />
      <CalendarDays className="pointer-events-none absolute inset-y-0 right-3 my-auto h-4 w-4 text-muted-foreground" />
      <input
        type="date"
        tabIndex={-1}
        aria-label="Chọn ngày"
        value={value}
        min={min}
        max={max}
        onChange={(e) => onChange(e.target.value)}
        className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
      />
    </div>
  );
};

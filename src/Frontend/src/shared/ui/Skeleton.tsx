import React from 'react';

/** Khung chờ tải dữ liệu, dùng thay cho dấu "..." hoặc dòng "Đang tải". */
export const Skeleton: React.FC<{ className?: string }> = ({ className = '' }) => (
  <div className={`skeleton ${className}`} aria-hidden="true" />
);

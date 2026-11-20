/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        // Bảng màu: #2563EB (primary), #383838 (chữ), #686868 (chữ phụ), #F2F2F4 (nền)
        background: '#F2F2F4',
        foreground: '#383838',
        card: {
          DEFAULT: '#FFFFFF',
          foreground: '#383838',
        },
        popover: {
          DEFAULT: '#FFFFFF',
          foreground: '#383838',
        },
        primary: {
          DEFAULT: '#2563EB',
          foreground: '#FFFFFF',
          hover: '#1D4ED8',
          soft: '#EAF1FF',
        },
        secondary: {
          DEFAULT: '#F2F2F4',
          foreground: '#383838',
        },
        muted: {
          DEFAULT: '#F2F2F4',
          foreground: '#686868',
        },
        accent: {
          DEFAULT: '#2563EB',
          foreground: '#FFFFFF',
        },
        destructive: {
          DEFAULT: '#DC2626',
          foreground: '#FFFFFF',
        },
        border: '#E4E4E9',
        input: '#E4E4E9',
        ring: '#2563EB',
      },
      fontFamily: {
        display: ['"Plus Jakarta Sans"', 'sans-serif'],
        sans: ['Inter', 'sans-serif'],
      },
      boxShadow: {
        card: '0 1px 2px rgba(16, 24, 40, 0.04), 0 2px 6px -1px rgba(16, 24, 40, 0.05)',
        lift: '0 10px 30px -12px rgba(16, 24, 40, 0.18)',
        glow: '0 12px 28px -10px rgba(37, 99, 235, 0.45)',
      },
      keyframes: {
        'fade-up': {
          '0%': { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'fade-in': {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        shimmer: {
          '0%': { backgroundPosition: '-400px 0' },
          '100%': { backgroundPosition: '400px 0' },
        },
      },
      animation: {
        'fade-up': 'fade-up 0.35s ease-out both',
        'fade-in': 'fade-in 0.25s ease-out both',
        shimmer: 'shimmer 1.4s linear infinite',
      },
    },
  },
  plugins: [],
};

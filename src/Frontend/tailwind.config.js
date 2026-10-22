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
        background: '#0F172A', // Slate 900
        card: {
          DEFAULT: '#1E293B', // Slate 800
          foreground: '#F8FAFC',
        },
        popover: {
          DEFAULT: '#1E293B',
          foreground: '#F8FAFC',
        },
        primary: {
          DEFAULT: '#4F46E5', // Indigo 600
          foreground: '#FFFFFF',
          hover: '#4338CA',
        },
        secondary: {
          DEFAULT: '#334155', // Slate 700
          foreground: '#F8FAFC',
        },
        muted: {
          DEFAULT: '#1E293B',
          foreground: '#94A3B8', // Slate 400
        },
        accent: {
          DEFAULT: '#06B6D4', // Cyan 500
          foreground: '#0F172A',
        },
        destructive: {
          DEFAULT: '#EF4444', // Rose 500
          foreground: '#FFFFFF',
        },
        border: '#334155', // Slate 700
        input: '#334155',
        ring: '#4F46E5',
      },
      fontFamily: {
        display: ['"Plus Jakarta Sans"', 'sans-serif'],
        sans: ['Inter', 'sans-serif'],
      },
    },
  },
  plugins: [],
};

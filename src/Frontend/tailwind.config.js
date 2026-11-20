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
        border: '#DEDEE3',
        input: '#DEDEE3',
        ring: '#2563EB',
      },
      fontFamily: {
        display: ['"Plus Jakarta Sans"', 'sans-serif'],
        sans: ['Inter', 'sans-serif'],
      },
    },
  },
  plugins: [],
};

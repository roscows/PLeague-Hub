/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: '#ec1c24',
        ink: '#071d2b',
        surface: '#f2f4f7',
        pitch: '#087f5b'
      }
    }
  },
  plugins: []
};

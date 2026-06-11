/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        mabuntle: {
          primary: '#3A1D32',
          primaryHover: '#2A1425',
          accent: '#B76E79',
          background: '#FFF9F4',
          surface: '#FFFFFF',
          text: '#1F1A1C',
          muted: '#6F5E66'
        },
        mabuntle: {
          ink: '#16120C',
          muted: '#665C4B',
          panel: '#FFFFFF',
          panelSoft: '#F8F7F3',
          line: '#E8E0D2',
          gold: '#BB9A62',
          goldDeep: '#9B7C49'
        },
        client: {
          bg: '#F8FAFC',
          text: '#0F172A',
          muted: '#64748B',
          border: '#E2E8F0',
          brand: '#111827',
          info: '#2563EB'
        }
      }
    }
  },
  plugins: []
};

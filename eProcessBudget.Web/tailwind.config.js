// Configuration Tailwind de la feuille figée wwwroot/css/tailwind.min.css (docs/07-composants-ui.md).
//
// Seules les classes écrites en toutes lettres dans les fichiers ci-dessous sont générées :
// ne composez jamais un nom de classe par morceaux (« bg-" + couleur »), écrivez-le en entier.
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Components/**/*.{razor,cs}',
    './Features/**/*.{razor,cs}',
    './Securite/**/*.cs',
    './wwwroot/js/**/*.js',
  ],
  theme: {
    extend: {
      // Couleurs de la charte en variables CSS : elles viennent d'appsettings.json à
      // l'exécution (App.razor), la feuille n'a pas à être régénérée quand elles changent.
      colors: {
        'app-primaire':   'var(--app-primaire)',
        'app-secondaire': 'var(--app-secondaire)',
        'app-tertiaire':  'var(--app-tertiaire)',
        'app-accent':     'var(--app-accent)',
      },
      fontFamily: { sans: ['Inter', 'system-ui', 'sans-serif'] },
    },
  },
};

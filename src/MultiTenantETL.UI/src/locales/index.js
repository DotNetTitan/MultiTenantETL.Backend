import en from './en.json';
import es from './es.json';
import fr from './fr.json';
import de from './de.json';
import it from './it.json';
import pt from './pt.json';

export const messages = {
  en,
  es,
  fr,
  de,
  it,
  pt
};

export const availableLocales = [
  { code: 'en', name: 'English', flag: '🇺🇸' },
  { code: 'es', name: 'Español', flag: '🇪🇸' },
  { code: 'fr', name: 'Français', flag: '🇫🇷' },
  { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
  // Temporarily disabled
  // { code: 'it', name: 'Italiano', flag: '🇮🇹' },
  // { code: 'pt', name: 'Português', flag: '🇵🇹' }
];

export const defaultLocale = 'en';
export const fallbackLocale = 'en';

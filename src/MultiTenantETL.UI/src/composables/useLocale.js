import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useLocale as useVuetifyLocale } from 'vuetify';
import { availableLocales } from '@/locales';

export function useLocale() {
  const { locale, t } = useI18n();
  const vuetifyLocale = useVuetifyLocale();

  const currentLocale = computed({
    get: () => locale.value,
    set: (newLocale) => {
      locale.value = newLocale;
      // Update Vuetify locale
      vuetifyLocale.current.value = newLocale;
      // Persist to localStorage
      localStorage.setItem('user-locale', newLocale);
      // Update HTML lang attribute
      document.documentElement.setAttribute('lang', newLocale);
    }
  });

  const changeLocale = (newLocale) => {
    currentLocale.value = newLocale;
  };

  const getCurrentLocaleName = computed(() => {
    const current = availableLocales.find(l => l.code === currentLocale.value);
    return current ? current.name : 'English';
  });

  const getCurrentLocaleFlag = computed(() => {
    const current = availableLocales.find(l => l.code === currentLocale.value);
    return current ? current.flag : '🇺🇸';
  });

  return {
    locale: currentLocale,
    availableLocales,
    changeLocale,
    currentLocaleName: getCurrentLocaleName,
    currentLocaleFlag: getCurrentLocaleFlag,
    t
  };
}

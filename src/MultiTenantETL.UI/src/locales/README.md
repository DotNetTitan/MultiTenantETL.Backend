# Internationalization (i18n) Implementation

This application uses **Vue I18n v9** for complete multi-language support.

## Supported Languages

- 🇺🇸 **English** (en) - Default
- 🇪🇸 **Español** (es) - Spanish
- 🇫🇷 **Français** (fr) - French
- 🇩🇪 **Deutsch** (de) - German
- 🇮🇹 **Italiano** (it) - Italian
- 🇵🇹 **Português** (pt) - Portuguese

## Usage in Components

### Template Usage

```vue
<template>
  <!-- Simple translation -->
  <h1>{{ $t('dashboard.title') }}</h1>
  
  <!-- With interpolation -->
  <p>{{ $t('messages.saveSuccess', { item: 'Pipeline' }) }}</p>
  
  <!-- In attributes -->
  <v-btn :title="$t('common.save')">
    {{ $t('common.save') }}
  </v-btn>
</template>
```

### Script Setup Usage

```vue
<script setup>
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

// Use in JavaScript
const message = t('common.loading');
console.log(message); // "Loading..."
</script>
```

### Using the Locale Composable

```vue
<script setup>
import { useLocale } from '@/composables/useLocale';

const { locale, changeLocale, availableLocales } = useLocale();

// Change language
changeLocale('es');

// Get current locale
console.log(locale.value); // 'es'
</script>
```

## Translation File Structure

All translations are organized in JSON files under `src/locales/`:

```
src/locales/
├── en.json          # English translations
├── es.json          # Spanish translations
├── fr.json          # French translations
├── de.json          # German translations
├── it.json          # Italian translations
├── pt.json          # Portuguese translations
├── index.js         # Locale configuration
└── README.md        # This file
```

### Translation Keys Organization

```json
{
  "common": {          // Shared across all pages
    "save": "Save",
    "cancel": "Cancel"
  },
  "nav": {             // Navigation items
    "dashboard": "Dashboard",
    "pipelines": "Pipelines"
  },
  "dashboard": {       // Page-specific
    "title": "Dashboard",
    "totalPipelines": "Total Pipelines"
  }
}
```

## Adding New Translations

### 1. Add to English (en.json)

```json
{
  "myFeature": {
    "title": "My Feature",
    "description": "This is my feature"
  }
}
```

### 2. Add to Spanish (es.json)

```json
{
  "myFeature": {
    "title": "Mi Función",
    "description": "Esta es mi función"
  }
}
```

### 3. Add to French (fr.json)

```json
{
  "myFeature": {
    "title": "Ma Fonctionnalité",
    "description": "Ceci est ma fonctionnalité"
  }
}
```

### 4. Use in Component

```vue
<template>
  <h1>{{ $t('myFeature.title') }}</h1>
  <p>{{ $t('myFeature.description') }}</p>
</template>
```

## Adding a New Language

### 1. Create Translation File

Create `src/locales/de.json` for German:

```json
{
  "common": {
    "save": "Speichern",
    "cancel": "Abbrechen"
  }
}
```

### 2. Update index.js

```javascript
import de from './de.json';

export const messages = {
  en,
  es,
  fr,
  de  // Add new language
};

export const availableLocales = [
  { code: 'en', name: 'English', flag: '🇺🇸' },
  { code: 'es', name: 'Español', flag: '🇪🇸' },
  { code: 'fr', name: 'Français', flag: '🇫🇷' },
  { code: 'de', name: 'Deutsch', flag: '🇩🇪' }  // Add new language
];
```

### 3. Update Vuetify Locale (main.js)

```javascript
import { en, es, fr, de } from 'vuetify/locale';

const vuetify = createVuetify({
  locale: {
    locale: savedLocale,
    fallback: fallbackLocale,
    messages: { en, es, fr, de }  // Add new language
  }
});
```

## Features

### Language Persistence

The selected language is automatically saved to `localStorage` and restored on page reload.

### Vuetify Integration

Vuetify components (date pickers, pagination, etc.) are automatically translated to match the selected language.

### AI Chatbot Support

The AI chatbot (Maeve) responds in the user's selected language automatically.

### HTML Lang Attribute

The `<html lang="...">` attribute is automatically updated when the language changes for better accessibility and SEO.

## Best Practices

1. **Use Descriptive Keys**: `dashboard.totalPipelines` not `d.tp`
2. **Group by Feature**: Organize translations by page or feature
3. **Keep Consistent**: Use the same structure across all language files
4. **Avoid Hardcoded Text**: Always use translation keys, never hardcode strings
5. **Test All Languages**: Switch languages and verify all text displays correctly

## Interpolation

Use placeholders for dynamic content:

```json
{
  "messages": {
    "saveSuccess": "{item} saved successfully"
  }
}
```

```vue
<template>
  {{ $t('messages.saveSuccess', { item: 'Pipeline' }) }}
  <!-- Output: "Pipeline saved successfully" -->
</template>
```

## Pluralization

Vue I18n supports pluralization:

```json
{
  "connectors": {
    "fields": "field | fields"
  }
}
```

```vue
<template>
  {{ $t('connectors.fields', 1) }}  <!-- "field" -->
  {{ $t('connectors.fields', 5) }}  <!-- "fields" -->
</template>
```

## Troubleshooting

### Translation Not Showing

1. Check the key exists in all language files
2. Verify the key path is correct
3. Check browser console for missing translation warnings

### Language Not Changing

1. Clear localStorage: `localStorage.removeItem('user-locale')`
2. Hard refresh the page (Ctrl+Shift+R)
3. Check that the language code matches exactly

### Vuetify Components Not Translated

Ensure Vuetify locale is configured in `main.js` with the correct language imports.

## Resources

- [Vue I18n Documentation](https://vue-i18n.intlify.dev/)
- [Vuetify Internationalization](https://vuetifyjs.com/en/features/internationalization/)

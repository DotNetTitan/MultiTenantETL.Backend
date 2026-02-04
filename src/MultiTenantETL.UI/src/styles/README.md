# Styles Guide

## Theme Colors

All theme colors are centrally defined in `src/main.js` using Vuetify's theme system.

### Dark Theme Colors
- Primary: `#2196F3` (Blue)
- Secondary: `#424242` (Gray)
- Success: `#4CAF50` (Green)
- Error: `#FF5252` (Red)
- Warning: `#FFC107` (Amber)
- Info: `#2196F3` (Blue)

### Light Theme Colors
- Primary: `#1565C0` (Darker Blue)
- Secondary: `#424242` (Gray)
- Success: `#388E3C` (Darker Green)
- Error: `#D32F2F` (Darker Red)
- Warning: `#F57C00` (Darker Orange)
- Info: `#0288D1` (Darker Blue)

## Using Theme Colors in Components

### CSS Variables (Preferred)
Always use Vuetify's CSS variables instead of hardcoded colors:

```scss
// Good ✓
.my-element {
  color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-surface), 0.95);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

// Bad ✗
.my-element {
  color: #2196F3;
  background: rgba(33, 33, 33, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
}
```

### Available CSS Variables
- `--v-theme-primary`
- `--v-theme-secondary`
- `--v-theme-success`
- `--v-theme-error`
- `--v-theme-warning`
- `--v-theme-info`
- `--v-theme-surface`
- `--v-theme-background`
- `--v-theme-on-surface`
- `--v-theme-on-background`
- `--v-theme-surface-variant`

## Global Utility Classes

The `global.scss` file provides reusable classes and mixins:

### Classes
- `.hover-primary` - Adds primary color hover effect
- `.glass-card` - Glass morphism effect with blur
- `.gradient-bg` - Gradient background using theme colors
- `.execution-row` - Hover effect for execution rows
- `.form-view` - Standard form view layout
- `.app-timeline-card` - Timeline card styling

### Mixins
```scss
@include card-hover;        // Hover effect for cards
@include glass-effect;      // Glass morphism effect
@include gradient-background; // Gradient background
```

### Status Classes
- `.status-completed` - Green color for completed status
- `.status-running` - Blue color for running status
- `.status-failed` - Red color for failed status
- `.status-pending` - Amber color for pending status

## Best Practices

1. **Never hardcode colors** - Always use CSS variables or Vuetify color props
2. **Use global classes** - Reuse existing utility classes before creating new ones
3. **Theme-aware styling** - Use `.v-theme--dark` and `.v-theme--light` selectors when needed
4. **Consistent spacing** - Follow Vuetify's spacing system (multiples of 4px)
5. **Scoped styles** - Use `<style scoped>` in components to avoid conflicts

## Updating Theme Colors

To change theme colors, edit `src/main.js`:

```javascript
const darkTheme = {
  dark: true,
  colors: {
    primary: '#YOUR_COLOR_HERE',
    // ... other colors
  }
}
```

All components using CSS variables will automatically update.

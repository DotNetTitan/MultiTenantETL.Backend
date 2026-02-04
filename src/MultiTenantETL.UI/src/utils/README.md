# Request Debouncing & Rate Limiting Utilities

This module provides utilities to prevent API spam and improve performance.

## Usage Examples

### 1. Debounce Search Input

```javascript
import { debounce } from '@/utils/requestDebounce'

// In your component
const debouncedSearch = debounce(async (query) => {
  const results = await api.get(`/api/search?q=${query}`)
  return results.data
}, 300) // Wait 300ms after user stops typing

// Use in input handler
async function handleSearchInput(event) {
  const results = await debouncedSearch(event.target.value)
  // Update UI with results
}
```

### 2. Throttle Button Clicks

```javascript
import { throttle } from '@/utils/requestDebounce'

// Prevent rapid-fire submissions
const throttledSubmit = throttle(async () => {
  await api.post('/api/data', formData)
}, 1000) // Max 1 request per second
```

### 3. Cancel Duplicate Requests

```javascript
import { cancelDuplicateRequest } from '@/utils/requestDebounce'

// Automatically cancel previous request if new one starts
async function fetchUserData(userId) {
  return cancelDuplicateRequest(
    `user-${userId}`,
    (signal) => api.get(`/api/users/${userId}`, { signal })
  )
}
```

### 4. Rate Limiter for Batch Operations

```javascript
import { RateLimiter } from '@/utils/requestDebounce'

const limiter = new RateLimiter(100) // Min 100ms between calls

// Process items with rate limiting
for (const item of items) {
  await limiter.execute(() => api.post('/api/process', item))
}
```

## When to Use

- **Debounce**: Search inputs, autocomplete, form validation
- **Throttle**: Scroll handlers, resize handlers, button clicks
- **Cancel Duplicates**: Navigation, tab switching, rapid user actions
- **Rate Limiter**: Batch operations, bulk uploads, polling

## Benefits

- Reduces server load
- Prevents rate limit errors (429)
- Improves user experience
- Protects against accidental API spam

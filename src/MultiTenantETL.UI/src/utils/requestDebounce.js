/**
 * Request debouncing utility to prevent API spam
 * Useful for search inputs, autocomplete, etc.
 */

const pendingRequests = new Map()

// Export for testing
export { pendingRequests }

/**
 * Debounce function calls with automatic cleanup
 * @param {Function} func - Function to debounce
 * @param {number} delay - Delay in milliseconds
 * @returns {Function} Debounced function
 */
export function debounce(func, delay = 300) {
  let timeoutId
  
  return function debounced(...args) {
    clearTimeout(timeoutId)
    
    return new Promise((resolve, reject) => {
      timeoutId = setTimeout(async () => {
        try {
          const result = await func.apply(this, args)
          resolve(result)
        } catch (error) {
          reject(error)
        }
      }, delay)
    })
  }
}

/**
 * Throttle function calls to limit execution rate
 * @param {Function} func - Function to throttle
 * @param {number} limit - Minimum time between calls in milliseconds
 * @returns {Function} Throttled function
 */
export function throttle(func, limit = 1000) {
  let inThrottle
  let lastResult
  
  return function throttled(...args) {
    if (!inThrottle) {
      inThrottle = true
      
      setTimeout(() => {
        inThrottle = false
      }, limit)
      
      lastResult = func.apply(this, args)
      return lastResult
    }
    
    return lastResult
  }
}

/**
 * Cancel duplicate in-flight requests
 * @param {string} key - Unique key for the request
 * @param {Function} requestFn - Function that returns a promise
 * @returns {Promise} Request promise
 */
export async function cancelDuplicateRequest(key, requestFn) {
  // Cancel existing request with same key
  if (pendingRequests.has(key)) {
    const existingController = pendingRequests.get(key)
    existingController.abort()
  }
  
  // Create new abort controller
  const controller = new AbortController()
  pendingRequests.set(key, controller)
  
  try {
    const result = await requestFn(controller.signal)
    pendingRequests.delete(key)
    return result
  } catch (error) {
    pendingRequests.delete(key)
    
    // Don't throw if request was cancelled
    if (error.name === 'AbortError' || error.code === 'ERR_CANCELED') {
      return null
    }
    
    throw error
  }
}

/**
 * Rate limiter for API calls
 * Ensures minimum time between consecutive calls
 */
export class RateLimiter {
  constructor(minInterval = 100) {
    this.minInterval = minInterval
    this.lastCallTime = 0
    this.queue = []
    this.processing = false
  }
  
  async execute(fn) {
    return new Promise((resolve, reject) => {
      this.queue.push({ fn, resolve, reject })
      this.processQueue()
    })
  }
  
  async processQueue() {
    if (this.processing || this.queue.length === 0) {
      return
    }
    
    this.processing = true
    
    while (this.queue.length > 0) {
      const now = Date.now()
      const timeSinceLastCall = now - this.lastCallTime
      
      if (timeSinceLastCall < this.minInterval) {
        await new Promise(resolve => 
          setTimeout(resolve, this.minInterval - timeSinceLastCall)
        )
      }
      
      const { fn, resolve, reject } = this.queue.shift()
      this.lastCallTime = Date.now()
      
      try {
        const result = await fn()
        resolve(result)
      } catch (error) {
        reject(error)
      }
    }
    
    this.processing = false
  }
}

<template>
  <div class="ai-chatbot" :class="{ 'chatbot-hidden': !shouldShowChatbot }">
    <!-- Floating Chat Button with Hint -->
    <div v-if="!isOpen" class="chat-fab-container">
      <v-btn
        icon
        size="large"
        color="primary"
        class="chat-fab"
        elevation="8"
        @click="toggleChat"
      >
        <v-icon>mdi-robot</v-icon>
      </v-btn>
      
      <!-- Help Hint Tooltip -->
      <div
        v-if="showHint"
        v-motion
        :initial="{ opacity: 0, x: 20, y: 0, scale: 1 }"
        :enter="{ 
          opacity: 1, 
          x: 0, 
          scale: 1,
          transition: { 
            type: 'spring',
            stiffness: 200,
            damping: 20
          }
        }"
        :leave="{ 
          opacity: 0, 
          x: 30, 
          transition: { 
            duration: 400,
            ease: [0.4, 0, 0.2, 1]
          }
        }"
        :hovered="{
          scale: 1.05,
          transition: {
            type: 'spring',
            stiffness: 400,
            damping: 10
          }
        }"
        class="help-hint"
        @click="toggleChat"
      >
        <div class="hint-content">
          <v-icon size="small" class="mr-1">mdi-help-circle</v-icon>
          <span 
            v-motion
            :initial="{ opacity: 0, x: -10 }"
            :enter="{ 
              opacity: 1, 
              x: 0, 
              transition: { delay: 300, duration: 500 } 
            }"
          >{{ $t('chatbot.needHelp') }}</span>
        </div>
        <v-btn
          icon
          size="x-small"
          variant="text"
          class="hint-close"
          @click.stop="dismissHint"
        >
          <v-icon size="small">mdi-close</v-icon>
        </v-btn>
      </div>
    </div>

    <!-- Chat Window -->
    <v-card
      v-if="isOpen"
      v-motion
      :initial="{ 
        opacity: 0, 
        y: 20, 
        scale: 0.95,
        transformOrigin: 'bottom right'
      }"
      :enter="{ 
        opacity: 1, 
        y: 0, 
        scale: 1,
        transition: {
          type: 'spring',
          stiffness: 250,
          damping: 25
        }
      }"
      :leave="{ 
        opacity: 0, 
        y: 20, 
        scale: 0.95,
        transition: {
          duration: 300,
          ease: 'easeIn'
        }
      }"
      class="chat-window"
      :class="{ 'chat-window-expanded': isExpanded }"
      elevation="12"
    >
      <v-card-title class="d-flex align-center pa-3 primary chat-header">
        <v-icon class="mr-2 text-white">mdi-robot</v-icon>
        <span class="text-white">Maeve</span>
        <v-spacer />
        <v-tooltip :text="$t('chatbot.clearChat')" location="bottom">
          <template #activator="{ props }">
            <v-btn
              icon
              size="small"
              variant="text"
              :disabled="messages.length === 0"
              v-bind="props"
              @click="clearChat"
            >
              <v-icon>mdi-broom</v-icon>
            </v-btn>
          </template>
        </v-tooltip>
        <v-tooltip :text="isExpanded ? $t('common.collapse') : $t('common.expand')" location="bottom">
          <template #activator="{ props }">
            <v-btn
              icon
              size="small"
              variant="text"
              v-bind="props"
              @click="isExpanded = !isExpanded"
            >
              <v-icon>{{ isExpanded ? 'mdi-arrow-collapse' : 'mdi-arrow-expand' }}</v-icon>
            </v-btn>
          </template>
        </v-tooltip>
        <v-tooltip :text="$t('common.close')" location="bottom">
          <template #activator="{ props }">
            <v-btn
              icon
              size="small"
              variant="text"
              class="close-btn"
              v-bind="props"
              @click="toggleChat"
            >
              <v-icon>mdi-close</v-icon>
            </v-btn>
          </template>
        </v-tooltip>
      </v-card-title>

      <v-divider />

      <!-- Messages Area -->
      <v-card-text
        ref="messagesContainer"
        class="messages-container pa-3"
      >
        <div v-if="messages.length === 0" class="text-center text-medium-emphasis py-8">
          <v-icon size="48" class="mb-2">mdi-chat-question</v-icon>
          <p>{{ $t('chatbot.greeting') }}</p>
        </div>

        <div
          v-for="(msg, index) in messages"
          :key="index"
          class="message mb-3"
          :class="msg.role"
        >
          <div class="message-content">
            <div class="message-header mb-1">
              <v-icon size="small" class="mr-1">
                {{ msg.role === 'user' ? 'mdi-account' : 'mdi-robot' }}
              </v-icon>
              <span class="text-caption">{{ msg.role === 'user' ? $t('chatbot.you') : 'Maeve' }}</span>
            </div>
            <div 
              class="message-text" 
              v-html="msg.role === 'assistant' ? renderMarkdown(msg.content) : msg.content"
            ></div>
          </div>
        </div>

        <div v-if="isLoading" class="message assistant mb-3">
          <div class="message-content">
            <div class="message-header mb-1">
              <v-icon size="small" class="mr-1">mdi-robot</v-icon>
              <span class="text-caption">Maeve</span>
            </div>
            <div class="message-text">
              <v-progress-circular
                indeterminate
                size="20"
                width="2"
                class="mr-2"
              />
              {{ $t('chatbot.thinking') }}
            </div>
          </div>
        </div>
      </v-card-text>

      <v-divider />

      <!-- Quick Suggestions -->
      <div v-if="messages.length === 0" class="quick-suggestions pa-2">
        <v-chip
          v-for="suggestion in quickSuggestions"
          :key="suggestion"
          size="small"
          class="ma-1"
          @click="userInput = suggestion"
        >
          {{ suggestion }}
        </v-chip>
      </div>

      <v-divider v-if="messages.length === 0" />

      <!-- Input Area -->
      <v-card-actions class="pa-3 input-area">
        <div class="input-wrapper">
          <v-textarea
            v-model="userInput"
            :placeholder="$t('chatbot.placeholder')"
            variant="outlined"
            rows="2"
            auto-grow
            max-rows="5"
            hide-details
            class="chat-input"
            @keydown.enter.exact.prevent="sendMessage"
            @keydown.shift.enter.exact="userInput += '\n'"
          />
          <div class="input-controls">
            <span v-if="userInput.length > 0" class="text-caption char-counter" :class="{ 'text-error': userInput.length > 500 }">
              {{ $t('chatbot.charLimit', { count: userInput.length }) }}
            </span>
            <v-spacer />
            <v-btn
              icon
              size="small"
              color="primary"
              :disabled="!userInput.trim() || isLoading || userInput.length > 500"
              @click="sendMessage"
            >
              <v-icon>mdi-send</v-icon>
            </v-btn>
          </div>
        </div>
      </v-card-actions>
    </v-card>
  </div>
</template>

<script setup>
import { ref, watch, nextTick, onMounted, onUnmounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { getChatResponse } from '@/services/geminiService';
import { marked } from 'marked';

const route = useRoute();
const { t } = useI18n();

// Expose method to open chatbot globally
const openChatbot = () => {
  isOpen.value = true;
  dismissHint();
};

// Listen for global chatbot open event
if (typeof window !== 'undefined') {
  window.openAiChatbot = openChatbot;
}

// Check if chatbot should be visible on current route
const shouldShowChatbot = computed(() => {
  const currentPath = route.path;
  
  // Hide on login
  if (currentPath === '/login') return false;
  
  // Hide on any connector form (new or edit with ID)
  if (currentPath.startsWith('/connectors/') && (currentPath.includes('/new') || currentPath.includes('/edit'))) {
    return false;
  }
  
  // Hide on any pipeline form (new or edit with ID)
  if (currentPath.startsWith('/pipelines/') && (currentPath.includes('/new') || currentPath.includes('/edit'))) {
    return false;
  }
  
  return true;
});
const isOpen = ref(false);
const userInput = ref('');
const messages = ref([]);
const isLoading = ref(false);
const messagesContainer = ref(null);
const showHint = ref(false);
const hintTimer = ref(null);
// Check sessionStorage to see if hint was already dismissed in this session
const hintDismissed = ref(sessionStorage.getItem('chatbot-hint-dismissed') === 'true');
const isExpanded = ref(false);

// Quick suggestions based on current page
const quickSuggestions = computed(() => [
  t('chatbot.explainPage'),
  t('chatbot.howToCreatePipeline'),
  t('chatbot.showExample'),
  t('chatbot.whatCanIDo')
]);

// Configure marked for basic rendering
marked.setOptions({
  breaks: true,
  gfm: true
});

// Render markdown to HTML with copy button for code blocks
const renderMarkdown = (text) => {
  const html = marked.parse(text);
  // Add copy button to code blocks
  return html.replace(/<pre><code/g, '<div class="code-block-wrapper"><button class="copy-code-btn" onclick="copyCode(this)"><span class="mdi mdi-content-copy"></span></button><pre><code').replace(/<\/code><\/pre>/g, '</code></pre></div>');
};

// Copy code function (attached to window for onclick access)
if (typeof window !== 'undefined') {
  window.copyCode = function(button) {
    const codeBlock = button.parentElement.querySelector('code');
    const code = codeBlock.textContent;
    navigator.clipboard.writeText(code).then(() => {
      const icon = button.querySelector('.mdi');
      icon.classList.remove('mdi-content-copy');
      icon.classList.add('mdi-check');
      button.classList.add('copied');
      setTimeout(() => {
        icon.classList.remove('mdi-check');
        icon.classList.add('mdi-content-copy');
        button.classList.remove('copied');
      }, 2000);
    });
  };
}

// Get current page context from route
const getCurrentPage = () => {
  const path = route.path.split('/')[1] || 'dashboard';
  return path;
};

const toggleChat = () => {
  isOpen.value = !isOpen.value;
  if (isOpen.value) {
    dismissHint();
  }
};

const clearChat = () => {
  messages.value = [];
};

const scrollToBottom = (smooth = false) => {
  nextTick(() => {
    if (messagesContainer.value) {
      const element = messagesContainer.value.$el || messagesContainer.value;
      if (smooth) {
        element.scrollTo({
          top: element.scrollHeight,
          behavior: 'smooth'
        });
      } else {
        element.scrollTop = element.scrollHeight;
      }
    }
  });
};

const sendMessage = async () => {
  if (!userInput.value.trim() || isLoading.value) return;
  
  // Check character limit
  if (userInput.value.length > 500) {
    return;
  }

  const message = userInput.value.trim();
  userInput.value = '';

  // Add user message
  messages.value.push({
    role: 'user',
    content: message
  });

  scrollToBottom();
  isLoading.value = true;

  try {
    const currentPage = getCurrentPage();
    // Get user's current language from localStorage
    const userLanguage = localStorage.getItem('user-locale') || 'en';
    const response = await getChatResponse(message, currentPage, messages.value, userLanguage);

    // Add assistant response
    messages.value.push({
      role: 'assistant',
      content: response
    });

    // Scroll with smooth animation after response
    setTimeout(() => scrollToBottom(true), 100);
  } catch (error) {
    console.error('Chat error:', error);
    
    // Provide specific error message
    let errorMessage = 'Sorry, I encountered an error. Please try again.';
    
    if (error && error.message) {
      const msg = error.message.toLowerCase();
      if (msg.includes('overloaded') || msg.includes('503')) {
        errorMessage = '⚠️ The AI service is currently overloaded. I tried 3 times but couldn\'t get through. Please wait a moment and try again.';
      } else if (msg.includes('retrying')) {
        errorMessage = '⚠️ The AI service is busy. Please try again in a moment.';
      } else if (msg.includes('quota')) {
        errorMessage = '❌ API quota exceeded. Please contact support.';
      } else if (msg.includes('api key') || msg.includes('403')) {
        errorMessage = '❌ API configuration error. Please contact support.';
      } else if (msg.includes('429')) {
        errorMessage = '⚠️ Rate limit exceeded. Please wait a moment before trying again.';
      }
    }
    
    messages.value.push({
      role: 'assistant',
      content: errorMessage
    });
    
    setTimeout(() => scrollToBottom(true), 100);
  } finally {
    isLoading.value = false;
  }
};

// Inactivity hint logic
const startHintTimer = () => {
  clearHintTimer();
  if (!hintDismissed.value && !isOpen.value) {
    hintTimer.value = setTimeout(() => {
      showHint.value = true;
    }, 5000); // Show after 5 seconds of inactivity
  }
};

const clearHintTimer = () => {
  if (hintTimer.value) {
    clearTimeout(hintTimer.value);
    hintTimer.value = null;
  }
};

const dismissHint = () => {
  showHint.value = false;
  hintDismissed.value = true;
  // Store dismissal in sessionStorage so it persists across page navigations
  sessionStorage.setItem('chatbot-hint-dismissed', 'true');
  clearHintTimer();
};

const resetHintTimer = () => {
  // Only reset if hint is not currently showing
  if (!showHint.value) {
    clearHintTimer();
    startHintTimer();
  }
};

// Track user activity (but not when hint is visible)
const handleUserActivity = () => {
  if (!isOpen.value && !showHint.value) {
    resetHintTimer();
  }
};

// Clear messages when route changes
watch(() => route.path, () => {
  messages.value = [];
  // Don't reset hint dismissal - it should persist for the entire session
  
  // Close chatbot when navigating to hidden routes
  if (!shouldShowChatbot.value) {
    isOpen.value = false;
  }
  
  resetHintTimer();
});

// Watch chat open state
watch(isOpen, (newValue) => {
  if (newValue) {
    clearHintTimer();
    showHint.value = false;
  } else {
    startHintTimer();
  }
});

onMounted(() => {
  // Start hint timer on mount
  startHintTimer();
  
  // Listen for user activity
  window.addEventListener('mousemove', handleUserActivity);
  window.addEventListener('keydown', handleUserActivity);
  window.addEventListener('click', handleUserActivity);
  window.addEventListener('scroll', handleUserActivity);
});

onUnmounted(() => {
  clearHintTimer();
  
  // Clean up event listeners
  window.removeEventListener('mousemove', handleUserActivity);
  window.removeEventListener('keydown', handleUserActivity);
  window.removeEventListener('click', handleUserActivity);
  window.removeEventListener('scroll', handleUserActivity);
});
</script>

<style scoped>
.ai-chatbot {
  position: fixed;
  bottom: 80px;
  right: 24px;
  z-index: 999;
}

.ai-chatbot.chatbot-hidden .chat-fab-container {
  display: none;
}

.chat-fab {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.chat-window {
  width: 380px;
  height: 500px;
  display: flex;
  flex-direction: column;
  border-radius: 12px;
  overflow: hidden;
  /* Specifically transition width/height for expansion, but skip transform/opacity which v-motion handles */
  transition: width 0.4s cubic-bezier(0.4, 0, 0.2, 1), height 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  will-change: width, height, transform, opacity;
}

.chat-window-expanded {
  width: 600px;
  height: 700px;
}

.chat-header {
  flex-shrink: 0;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  background: rgba(var(--v-theme-surface), 0.5);
  -webkit-overflow-scrolling: touch;
}

.message {
  display: flex;
}

.message.user {
  justify-content: flex-end;
}

.message.assistant {
  justify-content: flex-start;
}

.message-content {
  max-width: 80%;
  padding: 8px 12px;
  border-radius: 12px;
  overflow: hidden;
}

.message.user .message-content {
  background: rgb(var(--v-theme-primary));
  color: white;
}

.message.assistant .message-content {
  background: rgba(var(--v-theme-surface-variant), 1);
}

.message-header {
  display: flex;
  align-items: center;
  opacity: 0.7;
}

.message-text {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
}

/* Markdown styling in messages */
.message-text :deep(p) {
  margin: 0 0 8px 0;
}

.message-text :deep(p:last-child) {
  margin-bottom: 0;
}

.message-text :deep(ul),
.message-text :deep(ol) {
  margin: 8px 0;
  padding-left: 20px;
}

.message-text :deep(li) {
  margin: 4px 0;
}

.message-text :deep(strong) {
  font-weight: 600;
}

.message-text :deep(em) {
  font-style: italic;
}

.message-text :deep(code) {
  background: rgba(0, 0, 0, 0.15);
  padding: 2px 5px;
  border-radius: 3px;
  font-family: 'Courier New', Consolas, monospace;
  font-size: 0.85em;
}

/* Code block wrapper with copy button */
.message-text :deep(.code-block-wrapper) {
  position: relative;
  margin: 8px 0;
}

.message-text :deep(.copy-code-btn) {
  position: absolute;
  top: 8px;
  right: 8px;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  padding: 4px 8px;
  cursor: pointer;
  color: #ccc;
  font-size: 14px;
  transition: all 0.2s ease;
  z-index: 1;
}

.message-text :deep(.copy-code-btn:hover) {
  background: rgba(255, 255, 255, 0.2);
  color: white;
}

.message-text :deep(.copy-code-btn.copied) {
  background: rgba(76, 175, 80, 0.3);
  border-color: rgba(76, 175, 80, 0.5);
  color: #4caf50;
}

.message-text :deep(pre) {
  background: #2d2d2d;
  padding: 12px;
  padding-top: 36px;
  border-radius: 6px;
  overflow-x: auto;
  margin: 0;
}

.message-text :deep(pre code) {
  background: none;
  padding: 0;
  font-size: 0.8em;
  line-height: 1.5;
  display: block;
  white-space: pre;
  color: #ccc;
}

/* Light mode code blocks */
.v-theme--light .message-text :deep(pre) {
  background: #f8f8f8 !important;
  border: 1px solid #d0d0d0;
}

.v-theme--light .message-text :deep(pre code) {
  color: #2c3e50 !important;
  background: transparent !important;
}

.v-theme--light .message-text :deep(.copy-code-btn) {
  background: rgba(0, 0, 0, 0.05);
  border-color: rgba(0, 0, 0, 0.1);
  color: #666;
}

.v-theme--light .message-text :deep(.copy-code-btn:hover) {
  background: rgba(0, 0, 0, 0.1);
  color: #333;
}



.message-text :deep(a) {
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
}

.v-theme--light .message-text :deep(code) {
  background: rgba(0, 0, 0, 0.1);
}

.v-theme--light .message-text :deep(pre) {
  background: rgba(0, 0, 0, 0.05);
  border: 1px solid rgba(0, 0, 0, 0.1);
}

/* Light mode only improvements - don't touch dark mode */
.v-theme--light .chat-window {
  border: 1px solid #E0E0E0;
}

.v-theme--light .messages-container {
  background: #F5F5F5;
}

.v-theme--light .message.assistant .message-content {
  background: #FFFFFF;
  border: 1px solid #E0E0E0;
  color: #212121;
}

.v-theme--light .message.assistant .message-header {
  color: #616161;
}

.v-theme--light .message.assistant .message-text {
  color: #212121;
}

/* Light mode header - make it visible */
.v-theme--light .v-card-title.primary {
  background-color: #1976D2 !important;
}

.v-theme--light .v-card-title.primary * {
  color: white !important;
}

/* Light mode placeholder text */
.v-theme--light .v-text-field input::placeholder {
  color: #616161 !important;
  opacity: 1 !important;
}

/* Light mode input border */
.v-theme--light .v-card-actions {
  border-top: 1px solid #E0E0E0;
}

/* Quick suggestions */
.quick-suggestions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  background: rgba(var(--v-theme-surface), 0.3);
}

.quick-suggestions .v-chip {
  cursor: pointer;
  transition: all 0.2s ease;
}

.quick-suggestions .v-chip:hover {
  transform: translateY(-2px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

/* Input area improvements */
.input-area {
  background: rgba(var(--v-theme-surface), 0.5);
  display: block;
}

.input-wrapper {
  width: 100%;
}

.chat-input {
  font-size: 14px;
  width: 100%;
  margin-bottom: 8px;
}

.chat-input :deep(.v-field) {
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
}

.chat-input :deep(.v-field--focused) {
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.3);
}

.input-controls {
  display: flex;
  align-items: center;
  gap: 8px;
}

.char-counter {
  font-size: 11px;
  opacity: 0.7;
  white-space: nowrap;
}

.v-theme--light .quick-suggestions {
  background: #f5f5f5;
}

.v-theme--light .input-area {
  background: #fafafa;
}

/* Chat FAB Container */
.chat-fab-container {
  position: relative;
}

/* Help Hint Tooltip */
.help-hint {
  position: absolute;
  bottom: 8px;
  right: 76px;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-primary), 0.5);
  color: rgb(var(--v-theme-on-surface));
  padding: 12px 16px;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  white-space: nowrap;
  z-index: 998;
  /* Removed CSS hover transform to avoid conflict with v-motion :hovered */
  transition: box-shadow 0.2s ease;
  will-change: transform, opacity;
}

.help-hint:hover {
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.25);
}

.hint-content {
  display: flex;
  align-items: center;
  font-size: 13px;
  font-weight: 500;
  color: rgb(var(--v-theme-primary));
}

.hint-content .v-icon {
  color: rgb(var(--v-theme-primary));
}

.hint-close {
  opacity: 0.6;
  margin-left: 4px;
  color: rgb(var(--v-theme-on-surface));
}

.hint-close:hover {
  opacity: 1;
  background: rgba(var(--v-theme-on-surface), 0.1);
}

/* Light mode hint */
.v-theme--light .help-hint {
  background: #FFFFFF;
  border: 1px solid #2196F3;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.v-theme--light .help-hint:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

/* Dark mode hint */
.v-theme--dark .help-hint {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-primary), 0.6);
}

/* Hide chatbot completely on mobile and tablet */
@media (max-width: 960px) {
  .ai-chatbot {
    display: none !important;
  }
}
</style>



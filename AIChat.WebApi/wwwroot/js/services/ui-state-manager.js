(() => {
  "use strict";

  class UIStateManager {
    constructor(elements) {
      this.elements = elements;
      this.isStreaming = false;
      this.streamStartTime = null;
      this.accumulatedTokens = { input: 0, output: 0, total: 0 };
    }

    // Connection state management
    showReconnecting() {
      const banner = this.elements.connectionBanner;
      const reconnecting = this.elements.reconnectingBanner;
      const disconnected = this.elements.disconnectedBanner;
      
      if (banner && reconnecting && disconnected) {
        banner.classList.remove("hidden");
        reconnecting.classList.remove("hidden");
        disconnected.classList.add("hidden");
      }
    }

    hideReconnecting() {
      const reconnecting = this.elements.reconnectingBanner;
      const banner = this.elements.connectionBanner;
      const disconnected = this.elements.disconnectedBanner;
      
      if (reconnecting) reconnecting.classList.add("hidden");
      if (banner && disconnected && !disconnected.classList.contains("hidden")) return;
      if (banner) banner.classList.add("hidden");
    }

    showDisconnected() {
      const banner = this.elements.connectionBanner;
      const disconnected = this.elements.disconnectedBanner;
      const reconnecting = this.elements.reconnectingBanner;
      
      if (banner && disconnected && reconnecting) {
        banner.classList.remove("hidden");
        disconnected.classList.remove("hidden");
        reconnecting.classList.add("hidden");
      }
    }

    hideAllConnectionBanners() {
      const banner = this.elements.connectionBanner;
      const disconnected = this.elements.disconnectedBanner;
      const reconnecting = this.elements.reconnectingBanner;
      
      if (banner) banner.classList.add("hidden");
      if (disconnected) disconnected.classList.add("hidden");
      if (reconnecting) reconnecting.classList.add("hidden");
    }

    // Streaming state management
    showStreamingIndicator() {
      if (this.elements.streamingIndicator) {
        this.elements.streamingIndicator.classList.remove("hidden");
      }
    }

    hideStreamingIndicator() {
      if (this.elements.streamingIndicator) {
        this.elements.streamingIndicator.classList.add("hidden");
      }
    }

    setStreamingState(isStreaming) {
      this.isStreaming = isStreaming;
      if (isStreaming) {
        this.streamStartTime = Date.now();
      } else {
        this.streamStartTime = null;
      }
      this.updateSendEnabled();
    }

    // Stats management
    updateStats(usage, responseTimeMs) {
      const u = usage || {};
      const input = u.inputTokens ?? u.InputTokens ?? 0;
      const output = u.outputTokens ?? u.OutputTokens ?? 0;
      const total = u.totalTokens ?? u.TotalTokens ?? (input + output);
      
      this.accumulatedTokens = { input, output, total };
      
      if (this.elements.statsDisplay) {
        this.elements.inputTokens.textContent = String(input);
        this.elements.outputTokens.textContent = String(output);
        this.elements.totalTokens.textContent = String(total);
        this.elements.responseTime.textContent = DOMUtils.formatDuration(responseTimeMs);
        this.elements.statsDisplay.classList.remove("hidden");
      }
    }

    resetStats() {
      this.accumulatedTokens = { input: 0, output: 0, total: 0 };
      
      if (this.elements.statsDisplay) {
        this.elements.inputTokens.textContent = "0";
        this.elements.outputTokens.textContent = "0";
        this.elements.totalTokens.textContent = "0";
        this.elements.responseTime.textContent = "-";
        this.elements.statsDisplay.classList.add("hidden");
      }
    }

    // Input state management
    updateSendEnabled() {
      const hasText = !!(this.elements.messageInput.value || "").trim();
      const canSend = hasText && !this.isStreaming;
      
      this.elements.sendButton.disabled = !canSend;
      this.elements.messageInput.disabled = !!this.isStreaming;
    }

    // Textarea management
    autoExpandTextarea() {
      const ta = this.elements.messageInput;
      if (!ta) return;
      
      ta.style.height = "auto";
      const max = 200;
      ta.style.height = Math.min(ta.scrollHeight, max) + "px";
    }

    updateCharHint() {
      const hint = DOMUtils.byId("charHint");
      if (!hint) return;
      
      const text = this.elements.messageInput.value || "";
      hint.textContent = `${text.length} / 4000`;
    }

    // Welcome screen management
    showWelcome(show, forceShow = false) {
      const welcome = this.elements.welcomeScreen;
      if (!welcome) return;
      
      if (show && !forceShow) {
        // Check if there are messages in the container
        const hasMessages = this.elements.messagesContainer &&
                           this.elements.messagesContainer.children.length > 0;
        if (hasMessages) {
          welcome.classList.add("hidden");
          return;
        }
      }
      
      if (show) {
        welcome.classList.remove("hidden");
      } else {
        welcome.classList.add("hidden");
      }
    }

    // Loading state management
    showLoadingState() {
      this.hideWelcome(); // Hide welcome screen during loading
      this.clearMessages();
      
      if (this.elements.messagesContainer) {
        const loadingElement = document.createElement("div");
        loadingElement.id = "loadingIndicator";
        loadingElement.className = "flex items-center justify-center py-8";
        loadingElement.innerHTML = `
          <div class="flex items-center space-x-3 text-slate-500">
            <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-indigo-600"></div>
            <span class="text-sm font-medium">Loading conversation history...</span>
          </div>
        `;
        this.elements.messagesContainer.appendChild(loadingElement);
      }
    }

    hideLoadingState() {
      if (this.elements.messagesContainer) {
        const loadingIndicator = this.elements.messagesContainer.querySelector("#loadingIndicator");
        if (loadingIndicator) {
          loadingIndicator.remove();
        }
      }
    }

    // Error state management
    showError(message) {
      this.hideLoadingState();
      this.hideWelcome();
      
      if (this.elements.messagesContainer) {
        const errorElement = document.createElement("div");
        errorElement.id = "errorIndicator";
        errorElement.className = "flex items-center justify-center py-8";
        errorElement.innerHTML = `
          <div class="flex flex-col items-center space-y-3 text-center max-w-md">
            <div class="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center">
              <svg class="w-6 h-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
            </div>
            <div>
              <p class="text-slate-700 font-medium">Error</p>
              <p class="text-slate-500 text-sm mt-1">${DOMUtils.escapeHtml(message || "An error occurred")}</p>
            </div>
            <button onclick="this.parentElement.parentElement.remove()" class="text-indigo-600 hover:text-indigo-700 text-sm font-medium">
              Dismiss
            </button>
          </div>
        `;
        this.elements.messagesContainer.appendChild(errorElement);
      }
    }

    hideError() {
      if (this.elements.messagesContainer) {
        const errorIndicator = this.elements.messagesContainer.querySelector("#errorIndicator");
        if (errorIndicator) {
          errorIndicator.remove();
        }
      }
    }

    hideWelcome() {
      const welcome = this.elements.welcomeScreen;
      if (welcome) {
        welcome.classList.add("hidden");
      }
    }

    // Message container management
    clearMessages() {
      if (this.elements.messagesContainer) {
        this.elements.messagesContainer.innerHTML = "";
      }
    }
  }

  window.UIStateManager = UIStateManager;
})();
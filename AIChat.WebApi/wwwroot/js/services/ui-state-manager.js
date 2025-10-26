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
    showWelcome(show) {
      const welcome = this.elements.welcomeScreen;
      if (!welcome) return;
      
      if (show) {
        welcome.classList.remove("hidden");
      } else {
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
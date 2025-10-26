/* global signalR */
(() => {
  "use strict";

  class ChatApp {
    constructor() {
      this.signalRManager = new SignalRManager();
      this.providerManager = new ProviderManager();
      this.chatHistoryManager = new ChatHistoryManager(this.signalRManager);
      this.threadManager = new ThreadManager();
      this.messageRenderer = new MessageRenderer();
      this.uiStateManager = null;
      this.chatHistoryUI = new ChatHistoryUI(this.chatHistoryManager);
      this.autoTitleService = new AutoTitleService();
      
      this.elements = this.initializeElements();
      this.setupEventHandlers();
    }

    initializeElements() {
      const byId = (id) => document.getElementById(id);
      return {
        providerSelect: byId("providerSelect"),
        statsDisplay: byId("statsDisplay"),
        inputTokens: byId("inputTokens"),
        outputTokens: byId("outputTokens"),
        totalTokens: byId("totalTokens"),
        responseTime: byId("responseTime"),
        messageInput: byId("messageInput"),
        sendButton: byId("sendButton"),
        messagesContainer: byId("messagesContainer"),
        streamingIndicator: byId("streamingIndicator"),
        welcomeScreen: byId("welcomeScreen"),
        reconnectingBanner: byId("reconnectingBanner"),
        disconnectedBanner: byId("disconnectedBanner"),
        connectionBanner: byId("connectionBanner"),
        retryConnection: byId("retryConnection"),
        newChatBtn: byId("newChatBtn"),
        chatHistoryList: byId("chatHistoryList")
      };
    }

    setupEventHandlers() {
      this.uiStateManager = new UIStateManager(this.elements);

      // SignalR event handlers
      this.signalRManager.on('reconnecting', () => this.uiStateManager.showReconnecting());
      this.signalRManager.on('reconnected', () => {
        this.uiStateManager.hideReconnecting();
        this.chatHistoryManager.loadChatHistory();
      });
      this.signalRManager.on('disconnected', () => this.uiStateManager.showDisconnected());
      this.signalRManager.on('connected', () => this.uiStateManager.hideAllConnectionBanners());
      this.signalRManager.on('chatHistoryUpdated', (item) => this.handleChatHistoryUpdated(item));
      this.signalRManager.on('chatHistoryItemDeleted', (threadId) => this.handleChatHistoryItemDeleted(threadId));
      this.signalRManager.on('activeThreadChanged', (threadId) => this.handleActiveThreadChanged(threadId));

      // UI event handlers
      this.elements.sendButton.addEventListener("click", () => this.sendMessage());
      this.elements.messageInput.addEventListener("input", () => {
        this.uiStateManager.autoExpandTextarea();
        this.uiStateManager.updateCharHint();
        this.uiStateManager.updateSendEnabled();
      });
      this.elements.messageInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
          e.preventDefault();
          this.sendMessage();
        }
      });
      this.elements.providerSelect.addEventListener("change", () => {
        this.providerManager.setCurrentProvider(this.elements.providerSelect.value);
        this.uiStateManager.updateSendEnabled();
      });
      
      if (this.elements.retryConnection) {
        this.elements.retryConnection.addEventListener("click", async () => {
          await this.signalRManager.retryConnection();
        });
      }
      
      this.elements.newChatBtn.addEventListener("click", () => this.createNewThread());
    }

    async init() {
      try {
        await this.threadManager.restoreThreadFromStorage();
        await this.loadProviders();
        await this.loadChatHistory();
        await this.setupSignalR();
        
        this.uiStateManager.updateCharHint();
        this.uiStateManager.updateSendEnabled();
        
        // Show welcome screen if we have a thread
        if (this.threadManager.getCurrentThreadId()) {
          this.uiStateManager.showWelcome(true);
        }
      } catch (err) {
        console.error("Error initializing app:", err);
      }
    }

    async loadProviders() {
      const result = await this.providerManager.loadProviders();
      this.providerManager.populateSelectElement(this.elements.providerSelect);
      
      if (!result.success) {
        console.warn("No providers available");
      }
    }

    async loadChatHistory() {
      const result = await this.chatHistoryManager.loadChatHistory();
      if (result.success) {
        this.chatHistoryUI.displayChatHistory(
          this.elements.chatHistoryList,
          this.threadManager.getCurrentThreadId(),
          (threadId) => this.selectThread(threadId),
          (threadId) => this.deleteChatHistoryItem(threadId)
        );
      }
    }

    async setupSignalR() {
      await this.signalRManager.setupConnection();
    }

    async sendMessage() {
      if (this.uiStateManager.isStreaming) return;
      
      const message = (this.elements.messageInput.value || "").trim();
      if (!message) return;
      
      if (!this.providerManager.getCurrentProvider()) {
        alert("Please select a provider");
        return;
      }
      
      if (!this.threadManager.getCurrentThreadId()) {
        const result = await this.threadManager.createNewThread();
        if (!result.success) {
          alert("Failed to create new thread");
          return;
        }
        
        // Create initial chat history item
        await this.chatHistoryManager.createChatHistoryItem(
          this.threadManager.getCurrentThreadId(),
          "New Conversation"
        );
      }
      
      // Generate auto-title from first user message
      if (this.autoTitleService.shouldGenerateTitle(
        this.chatHistoryManager.getChatHistory(),
        this.threadManager.getCurrentThreadId()
      )) {
        this.generateAutoTitle(message);
      }
      
      // Send the message
      await this.processMessage(message);
    }

    async generateAutoTitle(message) {
      try {
        const title = await this.autoTitleService.generateAutoTitle(
          message,
          this.threadManager.getCurrentThreadId()
        );
        
        if (title) {
          this.autoTitleService.updateHistoryItemTitle(
            this.chatHistoryManager.chatHistory,
            this.threadManager.getCurrentThreadId(),
            title,
            message
          );
          this.chatHistoryUI.updateHistoryDisplay(
            this.elements.chatHistoryList,
            this.threadManager.getCurrentThreadId(),
            (threadId) => this.selectThread(threadId),
            (threadId) => this.deleteChatHistoryItem(threadId)
          );
        }
      } catch (err) {
        console.warn("Failed to generate auto-title:", err);
      }
    }

    async processMessage(message) {
      // 1. Add user message
      this.messageRenderer.addMessage(
        this.elements.messagesContainer,
        "user",
        message
      );
      
      // 2. Assistant placeholder
      const assistantMsgId = this.messageRenderer.addMessage(
        this.elements.messagesContainer,
        "assistant",
        ""
      );
      
      this.uiStateManager.showStreamingIndicator();
      this.uiStateManager.setStreamingState(true);
      
      let fullResponse = "";
      
      try {
        const stream = this.signalRManager.stream(
          "StreamChat",
          this.providerManager.getCurrentProvider(),
          message,
          this.threadManager.getCurrentThreadId()
        );
        
        stream.subscribe({
          next: (chunk) => {
            if (!chunk) return;
            if (chunk.error) {
              this.messageRenderer.updateMessage(
                this.elements.messagesContainer,
                assistantMsgId,
                `<span class="text-red-600">${DOMUtils.escapeHtml(chunk.error)}</span>`
              );
              return;
            }
            if (chunk.text) {
              fullResponse += chunk.text;
              this.messageRenderer.updateMessage(
                this.elements.messagesContainer,
                assistantMsgId,
                fullResponse,
                this.providerManager.getCurrentProvider()
              );
            }
            if (chunk.isFinal) {
              if (chunk.threadId && !this.threadManager.getCurrentThreadId()) {
                this.threadManager.currentThreadId = chunk.threadId;
                this.threadManager.saveThreadToStorage();
              }
              const ms = Date.now() - this.uiStateManager.streamStartTime;
              if (chunk.usage) {
                this.uiStateManager.updateStats(chunk.usage, ms);
              }
              this.uiStateManager.hideStreamingIndicator();
            }
          },
          error: (err) => {
            this.handleStreamError(err, assistantMsgId);
          },
          complete: () => {
            this.uiStateManager.setStreamingState(false);
          }
        });
      } catch (err) {
        this.handleStreamError(err, assistantMsgId);
      } finally {
        this.elements.messageInput.value = "";
        this.uiStateManager.autoExpandTextarea();
        this.uiStateManager.updateCharHint();
      }
    }

    handleStreamError(err, assistantMsgId) {
      console.error("Stream error", err);
      const msg = (err && err.message) ? err.message : "An error occurred while streaming";
      this.messageRenderer.updateMessage(
        this.elements.messagesContainer,
        assistantMsgId,
        `<span class="text-red-600">${DOMUtils.escapeHtml(msg)}</span>`
      );
      this.uiStateManager.hideStreamingIndicator();
      this.uiStateManager.setStreamingState(false);
    }

    async createNewThread() {
      const result = await this.threadManager.createNewThread();
      if (result.success) {
        this.threadManager.saveThreadToStorage();
        this.uiStateManager.clearMessages();
        this.uiStateManager.showWelcome(true);
        this.uiStateManager.resetStats();
        
        // Create initial chat history item
        await this.chatHistoryManager.createChatHistoryItem(
          this.threadManager.getCurrentThreadId(),
          "New Conversation"
        );
        
        this.chatHistoryUI.updateHistoryDisplay(
          this.elements.chatHistoryList,
          this.threadManager.getCurrentThreadId(),
          (threadId) => this.selectThread(threadId),
          (threadId) => this.deleteChatHistoryItem(threadId)
        );
      }
    }

    async selectThread(threadId) {
      const result = await this.threadManager.selectThread(threadId, this.signalRManager);
      if (result.success) {
        this.threadManager.saveThreadToStorage();
        this.uiStateManager.clearMessages();
        this.uiStateManager.showWelcome(true);
        this.uiStateManager.resetStats();
        
        // Load thread messages if they exist
        try {
          const response = await fetch(`/api/threads/${encodeURIComponent(threadId)}`);
          if (response.ok) {
            const threadData = await response.json();
            if (threadData && threadData.messages) {
              // Render existing messages
              threadData.messages.forEach(msg => {
                this.messageRenderer.addMessage(
                  this.elements.messagesContainer,
                  msg.role,
                  msg.content
                );
              });
              this.uiStateManager.showWelcome(false);
            }
          }
        } catch (err) {
          console.warn("Failed to load thread messages:", err);
        }
        
        // Update UI to reflect active thread
        this.chatHistoryManager.updateActiveThread(threadId);
        this.chatHistoryUI.updateHistoryDisplay(
          this.elements.chatHistoryList,
          threadId,
          (threadId) => this.selectThread(threadId),
          (threadId) => this.deleteChatHistoryItem(threadId)
        );
      }
    }

    async deleteChatHistoryItem(threadId) {
      const success = await this.chatHistoryManager.deleteChatHistoryItem(threadId);
      if (success) {
        // If we deleted the current active thread, clear it
        if (this.threadManager.getCurrentThreadId() === threadId) {
          this.threadManager.clearCurrentThread();
          this.uiStateManager.clearMessages();
          this.uiStateManager.showWelcome(true);
        }
        
        this.chatHistoryUI.updateHistoryDisplay(
          this.elements.chatHistoryList,
          this.threadManager.getCurrentThreadId(),
          (threadId) => this.selectThread(threadId),
          (threadId) => this.deleteChatHistoryItem(threadId)
        );
      }
    }

    // Event handlers
    handleChatHistoryUpdated(item) {
      this.chatHistoryManager.updateChatHistoryItem(item);
      this.chatHistoryUI.updateHistoryDisplay(
        this.elements.chatHistoryList,
        this.threadManager.getCurrentThreadId()
      );
    }

    handleChatHistoryItemDeleted(threadId) {
      this.chatHistoryManager.removeChatHistoryItem(threadId);
      
      // If we deleted the current active thread, clear it
      if (this.threadManager.getCurrentThreadId() === threadId) {
        this.threadManager.clearCurrentThread();
        this.uiStateManager.clearMessages();
        this.uiStateManager.showWelcome(true);
      }
      
      this.chatHistoryUI.updateHistoryDisplay(
        this.elements.chatHistoryList,
        this.threadManager.getCurrentThreadId(),
        (threadId) => this.selectThread(threadId),
        (threadId) => this.deleteChatHistoryItem(threadId)
      );
    }

    handleActiveThreadChanged(threadId) {
      this.chatHistoryManager.updateActiveThread(threadId);
      this.chatHistoryUI.updateHistoryDisplay(
        this.elements.chatHistoryList,
        threadId,
        (threadId) => this.selectThread(threadId),
        (threadId) => this.deleteChatHistoryItem(threadId)
      );
    }
  }

  // Initialize the application
  window.addEventListener("DOMContentLoaded", () => {
    const app = new ChatApp();
    app.init();
  });
})();
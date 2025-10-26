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
        
        // Show welcome screen if we have a thread with no messages
        if (this.threadManager.getCurrentThreadId()) {
          this.uiStateManager.showWelcome(true, true); // Force show on init
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
      
      // Hide welcome screen immediately when user starts typing a message
      this.uiStateManager.showWelcome(false);
      
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
      
      // Don't generate title immediately - wait for AI response to have more context
      this.awaitingTitleGeneration = this.autoTitleService.shouldGenerateTitle(
        this.chatHistoryManager.getChatHistory(),
        this.threadManager.getCurrentThreadId()
      );
      
      // Send the message
      await this.processMessage(message);
    }

    async generateAutoTitle(userMessage, aiResponse = null) {
      try {
        const title = await this.autoTitleService.generateAutoTitle(
          userMessage,
          this.threadManager.getCurrentThreadId(),
          aiResponse
        );
        
        if (title) {
          const success = this.autoTitleService.updateHistoryItemTitle(
            this.chatHistoryManager.chatHistory,
            this.threadManager.getCurrentThreadId(),
            title,
            userMessage
          );
          
          if (success) {
            // Update the backend with the new title
            await this.updateChatHistoryTitle(this.threadManager.getCurrentThreadId(), title);
            
            // Update the UI
            this.chatHistoryUI.updateHistoryDisplay(
              this.elements.chatHistoryList,
              this.threadManager.getCurrentThreadId(),
              (threadId) => this.selectThread(threadId),
              (threadId) => this.deleteChatHistoryItem(threadId)
            );
          }
        }
      } catch (err) {
        console.warn("Failed to generate auto-title:", err);
      }
    }

    async updateChatHistoryTitle(threadId, title) {
      try {
        const response = await fetch(`/api/chathistory/${threadId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ title })
        });
        
        if (!response.ok) {
          console.warn("Failed to update title on backend");
        }
      } catch (err) {
        console.warn("Error updating title on backend:", err);
      }
    }

    async processMessage(message) {
      // 1. Add user message
      this.messageRenderer.addMessage(
        this.elements.messagesContainer,
        "user",
        message,
        null, // id
        null, // providerName
        new Date().toISOString() // timestamp
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
              
              // Generate auto-title now that we have both user message and AI response
              if (this.awaitingTitleGeneration) {
                this.generateAutoTitle(message, fullResponse);
                this.awaitingTitleGeneration = false;
              }
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
        this.uiStateManager.showWelcome(true, true); // Force show welcome for new thread
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
      // Clear any previous loading states in the history UI
      this.chatHistoryUI.clearAllLoadingStates(this.elements.chatHistoryList);
      
      // Show loading state
      this.uiStateManager.showLoadingState();
      
      const result = await this.threadManager.selectThread(threadId, this.signalRManager);
      if (result.success) {
        this.threadManager.saveThreadToStorage();
        this.uiStateManager.clearMessages();
        this.uiStateManager.resetStats();
        
        try {
          // Load complete thread messages with better error handling
          const messages = await this.loadThreadMessages(threadId);
          
          if (messages && messages.length > 0) {
            // Render all messages with proper formatting
            await this.renderMessagesSequentially(messages);
            this.uiStateManager.showWelcome(false);
            
            // Update message count in history
            await this.chatHistoryManager.updateMessageCount(threadId);
          } else {
            // No messages in thread, show welcome screen
            this.uiStateManager.showWelcome(true);
          }
        } catch (err) {
          console.error("Error loading thread messages:", err);
          this.uiStateManager.showError("Failed to load conversation history");
          this.uiStateManager.showWelcome(true);
        }
        
        // Update UI to reflect active thread
        this.chatHistoryManager.updateActiveThread(threadId);
        this.chatHistoryUI.updateHistoryDisplay(
          this.elements.chatHistoryList,
          threadId,
          (threadId) => this.selectThread(threadId),
          (threadId) => this.deleteChatHistoryItem(threadId)
        );
      } else {
        this.uiStateManager.hideLoadingState();
        this.uiStateManager.showError("Failed to select thread");
      }
    }

    async loadThreadMessages(threadId) {
      try {
        const response = await fetch(`/api/threads/${encodeURIComponent(threadId)}`);
        
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const threadData = await response.json();
        
        // Handle different response formats
        if (threadData && threadData.messages && Array.isArray(threadData.messages)) {
          return threadData.messages;
        } else if (threadData && Array.isArray(threadData)) {
          // Direct array of messages
          return threadData;
        } else {
          console.warn("Unexpected thread data format:", threadData);
          return [];
        }
      } catch (error) {
        console.error("Failed to fetch thread messages:", error);
        throw error;
      }
    }

    async renderMessagesSequentially(messages) {
      // Handle very large histories by showing a loading indicator
      const isLargeHistory = messages.length > 50;
      let loadingMsg = null;
      
      if (isLargeHistory) {
        // Show initial loading message for large histories
        loadingMsg = document.createElement("div");
        loadingMsg.id = "historyLoadingIndicator";
        loadingMsg.className = "text-center py-4 text-slate-500 text-sm";
        loadingMsg.innerHTML = `
          <div class="flex items-center justify-center space-x-2">
            <div class="animate-spin rounded-full h-4 w-4 border-b-2 border-indigo-600"></div>
            <span>Loading ${messages.length} messages...</span>
          </div>
        `;
        this.elements.messagesContainer.appendChild(loadingMsg);
      }
      
      // Render messages with a small delay for better UX on large histories
      const batchSize = isLargeHistory ? 20 : 10;
      const totalBatches = Math.ceil(messages.length / batchSize);
      
      for (let i = 0; i < messages.length; i += batchSize) {
        const batch = messages.slice(i, i + batchSize);
        const currentBatch = Math.floor(i / batchSize) + 1;
        
        batch.forEach(msg => {
          // Validate message structure
          if (msg && typeof msg.role === 'string' && typeof msg.content === 'string') {
            this.messageRenderer.addMessage(
              this.elements.messagesContainer,
              msg.role,
              msg.content,
              null, // id
              msg.role === 'assistant' ? this.providerManager.getCurrentProvider() : null, // providerName
              msg.timestamp // timestamp
            );
          } else {
            console.warn("Invalid message format:", msg);
          }
        });
        
        // Update progress for large histories
        if (isLargeHistory && loadingMsg) {
          const progress = Math.round((currentBatch / totalBatches) * 100);
          loadingMsg.innerHTML = `
            <div class="flex items-center justify-center space-x-2">
              <div class="animate-spin rounded-full h-4 w-4 border-b-2 border-indigo-600"></div>
              <span>Loading messages: ${progress}% (${i + batch.length}/${messages.length})</span>
            </div>
          `;
        }
        
        // Small delay between batches for very large histories
        if (i + batchSize < messages.length) {
          await new Promise(resolve => setTimeout(resolve, isLargeHistory ? 30 : 50));
        }
      }
      
      // Remove loading indicator
      if (loadingMsg) {
        loadingMsg.remove();
      }
      
      // Scroll to bottom after all messages are rendered
      this.scrollToBottom();
      
      // Show completion message for very large histories
      if (isLargeHistory) {
        const completionMsg = document.createElement("div");
        completionMsg.className = "text-center py-2 text-slate-400 text-xs fade-in";
        completionMsg.textContent = `Loaded ${messages.length} messages`;
        this.elements.messagesContainer.appendChild(completionMsg);
        
        // Fade out the completion message after 3 seconds
        setTimeout(() => {
          completionMsg.style.opacity = '0';
          setTimeout(() => completionMsg.remove(), 300);
        }, 3000);
      }
    }

    scrollToBottom() {
      if (this.elements.messagesContainer) {
        this.elements.messagesContainer.scrollTop = this.elements.messagesContainer.scrollHeight;
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
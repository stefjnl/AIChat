/* global signalR */
(() => {
  "use strict";
  class ChatApp {
    constructor() {
      this.connection = null;
      this.currentThreadId = null;
      this.currentProvider = null;
      this.isStreaming = false;
      this.streamStartTime = null;
      this.accumulatedTokens = { input: 0, output: 0 };
      this.providerMap = new Map();
      this.chatHistory = [];
      this.activeThreadId = null;
      const byId = (id) => document.getElementById(id);
      this.elements = {
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

    async init() {
      await this.restoreThreadFromStorage();
      await this.loadProviders();
      await this.loadChatHistory();
      await this.setupSignalR();
      this.setupEventListeners();
      this.updateCharHint();
      this.updateSendEnabled();
    }

    async loadProviders() {
      try {
        const res = await fetch("/api/providers");
        if (!res.ok) throw new Error("Failed to load providers");
        const data = await res.json();
        const sel = this.elements.providerSelect;
        sel.innerHTML = "";
        if (Array.isArray(data) && data.length > 0) {
          data.forEach(p => {
            const opt = document.createElement("option");
            opt.value = p.name || p.Name;
            const model = p.model || p.Model || "";
            opt.textContent = `${opt.value}${model ? " · " + model : ""}`;
            sel.appendChild(opt);
            this.providerMap.set(opt.value, { model, baseUrl: p.baseUrl || p.BaseUrl || "" });
          });
          this.currentProvider = sel.value;
        } else {
          const opt = document.createElement("option");
          opt.value = "";
          opt.textContent = "No providers configured";
          sel.appendChild(opt);
          this.currentProvider = null;
        }
      } catch (err) {
        console.error(err);
      }
    }

    async setupSignalR() {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect()
        .build();

      this.connection.onreconnecting(() => {
        this.showReconnecting();
      });
      this.connection.onreconnected(() => {
        this.hideReconnecting();
        // Reload chat history after reconnection
        this.loadChatHistory();
      });
      this.connection.onclose(() => {
        this.showDisconnected();
      });

      // Chat history real-time updates
      this.connection.on("ChatHistoryUpdated", (historyItem) => {
        this.updateChatHistoryItem(historyItem);
      });

      this.connection.on("ChatHistoryItemDeleted", (threadId) => {
        this.removeChatHistoryItem(threadId);
      });

      this.connection.on("ActiveThreadChanged", (threadId) => {
        this.updateActiveThread(threadId);
      });

      try {
        await this.connection.start();
        this.hideAllConnectionBanners();
      } catch (err) {
        console.error("Failed to start SignalR", err);
        this.showDisconnected();
      }
    }

    setupEventListeners() {
      const els = this.elements;
      els.sendButton.addEventListener("click", () => this.sendMessage());
      els.messageInput.addEventListener("input", () => {
        this.autoExpandTextarea();
        this.updateCharHint();
        this.updateSendEnabled();
      });
      els.messageInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
          e.preventDefault();
          this.sendMessage();
        }
      });
      els.providerSelect.addEventListener("change", () => {
        this.currentProvider = els.providerSelect.value || null;
        this.updateSendEnabled();
      });
      if (els.retryConnection) {
        els.retryConnection.addEventListener("click", async () => {
          try {
            await this.connection.start();
            this.hideAllConnectionBanners();
          } catch (err) {
            console.error("Retry connection failed", err);
          }
        });
      }
      els.newChatBtn.addEventListener("click", () => this.createNewThread());
    }

    async loadChatHistory() {
      try {
        // Try to get from SignalR first for real-time updates
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
          this.chatHistory = await this.connection.invoke("GetChatHistory");
        } else {
          // Fallback to REST API
          const res = await fetch("/api/chathistory");
          if (!res.ok) throw new Error("Failed to load chat history");
          const data = await res.json();
          this.chatHistory = data.items || [];
        }
        this.displayChatHistory();
      } catch (err) {
        console.error("Error loading chat history:", err);
        // Fallback to basic thread list if chat history fails
        await this.loadThreadsFallback();
      }
    }

    async loadThreadsFallback() {
      try {
        const res = await fetch("/api/threads/list");
        if (!res.ok) throw new Error("Failed to load threads");
        const data = await res.json();
        // Convert thread IDs to basic chat history items
        this.chatHistory = (data.threadIds || []).map(id => ({
          threadId: id,
          title: `Thread ${id.slice(0, 8)}...`,
          createdAt: new Date(),
          lastUpdatedAt: new Date(),
          messageCount: 0,
          isActive: id === this.currentThreadId
        }));
        this.displayChatHistory();
      } catch (err) {
        console.error("Error loading fallback threads:", err);
      }
    }

    displayChatHistory() {
      const container = this.elements.chatHistoryList;
      container.innerHTML = "";
      
      // Sort by last updated (newest first)
      const sortedHistory = this.chatHistory.sort((a, b) =>
        new Date(b.lastUpdatedAt) - new Date(a.lastUpdatedAt)
      );

      sortedHistory.forEach(item => {
        const historyItem = document.createElement("div");
        historyItem.className = `chat-item p-3 rounded-lg hover:bg-slate-100 cursor-pointer text-sm transition-all duration-200 ${
          item.isActive ? 'bg-indigo-50 border border-indigo-200' : 'text-slate-700'
        }`;
        
        // Format timestamp
        const timestamp = this.formatTimestamp(item.lastUpdatedAt);
        
        historyItem.innerHTML = `
          <div class="flex justify-between items-start">
            <div class="flex-1 min-w-0">
              <div class="font-medium text-slate-800 truncate">${this.escapeHtml(item.title)}</div>
              <div class="text-xs text-slate-500 mt-1">${timestamp}</div>
            </div>
            <div class="flex items-center space-x-1 ml-2">
              ${item.messageCount > 0 ? `<span class="text-xs bg-slate-200 text-slate-600 px-2 py-1 rounded-full">${item.messageCount}</span>` : ''}
              <button class="delete-btn opacity-0 hover:opacity-100 text-red-500 hover:text-red-700 p-1 rounded" data-thread-id="${item.threadId}">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                </svg>
              </button>
            </div>
          </div>
        `;
        
        // Click to select thread
        historyItem.addEventListener("click", (e) => {
          if (!e.target.closest('.delete-btn')) {
            this.selectThread(item.threadId);
          }
        });
        
        // Delete button
        const deleteBtn = historyItem.querySelector('.delete-btn');
        deleteBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          this.deleteChatHistoryItem(item.threadId);
        });
        
        container.appendChild(historyItem);
      });
    }

    async selectThread(threadId) {
      try {
        // Set active thread via SignalR for real-time sync
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
          await this.connection.invoke("SetActiveThread", threadId);
        }
        
        this.currentThreadId = threadId;
        this.saveThreadToStorage();
        this.clearMessages();
        this.showWelcome(true);
        this.resetStats();
        
        // Update UI to reflect active thread
        this.updateActiveThread(threadId);
      } catch (err) {
        console.error("Error selecting thread:", err);
      }
    }

    async createNewThread() {
      try {
        const res = await fetch("/api/threads/new", { method: "POST" });
        if (!res.ok) throw new Error("Failed to create thread");
        const data = await res.json();
        this.currentThreadId = data.threadId || data.ThreadId || null;
        this.saveThreadToStorage();
        this.clearMessages();
        this.showWelcome(true);
        this.resetStats();
        
        // Create initial chat history item
        if (this.currentThreadId) {
          await this.createChatHistoryItem(this.currentThreadId, "New Chat");
        }
      } catch (err) {
        console.error("Error creating new thread:", err);
      }
    }

    addMessage(role, content, id) {
      const container = this.elements.messagesContainer;
      const msgId = id || `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const wrap = document.createElement("div");
      wrap.className = role === "user" ? "flex justify-end mb-4" : "flex items-start space-x-4 mb-4";
      wrap.dataset.messageId = msgId;
      this.isAIMessage = role === "assistant"; // Set flag for markdown parsing

      if (role === "user") {
        const inner = document.createElement("div");
        inner.className = "flex items-start space-x-4 max-w-3xl";
        const bubble = document.createElement("div");
        bubble.className = "bg-indigo-50 p-4 rounded-xl shadow-sm order-2";
        const p = document.createElement("p");
        p.className = "text-gray-800 leading-relaxed msg-text";
        p.innerHTML = this.toHtml(content);
        bubble.appendChild(p);
        const avatar = document.createElement("div");
        avatar.className = "w-8 h-8 bg-gray-500 rounded-full order-1 flex items-center justify-center text-white text-xs";
        avatar.textContent = "U";
        inner.appendChild(bubble);
        inner.appendChild(avatar);
        wrap.appendChild(inner);
      } else {
        const avatar = document.createElement("div");
        avatar.className = "w-8 h-8 bg-indigo-500 rounded-full flex items-center justify-center text-white text-xs";
        avatar.textContent = "AI";
        const bubble = document.createElement("div");
        bubble.className = "bg-white p-4 rounded-xl shadow-sm max-w-3xl";
        const providerLine = document.createElement("div");
        providerLine.className = "text-xs text-gray-500 mb-1";
        providerLine.textContent = this.currentProvider || "";
        const p = document.createElement("p");
        p.className = "text-gray-800 leading-relaxed msg-text markdown-content";
        p.innerHTML = this.toHtml(content);
        bubble.appendChild(providerLine);
        bubble.appendChild(p);
        wrap.appendChild(avatar);
        wrap.appendChild(bubble);
      }

      container.appendChild(wrap);
      this.scrollToBottom();
      this.showWelcome(false);
      return msgId;
    }

    updateMessage(id, content) {
      const container = this.elements.messagesContainer;
      const el = Array.from(container.children).find(div => div.dataset.messageId === id);
      if (!el) return;
      const p = el.querySelector(".msg-text");
      if (p) {
        // Check if this is an AI message by looking at the avatar or message structure
        const isAI = el.querySelector('.bg-indigo-500') !== null;
        this.isAIMessage = isAI;
        p.innerHTML = this.toHtml(content);
      }
      this.scrollToBottom();
    }

    async sendMessage() {
      if (this.isStreaming) return;
      const input = this.elements.messageInput;
      const message = (input.value || "").trim();
      if (!message) return;
      if (!this.currentProvider) {
        alert("Please select a provider");
        return;
      }
      if (!this.currentThreadId) {
        await this.createNewThread();
      }
      
      // Generate auto-title from first user message
      if (this.currentThreadId && this.chatHistory.find(h => h.threadId === this.currentThreadId)?.title === "New Chat") {
        try {
          const response = await fetch("/api/chathistory/generate-title", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(message)
          });
          
          if (response.ok) {
            const data = await response.json();
            const title = data.title;
            
            // Update the chat history item
            const historyItem = this.chatHistory.find(h => h.threadId === this.currentThreadId);
            if (historyItem) {
              historyItem.title = title;
              historyItem.firstUserMessage = message;
              this.displayChatHistory();
            }
          }
        } catch (err) {
          console.warn("Failed to generate auto-title:", err);
        }
      }
      
      // 1. Add user message
      this.addMessage("user", message);
      // 2. Assistant placeholder
      const assistantMsgId = this.addMessage("assistant", "");
      this.showStreamingIndicator();
      // 3. Track response time
      this.streamStartTime = Date.now();
      // 4. Set streaming state & disable input
      this.isStreaming = true;
      this.updateSendEnabled();

      let fullResponse = "";
      try {
        const stream = this.connection.stream(
          "StreamChat",
          this.currentProvider,
          message,
          this.currentThreadId
        );
        stream.subscribe({
          next: (chunk) => {
            if (!chunk) return;
            if (chunk.error) {
              this.updateMessage(assistantMsgId, `<span class="text-red-600">${this.escapeHtml(chunk.error)}</span>`);
              return;
            }
            if (chunk.text) {
              fullResponse += chunk.text;
              this.updateMessage(assistantMsgId, fullResponse);
            }
            if (chunk.isFinal) {
              if (chunk.threadId && !this.currentThreadId) {
                this.currentThreadId = chunk.threadId;
                this.saveThreadToStorage();
              }
              const ms = Date.now() - this.streamStartTime;
              if (chunk.usage) {
                this.updateStats(chunk.usage, ms);
              }
              this.hideStreamingIndicator();
            }
          },
          error: (err) => {
            this.handleStreamError(err, assistantMsgId);
          },
          complete: () => {
            this.resetInputState();
          }
        });
      } catch (err) {
        this.handleStreamError(err, assistantMsgId);
      } finally {
        input.value = "";
        this.autoExpandTextarea();
        this.updateCharHint();
      }
    }

    handleStreamError(err, assistantMsgId) {
      console.error("Stream error", err);
      const msg = (err && err.message) ? err.message : "An error occurred while streaming";
      this.updateMessage(assistantMsgId, `<span class="text-red-600">${this.escapeHtml(msg)}</span>`);
      this.hideStreamingIndicator();
      this.resetInputState();
    }

    showStreamingIndicator() {
      this.elements.streamingIndicator.classList.remove("hidden");
    }
    hideStreamingIndicator() {
      this.elements.streamingIndicator.classList.add("hidden");
    }

    updateStats(usage, responseTimeMs) {
      const u = usage || {};
      const input = u.inputTokens ?? u.InputTokens ?? 0;
      const output = u.outputTokens ?? u.OutputTokens ?? 0;
      const total = u.totalTokens ?? u.TotalTokens ?? (input + output);
      this.accumulatedTokens = { input, output, total };
      const els = this.elements;
      els.inputTokens.textContent = String(input);
      els.outputTokens.textContent = String(output);
      els.totalTokens.textContent = String(total);
      els.responseTime.textContent = this.formatDuration(responseTimeMs);
      els.statsDisplay.classList.remove("hidden");
    }

    resetStats() {
      this.accumulatedTokens = { input: 0, output: 0, total: 0 };
      const els = this.elements;
      els.inputTokens.textContent = "0";
      els.outputTokens.textContent = "0";
      els.totalTokens.textContent = "0";
      els.responseTime.textContent = "-";
      els.statsDisplay.classList.add("hidden");
    }

    showReconnecting() {
      const b = this.elements.connectionBanner;
      const r = this.elements.reconnectingBanner;
      const d = this.elements.disconnectedBanner;
      if (b && r && d) {
        b.classList.remove("hidden");
        r.classList.remove("hidden");
        d.classList.add("hidden");
      }
    }
    hideReconnecting() {
      const r = this.elements.reconnectingBanner;
      if (r) r.classList.add("hidden");
      const b = this.elements.connectionBanner;
      if (b && !this.elements.disconnectedBanner.classList.contains("hidden")) return;
      if (b) b.classList.add("hidden");
    }
    showDisconnected() {
      const b = this.elements.connectionBanner;
      const d = this.elements.disconnectedBanner;
      const r = this.elements.reconnectingBanner;
      if (b && d && r) {
        b.classList.remove("hidden");
        d.classList.remove("hidden");
        r.classList.add("hidden");
      }
    }
    hideAllConnectionBanners() {
      const b = this.elements.connectionBanner;
      const d = this.elements.disconnectedBanner;
      const r = this.elements.reconnectingBanner;
      if (b) b.classList.add("hidden");
      if (d) d.classList.add("hidden");
      if (r) r.classList.add("hidden");
    }

    resetInputState() {
      this.isStreaming = false;
      this.updateSendEnabled();
    }

    updateSendEnabled() {
      const hasText = !!(this.elements.messageInput.value || "").trim();
      const canSend = hasText && !!this.currentProvider && !this.isStreaming;
      this.elements.sendButton.disabled = !canSend;
      this.elements.messageInput.disabled = !!this.isStreaming;
    }

    scrollToBottom() {
      const history = document.getElementById("chatHistory");
      if (history) {
        history.scrollTop = history.scrollHeight;
      }
    }

    clearMessages() {
      this.elements.messagesContainer.innerHTML = "";
    }

    showWelcome(show) {
      const w = this.elements.welcomeScreen;
      if (!w) return;
      if (show) w.classList.remove("hidden"); else w.classList.add("hidden");
    }

    autoExpandTextarea() {
      const ta = this.elements.messageInput;
      if (!ta) return;
      ta.style.height = "auto";
      const max = 200;
      ta.style.height = Math.min(ta.scrollHeight, max) + "px";
    }

    updateCharHint() {
      const hint = document.getElementById("charHint");
      if (!hint) return;
      const text = this.elements.messageInput.value || "";
      hint.textContent = `${text.length} / 4000`;
    }

    formatDuration(ms) {
      if (ms >= 1000) return (ms / 1000).toFixed(1) + "s";
      return Math.max(0, Math.round(ms)) + "ms";
    }

    toHtml(text) {
      const t = String(text ?? "");
      // For AI messages, use markdown parsing
      if (this.isAIMessage && typeof marked !== 'undefined') {
        try {
          return marked.parse(t);
        } catch (e) {
          console.warn('Markdown parsing failed, falling back to basic HTML', e);
          return this.escapeHtml(t).replace(/\n/g, "<br>");
        }
      }
      // For user messages, use basic HTML escaping
      return this.escapeHtml(t).replace(/\n/g, "<br>");
    }

    escapeHtml(text) {
      const div = document.createElement('div');
      div.textContent = String(text);
      return div.innerHTML;
    }

    async restoreThreadFromStorage() {
      try {
        const id = localStorage.getItem("threadId");
        if (!id) return;
        const res = await fetch(`/api/threads/${encodeURIComponent(id)}/exists`);
        if (!res.ok) return;
        const data = await res.json();
        const exists = data.exists ?? data.Exists ?? false;
        if (exists) {
          this.currentThreadId = id;
          this.showWelcome(true);
        }
      } catch {
        /* ignore */
      }
    }

    saveThreadToStorage() {
      try {
        if (this.currentThreadId) {
          localStorage.setItem("threadId", this.currentThreadId);
        }
      } catch {
        /* ignore */
      }
    }

    // Chat history helper methods
    async createChatHistoryItem(threadId, title) {
      try {
        const response = await fetch("/api/chathistory", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            threadId,
            title,
            createdAt: new Date().toISOString(),
            lastUpdatedAt: new Date().toISOString(),
            messageCount: 0,
            isActive: true
          })
        });
        
        if (!response.ok) {
          console.warn("Failed to create chat history item");
        }
      } catch (err) {
        console.error("Error creating chat history item:", err);
      }
    }

    async deleteChatHistoryItem(threadId) {
      try {
        const confirmed = confirm("Are you sure you want to delete this chat?");
        if (!confirmed) return;

        // Delete via SignalR for real-time sync
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
          const success = await this.connection.invoke("DeleteChatHistoryItem", threadId);
          if (success) {
            this.removeChatHistoryItem(threadId);
          }
        } else {
          // Fallback to REST API
          const response = await fetch(`/api/chathistory/${threadId}`, { method: "DELETE" });
          if (response.ok) {
            this.removeChatHistoryItem(threadId);
          }
        }
      } catch (err) {
        console.error("Error deleting chat history item:", err);
      }
    }

    updateChatHistoryItem(updatedItem) {
      const index = this.chatHistory.findIndex(item => item.threadId === updatedItem.threadId);
      if (index >= 0) {
        this.chatHistory[index] = updatedItem;
      } else {
        this.chatHistory.push(updatedItem);
      }
      this.displayChatHistory();
    }

    removeChatHistoryItem(threadId) {
      this.chatHistory = this.chatHistory.filter(item => item.threadId !== threadId);
      this.displayChatHistory();
      
      // If we deleted the current active thread, clear it
      if (this.currentThreadId === threadId) {
        this.currentThreadId = null;
        this.saveThreadToStorage();
        this.clearMessages();
        this.showWelcome(true);
      }
    }

    updateActiveThread(threadId) {
      this.chatHistory.forEach(item => {
        item.isActive = item.threadId === threadId;
      });
      this.displayChatHistory();
    }

    formatTimestamp(dateString) {
      const date = new Date(dateString);
      const now = new Date();
      const diffMs = now - date;
      const diffMins = Math.floor(diffMs / 60000);
      const diffHours = Math.floor(diffMs / 3600000);
      const diffDays = Math.floor(diffMs / 86400000);

      if (diffMins < 1) return "Just now";
      if (diffMins < 60) return `${diffMins}m ago`;
      if (diffHours < 24) return `${diffHours}h ago`;
      if (diffDays < 7) return `${diffDays}d ago`;
      
      return date.toLocaleDateString();
    }
  }

  window.addEventListener("DOMContentLoaded", () => {
    const app = new ChatApp();
    app.init();
  });
})();
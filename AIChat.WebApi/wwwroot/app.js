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
      await this.loadThreads();
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
      });
      this.connection.onclose(() => {
        this.showDisconnected();
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

    async loadThreads() {
      try {
        const res = await fetch("/api/threads/list");
        if (!res.ok) throw new Error("Failed to load threads");
        const data = await res.json();
        this.displayThreads(data.threadIds || []);
      } catch (err) {
        console.error(err);
      }
    }

    displayThreads(threadIds) {
      const container = this.elements.chatHistoryList;
      container.innerHTML = "";
      threadIds.forEach(id => {
        const item = document.createElement("div");
        item.className = "p-2 rounded-lg hover:bg-gray-100 cursor-pointer text-sm text-gray-700";
        item.textContent = `Thread ${id.slice(0, 8)}...`;
        item.dataset.threadId = id;
        item.addEventListener("click", () => this.selectThread(id));
        container.appendChild(item);
      });
    }

    async selectThread(threadId) {
      this.currentThreadId = threadId;
      this.saveThreadToStorage();
      this.clearMessages();
      this.showWelcome(true);
      this.resetStats();
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
        await this.loadThreads(); // Reload the thread list
      } catch (err) {
        console.error(err);
      }
    }

    addMessage(role, content, id) {
      const container = this.elements.messagesContainer;
      const msgId = id || `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const wrap = document.createElement("div");
      wrap.className = role === "user" ? "flex justify-end mb-4" : "flex items-start space-x-4 mb-4";
      wrap.dataset.messageId = msgId;

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
        p.className = "text-gray-800 leading-relaxed msg-text";
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
      if (p) p.innerHTML = this.toHtml(content);
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
      hint.textContent = `${text.length} / 4000 characters`;
    }

    formatDuration(ms) {
      if (ms >= 1000) return (ms / 1000).toFixed(1) + "s";
      return Math.max(0, Math.round(ms)) + "ms";
    }

    toHtml(text) {
      const t = String(text ?? "");
      return this.escapeHtml(t).replace(/\n/g, "<br>");
    }

    escapeHtml(text) {
      const map = {
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        "\"": "&quot;",
        "'": '&#039;'
      };
      return String(text).replace(/[&<>"']/g, (m) => map[m]);
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
  }

  window.addEventListener("DOMContentLoaded", () => {
    const app = new ChatApp();
    app.init();
  });
})();
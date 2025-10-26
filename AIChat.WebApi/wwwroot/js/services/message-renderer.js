/* global marked */
(() => {
  "use strict";

  class MessageRenderer {
    constructor() {
      this.isAIMessage = false;
    }

    addMessage(container, role, content, id, providerName = null) {
      const msgId = id || `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const wrap = document.createElement("div");
      wrap.className = role === "user" ? "flex justify-end mb-4" : "flex items-start space-x-4 mb-4";
      wrap.dataset.messageId = msgId;
      this.isAIMessage = role === "assistant";

      if (role === "user") {
        this.createUserMessage(wrap, content);
      } else {
        this.createAIMessage(wrap, content, providerName);
      }

      container.appendChild(wrap);
      return msgId;
    }

    createUserMessage(wrap, content) {
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
    }

    createAIMessage(wrap, content, providerName = null) {
      const avatar = document.createElement("div");
      avatar.className = "w-8 h-8 bg-indigo-500 rounded-full flex items-center justify-center text-white text-xs";
      avatar.textContent = "AI";
      const bubble = document.createElement("div");
      bubble.className = "bg-white p-4 rounded-xl shadow-sm max-w-3xl";
      const providerLine = document.createElement("div");
      providerLine.className = "text-xs text-gray-500 mb-1";
      providerLine.textContent = providerName || "";
      const p = document.createElement("p");
      p.className = "text-gray-800 leading-relaxed msg-text markdown-content";
      p.innerHTML = this.toHtml(content);
      bubble.appendChild(providerLine);
      bubble.appendChild(p);
      wrap.appendChild(avatar);
      wrap.appendChild(bubble);
    }

    updateMessage(container, id, content, providerName = null) {
      const el = Array.from(container.children).find(div => div.dataset.messageId === id);
      if (!el) return false;

      const p = el.querySelector(".msg-text");
      if (p) {
        // Check if this is an AI message by looking at the avatar or message structure
        const isAI = el.querySelector('.bg-indigo-500') !== null;
        this.isAIMessage = isAI;
        p.innerHTML = this.toHtml(content);
      }

      // Update provider name if provided and this is an AI message
      if (providerName && el.querySelector('.bg-indigo-500')) {
        const providerLine = el.querySelector(".text-xs.text-gray-500");
        if (providerLine) {
          providerLine.textContent = providerName;
        }
      }

      return true;
    }

    toHtml(text) {
      const t = String(text ?? "");
      // For AI messages, use markdown parsing
      if (this.isAIMessage && typeof marked !== 'undefined') {
        try {
          return marked.parse(t);
        } catch (e) {
          console.warn('Markdown parsing failed, falling back to basic HTML', e);
          return DOMUtils.escapeHtml(t).replace(/\n/g, "<br>");
        }
      }
      // For user messages, use basic HTML escaping
      return DOMUtils.escapeHtml(t).replace(/\n/g, "<br>");
    }

    clearMessages(container) {
      container.innerHTML = "";
    }

    scrollToBottom(containerId) {
      const container = DOMUtils.byId(containerId);
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }
  }

  window.MessageRenderer = MessageRenderer;
})();
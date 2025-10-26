/* global marked */
(() => {
  "use strict";

  class MessageRenderer {
    constructor() {
      this.isAIMessage = false;
    }

    addMessage(container, role, content, id, providerName = null, timestamp = null) {
      const msgId = id || `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const wrap = document.createElement("div");
      wrap.className = role === "user" ? "flex justify-end mb-4" : "flex items-start space-x-4 mb-4";
      wrap.dataset.messageId = msgId;
      this.isAIMessage = role === "assistant";

      if (role === "user") {
        this.createUserMessage(wrap, content, timestamp);
      } else {
        this.createAIMessage(wrap, content, providerName, timestamp);
      }

      container.appendChild(wrap);
      return msgId;
    }

    createUserMessage(wrap, content, timestamp = null) {
      const inner = document.createElement("div");
      inner.className = "flex items-start space-x-4 max-w-3xl";
      const bubble = document.createElement("div");
      bubble.className = "bg-indigo-50 p-4 rounded-xl shadow-sm order-2";
      const p = document.createElement("p");
      p.className = "text-gray-800 leading-relaxed msg-text";
      p.innerHTML = this.toHtml(content);
      bubble.appendChild(p);
      
      // Add timestamp if provided
      if (timestamp) {
        const timestampEl = document.createElement("div");
        timestampEl.className = "text-xs text-gray-500 mt-2";
        timestampEl.textContent = this.formatTimestamp(timestamp);
        bubble.appendChild(timestampEl);
      }
      
      const avatar = document.createElement("div");
      avatar.className = "w-8 h-8 bg-gray-500 rounded-full order-1 flex items-center justify-center text-white text-xs";
      avatar.textContent = "U";
      inner.appendChild(bubble);
      inner.appendChild(avatar);
      wrap.appendChild(inner);
    }

    createAIMessage(wrap, content, providerName = null, timestamp = null) {
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
      
      // Add timestamp if provided
      if (timestamp) {
        const timestampEl = document.createElement("div");
        timestampEl.className = "text-xs text-gray-500 mt-2";
        timestampEl.textContent = this.formatTimestamp(timestamp);
        bubble.appendChild(timestampEl);
      }
      
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
          const html = marked.parse(t);
          // Apply syntax highlighting and add copy buttons after a short delay
          setTimeout(() => {
            if (typeof hljs !== 'undefined') {
              document.querySelectorAll('pre code').forEach((block) => {
                hljs.highlightElement(block);
              });
            }
            // Add copy buttons to code blocks
            this.addCopyButtonsToCodeBlocks();
          }, 0);
          return html;
        } catch (e) {
          console.warn('Markdown parsing failed, falling back to basic HTML', e);
          return DOMUtils.escapeHtml(t).replace(/\n/g, "<br>");
        }
      }
      // For user messages, use basic HTML escaping
      return DOMUtils.escapeHtml(t).replace(/\n/g, "<br>");
    }

    addCopyButtonsToCodeBlocks() {
      // Find all pre elements that don't already have copy buttons
      const preElements = document.querySelectorAll('pre:not(:has(.copy-button))');
      
      preElements.forEach((pre) => {
        const button = document.createElement('button');
        button.className = 'copy-button';
        button.textContent = 'Copy';
        button.onclick = () => this.copyCodeToClipboard(button, pre);
        pre.style.position = 'relative';
        pre.appendChild(button);
      });
    }
    
    formatTimestamp(timestamp) {
      if (!timestamp) return "";
      
      const date = new Date(timestamp);
      const now = new Date();
      const diffMs = now - date;
      const diffMins = Math.floor(diffMs / (1000 * 60));
      const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
      const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
      
      if (diffMins < 1) return "Just now";
      if (diffMins < 60) return `${diffMins}m ago`;
      if (diffHours < 24) return `${diffHours}h ago`;
      if (diffDays < 7) return `${diffDays}d ago`;
      
      // For older messages, show date
      return date.toLocaleDateString();
    }
  }

  window.MessageRenderer = MessageRenderer;
})();
(() => {
  "use strict";

  class ChatHistoryUI {
    constructor(chatHistoryManager) {
      this.chatHistoryManager = chatHistoryManager;
    }

    displayChatHistory(container, currentThreadId = null, onSelect = null, onDelete = null) {
      if (!container) return;

      container.innerHTML = "";
      const sortedHistory = this.chatHistoryManager.getSortedHistory();

      sortedHistory.forEach(item => {
        const historyItem = this.createHistoryItemElement(item, currentThreadId);
        this.setupHistoryItemEvents(historyItem, item, onSelect, onDelete);
        container.appendChild(historyItem);
      });
    }

    createHistoryItemElement(item, currentThreadId) {
      const historyItem = document.createElement("div");
      historyItem.className = `chat-item p-3 rounded-lg hover:bg-slate-100 cursor-pointer text-sm transition-all duration-200 ${
        item.isActive || item.threadId === currentThreadId ? 'bg-indigo-50 border border-indigo-200' : 'text-slate-700'
      }`;
      
      // Format timestamp
      const timestamp = DOMUtils.formatTimestamp(item.lastUpdatedAt);
      
      historyItem.innerHTML = `
        <div class="flex justify-between items-start">
          <div class="flex-1 min-w-0">
            <div class="font-medium text-slate-800 truncate">${DOMUtils.escapeHtml(item.title || "New Conversation")}</div>
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
      
      return historyItem;
    }

    setupHistoryItemEvents(historyItem, item, onSelect, onDelete) {
      // Click to select thread
      historyItem.addEventListener("click", (e) => {
        if (!e.target.closest('.delete-btn')) {
          onSelect(item.threadId);
        }
      });
      
      // Delete button
      const deleteBtn = historyItem.querySelector('.delete-btn');
      if (deleteBtn) {
        deleteBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          onDelete(item.threadId);
        });
      }
    }

    updateHistoryDisplay(container, currentThreadId = null, onSelect = null, onDelete = null) {
      if (!container) return;
      this.displayChatHistory(container, currentThreadId, onSelect, onDelete);
    }

    addHistoryItem(container, item, currentThreadId, onSelect, onDelete) {
      if (!container) return;

      const historyItem = this.createHistoryItemElement(item, currentThreadId);
      this.setupHistoryItemEvents(historyItem, item, onSelect, onDelete);
      
      // Insert at the top (newest first)
      container.insertBefore(historyItem, container.firstChild);
    }

    removeHistoryItem(container, threadId) {
      if (!container) return;

      const item = container.querySelector(`[data-thread-id="${threadId}"]`);
      if (item && item.parentElement) {
        item.parentElement.remove();
      }
    }

    updateActiveState(container, threadId, isActive) {
      if (!container) return;

      const items = container.querySelectorAll('.chat-item');
      items.forEach(item => {
        const itemThreadId = item.querySelector('.delete-btn')?.dataset.threadId;
        if (itemThreadId === threadId) {
          if (isActive) {
            item.classList.add('bg-indigo-50', 'border', 'border-indigo-200');
            item.classList.remove('text-slate-700');
          } else {
            item.classList.remove('bg-indigo-50', 'border', 'border-indigo-200');
            item.classList.add('text-slate-700');
          }
        }
      });
    }
  }

  window.ChatHistoryUI = ChatHistoryUI;
})();
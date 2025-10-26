(() => {
  "use strict";

  class AutoTitleService {
    constructor() {
      this.generatingTitles = new Set();
    }

    async generateAutoTitle(message, threadId) {
      if (this.generatingTitles.has(threadId)) {
        return null; // Already generating a title for this thread
      }

      try {
        this.generatingTitles.add(threadId);
        
        const response = await fetch("/api/chathistory/generate-title", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(message)
        });
        
        if (response.ok) {
          const data = await response.json();
          return data.title || null;
        }
        
        return null;
      } catch (err) {
        console.warn("Failed to generate auto-title:", err);
        return null;
      } finally {
        this.generatingTitles.delete(threadId);
      }
    }

    shouldGenerateTitle(chatHistory, threadId) {
      if (!threadId) return false;
      
      const historyItem = chatHistory.find(h => h.threadId === threadId);
      if (!historyItem) return false;
      
      // Generate title only for "New Chat" items or items without meaningful titles
      return historyItem.title === "New Chat" || !historyItem.title || historyItem.title.trim().length === 0;
    }

    updateHistoryItemTitle(chatHistory, threadId, title, firstUserMessage) {
      const historyItem = chatHistory.find(h => h.threadId === threadId);
      if (historyItem) {
        historyItem.title = title;
        historyItem.firstUserMessage = firstUserMessage;
        return true;
      }
      return false;
    }
  }

  window.AutoTitleService = AutoTitleService;
})();
(() => {
  "use strict";

  class ChatHistoryManager {
    constructor(signalRManager) {
      this.signalRManager = signalRManager;
      this.chatHistory = [];
    }

    async loadChatHistory() {
      try {
        // Try to get from SignalR first for real-time updates
        if (this.signalRManager.getConnectionState() === signalR.HubConnectionState.Connected) {
          this.chatHistory = await this.signalRManager.invoke("GetChatHistory");
        } else {
          // Fallback to REST API
          const res = await fetch("/api/chathistory");
          if (!res.ok) throw new Error("Failed to load chat history");
          const data = await res.json();
          this.chatHistory = data.items || [];
        }
        return { success: true, history: this.chatHistory };
      } catch (err) {
        console.error("Error loading chat history:", err);
        // Fallback to basic thread list if chat history fails
        return await this.loadThreadsFallback();
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
          isActive: false
        }));
        
        return { success: true, history: this.chatHistory };
      } catch (err) {
        console.error("Error loading fallback threads:", err);
        this.chatHistory = [];
        return { success: false, message: err.message };
      }
    }

    getChatHistory() {
      return [...this.chatHistory];
    }

    updateChatHistoryItem(updatedItem) {
      const index = this.chatHistory.findIndex(item => item.threadId === updatedItem.threadId);
      if (index >= 0) {
        this.chatHistory[index] = updatedItem;
      } else {
        this.chatHistory.push(updatedItem);
      }
      return true;
    }

    removeChatHistoryItem(threadId) {
      const initialLength = this.chatHistory.length;
      this.chatHistory = this.chatHistory.filter(item => item.threadId !== threadId);
      return this.chatHistory.length < initialLength;
    }

    updateActiveThread(threadId) {
      this.chatHistory.forEach(item => {
        item.isActive = item.threadId === threadId;
      });
      return true;
    }

    getActiveThread() {
      return this.chatHistory.find(item => item.isActive) || null;
    }

    async createChatHistoryItem(threadId, title) {
      try {
        // Generate a more sensible default title if none provided
        const effectiveTitle = title && title !== "New Chat" ? title : "New Conversation";
        
        const response = await fetch("/api/chathistory", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            threadId,
            title: effectiveTitle,
            createdAt: new Date().toISOString(),
            lastUpdatedAt: new Date().toISOString(),
            messageCount: 0,
            isActive: true
          })
        });
        
        if (!response.ok) {
          console.warn("Failed to create chat history item");
          return false;
        }
        
        // Update local state
        const newItem = {
          threadId,
          title: effectiveTitle,
          createdAt: new Date().toISOString(),
          lastUpdatedAt: new Date().toISOString(),
          messageCount: 0,
          isActive: true
        };
        
        this.updateChatHistoryItem(newItem);
        return true;
      } catch (err) {
        console.error("Error creating chat history item:", err);
        return false;
      }
    }

    async deleteChatHistoryItem(threadId) {
      try {
        const confirmed = confirm("Are you sure you want to delete this chat?");
        if (!confirmed) return false;

        // Delete via SignalR for real-time sync
        if (this.signalRManager.getConnectionState() === signalR.HubConnectionState.Connected) {
          const success = await this.signalRManager.invoke("DeleteChatHistoryItem", threadId);
          if (success) {
            this.removeChatHistoryItem(threadId);
            return true;
          }
        } else {
          // Fallback to REST API
          const response = await fetch(`/api/chathistory/${threadId}`, { method: "DELETE" });
          if (response.ok) {
            this.removeChatHistoryItem(threadId);
            return true;
          }
        }
        return false;
      } catch (err) {
        console.error("Error deleting chat history item:", err);
        return false;
      }
    }

    getSortedHistory() {
      return [...this.chatHistory].sort((a, b) =>
        new Date(b.lastUpdatedAt) - new Date(a.lastUpdatedAt)
      );
    }
  }

  window.ChatHistoryManager = ChatHistoryManager;
})();
(() => {
  "use strict";

  class ThreadManager {
    constructor() {
      this.currentThreadId = null;
    }

    async createNewThread() {
      try {
        const res = await fetch("/api/threads/new", { method: "POST" });
        if (!res.ok) throw new Error("Failed to create thread");
        const data = await res.json();
        this.currentThreadId = data.threadId || data.ThreadId || null;
        return { success: true, threadId: this.currentThreadId };
      } catch (err) {
        console.error("Error creating new thread:", err);
        return { success: false, message: err.message };
      }
    }

    async selectThread(threadId, signalRManager = null) {
      try {
        // Set active thread via SignalR for real-time sync
        if (signalRManager && signalRManager.getConnectionState() === signalR.HubConnectionState.Connected) {
          await signalRManager.invoke("SetActiveThread", threadId);
        }
        
        this.currentThreadId = threadId;
        return { success: true, threadId: this.currentThreadId };
      } catch (err) {
        console.error("Error selecting thread:", err);
        return { success: false, message: err.message };
      }
    }

    async restoreThreadFromStorage() {
      try {
        const id = localStorage.getItem("threadId");
        if (!id) return { success: false, message: "No thread ID in storage" };
        
        const res = await fetch(`/api/threads/${encodeURIComponent(id)}/exists`);
        if (!res.ok) return { success: false, message: "Failed to check thread existence" };
        
        const data = await res.json();
        const exists = data.exists ?? data.Exists ?? false;
        
        if (exists) {
          this.currentThreadId = id;
          return { success: true, threadId: this.currentThreadId };
        }
        
        return { success: false, message: "Thread does not exist" };
      } catch (err) {
        console.error("Error restoring thread from storage:", err);
        return { success: false, message: err.message };
      }
    }

    saveThreadToStorage() {
      try {
        if (this.currentThreadId) {
          localStorage.setItem("threadId", this.currentThreadId);
          return true;
        }
        return false;
      } catch (err) {
        console.error("Error saving thread to storage:", err);
        return false;
      }
    }

    clearThreadFromStorage() {
      try {
        localStorage.removeItem("threadId");
        return true;
      } catch (err) {
        console.error("Error clearing thread from storage:", err);
        return false;
      }
    }

    getCurrentThreadId() {
      return this.currentThreadId;
    }

    clearCurrentThread() {
      this.currentThreadId = null;
      this.clearThreadFromStorage();
    }
  }

  window.ThreadManager = ThreadManager;
})();
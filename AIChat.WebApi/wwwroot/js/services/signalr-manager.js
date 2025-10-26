/* global signalR */
(() => {
  "use strict";

  class SignalRManager {
    constructor() {
      this.connection = null;
      this.eventHandlers = new Map();
    }

    async setupConnection() {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect()
        .build();

      // Set up default event handlers
      this.connection.onreconnecting(() => {
        this.triggerEvent('reconnecting');
      });

      this.connection.onreconnected(() => {
        this.triggerEvent('reconnected');
      });

      this.connection.onclose(() => {
        this.triggerEvent('disconnected');
      });

      // Chat history real-time updates
      this.connection.on("ChatHistoryUpdated", (historyItem) => {
        this.triggerEvent('chatHistoryUpdated', historyItem);
      });

      this.connection.on("ChatHistoryItemDeleted", (threadId) => {
        this.triggerEvent('chatHistoryItemDeleted', threadId);
      });

      this.connection.on("ActiveThreadChanged", (threadId) => {
        this.triggerEvent('activeThreadChanged', threadId);
      });

      try {
        await this.connection.start();
        this.triggerEvent('connected');
        return true;
      } catch (err) {
        console.error("Failed to start SignalR", err);
        this.triggerEvent('connectionFailed', err);
        return false;
      }
    }

    on(eventName, handler) {
      if (!this.eventHandlers.has(eventName)) {
        this.eventHandlers.set(eventName, []);
      }
      this.eventHandlers.get(eventName).push(handler);
    }

    off(eventName, handler) {
      if (this.eventHandlers.has(eventName)) {
        const handlers = this.eventHandlers.get(eventName);
        const index = handlers.indexOf(handler);
        if (index > -1) {
          handlers.splice(index, 1);
        }
      }
    }

    triggerEvent(eventName, ...args) {
      if (this.eventHandlers.has(eventName)) {
        this.eventHandlers.get(eventName).forEach(handler => {
          try {
            handler(...args);
          } catch (err) {
            console.error(`Error in event handler for ${eventName}:`, err);
          }
        });
      }
    }

    async invoke(methodName, ...args) {
      if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
        try {
          return await this.connection.invoke(methodName, ...args);
        } catch (err) {
          console.error(`Error invoking ${methodName}:`, err);
          throw err;
        }
      } else {
        throw new Error("SignalR connection is not established");
      }
    }

    stream(methodName, ...args) {
      if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
        return this.connection.stream(methodName, ...args);
      } else {
        throw new Error("SignalR connection is not established");
      }
    }

    getConnectionState() {
      return this.connection ? this.connection.state : signalR.HubConnectionState.Disconnected;
    }

    async retryConnection() {
      if (this.connection) {
        try {
          await this.connection.start();
          this.triggerEvent('connected');
          return true;
        } catch (err) {
          console.error("Retry connection failed", err);
          this.triggerEvent('connectionFailed', err);
          return false;
        }
      }
      return false;
    }
  }

  window.SignalRManager = SignalRManager;
})();
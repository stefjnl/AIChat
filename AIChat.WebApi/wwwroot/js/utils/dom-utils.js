/* global document */
(() => {
  "use strict";

  class DOMUtils {
    static byId(id) {
      return document.getElementById(id);
    }

    static escapeHtml(text) {
      const div = document.createElement('div');
      div.textContent = String(text);
      return div.innerHTML;
    }

    static formatDuration(ms) {
      if (ms >= 1000) return (ms / 1000).toFixed(1) + "s";
      return Math.max(0, Math.round(ms)) + "ms";
    }

    static formatTimestamp(dateString) {
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

  window.DOMUtils = DOMUtils;
})();
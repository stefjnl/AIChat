(() => {
  "use strict";

  class ProviderManager {
    constructor() {
      this.providers = new Map();
      this.currentProvider = null;
    }

    async loadProviders() {
      try {
        const res = await fetch("/api/providers");
        if (!res.ok) throw new Error("Failed to load providers");
        const data = await res.json();
        
        this.providers.clear();
        
        if (Array.isArray(data) && data.length > 0) {
          data.forEach(p => {
            const name = p.name || p.Name;
            const model = p.model || p.Model || "";
            const baseUrl = p.baseUrl || p.BaseUrl || "";
            this.providers.set(name, { model, baseUrl });
          });
          this.currentProvider = data[0].name || data[0].Name;
          return { success: true, providers: Array.from(this.providers.keys()) };
        } else {
          this.currentProvider = null;
          return { success: false, message: "No providers configured" };
        }
      } catch (err) {
        console.error("Error loading providers:", err);
        this.currentProvider = null;
        return { success: false, message: err.message };
      }
    }

    getProviderInfo(providerName) {
      return this.providers.get(providerName) || null;
    }

    getCurrentProvider() {
      return this.currentProvider;
    }

    setCurrentProvider(providerName) {
      if (this.providers.has(providerName)) {
        this.currentProvider = providerName;
        return true;
      }
      return false;
    }

    getAllProviders() {
      return Array.from(this.providers.entries()).map(([name, info]) => ({
        name,
        model: info.model,
        baseUrl: info.baseUrl
      }));
    }

    populateSelectElement(selectElement) {
      selectElement.innerHTML = "";
      
      if (this.providers.size > 0) {
        this.providers.forEach((info, name) => {
          const opt = document.createElement("option");
          opt.value = name;
          opt.textContent = info.model ? `${name} · ${info.model}` : name;
          selectElement.appendChild(opt);
        });
        
        if (this.currentProvider) {
          selectElement.value = this.currentProvider;
        }
      } else {
        const opt = document.createElement("option");
        opt.value = "";
        opt.textContent = "No providers configured";
        selectElement.appendChild(opt);
      }
    }
  }

  window.ProviderManager = ProviderManager;
})();
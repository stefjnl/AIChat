(() => {
  "use strict";

  class AutoTitleService {
    constructor() {
      this.generatingTitles = new Set();
      this.pendingTitles = new Map(); // Store pending title generation with context
      this.titlePatterns = [
        "Discussion about {topic}",
        "{question} about {subject}",
        "How to {task}",
        "{topic} Analysis",
        "{subject} Help",
        "{task} Tutorial",
        "{topic} Explanation",
        "{subject} Guidance"
      ];
    }

    // Enhanced title generation that considers both user message and AI response
    async generateAutoTitle(userMessage, threadId, aiResponse = null) {
      if (this.generatingTitles.has(threadId)) {
        return null; // Already generating a title for this thread
      }

      try {
        this.generatingTitles.add(threadId);
        
        // Preprocess the messages to extract key information
        const context = this.extractContext(userMessage, aiResponse);
        
        // Try client-side generation first for faster response
        const clientTitle = this.generateTitleFromContext(context);
        
        // If we have enough context, use the client-generated title
        if (clientTitle && this.isTitleMeaningful(clientTitle)) {
          // Still send to API in background for potential improvement
          this.sendToApiForImprovement(userMessage, aiResponse, threadId, clientTitle);
          return clientTitle;
        }
        
        // Fallback to API generation with timeout
        const response = await this.fetchWithTimeout("/api/chathistory/generate-title", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            userMessage,
            aiResponse,
            context
          })
        }, 3000); // 3 second timeout
        
        if (response.ok) {
          const data = await response.json();
          return data.title || clientTitle || this.generateFallbackTitle(context);
        }
        
        return clientTitle || this.generateFallbackTitle(context);
      } catch (err) {
        console.warn("Failed to generate auto-title:", err);
        // Final fallback - generate client-side title
        const context = this.extractContext(userMessage, aiResponse);
        return this.generateFallbackTitle(context);
      } finally {
        this.generatingTitles.delete(threadId);
      }
    }

    // Extract context from messages for better title generation
    extractContext(userMessage, aiResponse = null) {
      const userText = (userMessage || "").trim();
      const aiText = (aiResponse || "").trim();
      
      // Detect message type
      const messageType = this.detectMessageType(userText);
      
      // Extract key topics and keywords
      const keywords = this.extractKeywords(userText, aiText);
      
      // Identify the main subject/topic
      const subject = this.identifySubject(userText, aiText, keywords);
      
      // For questions, extract the question part
      const question = messageType === 'question' ? this.extractQuestion(userText) : null;
      
      // For tasks, extract the action verb
      const task = messageType === 'task' ? this.extractTask(userText) : null;
      
      return {
        messageType,
        keywords,
        subject,
        question,
        task,
        userText,
        aiText
      };
    }

    // Detect if the message is a question, request, or statement
    detectMessageType(message) {
      const questionIndicators = ['?', 'what', 'how', 'why', 'when', 'where', 'which', 'who', 'can', 'could', 'would', 'should', 'is', 'are', 'do', 'does'];
      const taskIndicators = ['help', 'create', 'write', 'generate', 'make', 'build', 'implement', 'develop', 'design', 'explain', 'show', 'tell'];
      
      const lowerMessage = message.toLowerCase();
      
      // Check for questions
      if (this.isQuestion(message, lowerMessage, questionIndicators)) {
        return 'question';
      }
      
      // Check for tasks/requests
      if (taskIndicators.some(indicator => lowerMessage.includes(indicator))) {
        return 'task';
      }
      
      return 'statement';
    }

    // Helper method to check if a message contains question indicators
    isQuestion(message, lowerMessage, questionIndicators) {
      return message.includes('?') || 
             questionIndicators.some(indicator => 
               lowerMessage.startsWith(indicator + ' ') || 
               lowerMessage.includes(' ' + indicator + ' ')
             );
    }

    // Extract important keywords from the messages
    extractKeywords(userText, aiText) {
      const allText = `${userText} ${aiText}`.toLowerCase();
      
      // Common technical terms that often indicate topics
      const technicalTerms = [
        'javascript', 'python', 'java', 'c#', 'react', 'angular', 'vue', 'node', 'express',
        'api', 'database', 'sql', 'nosql', 'mongodb', 'mysql', 'postgresql',
        'html', 'css', 'frontend', 'backend', 'fullstack', 'devops', 'docker',
        'algorithm', 'data structure', 'function', 'class', 'method', 'variable',
        'bug', 'error', 'issue', 'problem', 'solution', 'fix', 'debug',
        'code', 'programming', 'development', 'software', 'application', 'system',
        'test', 'testing', 'unit test', 'integration', 'deployment'
      ];
      
      // Extract words that might be important
      const words = allText.match(/\b[a-z]{3,}\b/g) || [];
      const keywords = [];
      
      // Add technical terms found in the text
      technicalTerms.forEach(term => {
        if (allText.includes(term)) {
          keywords.push(term);
        }
      });
      
      // Add capitalized words (likely proper nouns)
      const capitalizedWords = (userText + ' ' + aiText).match(/\b[A-Z][a-z]+\b/g) || [];
      keywords.push(...capitalizedWords);
      
      // Remove duplicates and return unique keywords
      return [...new Set(keywords)].slice(0, 5); // Limit to 5 most important keywords
    }

    // Identify the main subject/topic of the conversation
    identifySubject(userText, aiText, keywords) {
      // If we have technical keywords, use the first/most prominent one
      if (keywords.length > 0) {
        // Prioritize technical terms
        const technicalKeywords = keywords.filter(k =>
          ['javascript', 'python', 'java', 'c#', 'react', 'api', 'database', 'code'].includes(k.toLowerCase())
        );
        
        if (technicalKeywords.length > 0) {
          return this.capitalizeWords(technicalKeywords[0]);
        }
        
        // Use the first keyword if no technical ones
        return this.capitalizeWords(keywords[0]);
      }
      
      // Try to extract from the first sentence
      const firstSentence = userText.split(/[.!?]/)[0];
      const words = firstSentence.split(' ').filter(w => w.length > 3);
      
      if (words.length > 0) {
        return this.capitalizeWords(words[0]);
      }
      
      return null;
    }

    // Extract the question part from a question message
    extractQuestion(message) {
      // Remove question words and keep the core question
      const questionWords = ['what', 'how', 'why', 'when', 'where', 'which', 'who', 'can', 'could', 'would', 'should', 'is', 'are', 'do', 'does'];
      let question = message.trim();
      
      questionWords.forEach(word => {
        const regex = new RegExp(`^${word}\\s+(is|are)?\\s+`, 'i');
        question = question.replace(regex, '');
      });
      
      // Remove question mark and clean up
      question = question.replace(/\?$/, '').trim();
      
      return question.length > 0 ? question : null;
    }

    // Extract the task/action from a task message
    extractTask(message) {
      const taskVerbs = ['help', 'create', 'write', 'generate', 'make', 'build', 'implement', 'develop', 'design', 'explain', 'show', 'tell'];
      const lowerMessage = message.toLowerCase();
      
      for (const verb of taskVerbs) {
        if (lowerMessage.includes(verb)) {
          const regex = new RegExp(`${verb}\\s+(.+)`, 'i');
          const match = message.match(regex);
          if (match && match[1]) {
            return match[1].trim();
          }
        }
      }
      
      return null;
    }

    // Generate title from extracted context
    generateTitleFromContext(context) {
      const { messageType, subject, question, task, keywords } = context;
      
      // Use different patterns based on message type
      if (messageType === 'question' && question && subject) {
        return `${subject} Question`;
      }
      
      if (messageType === 'task' && task) {
        const taskTitle = task.length > 30 ? task.substring(0, 30) + '...' : task;
        return this.capitalizeWords(taskTitle);
      }
      
      if (subject) {
        if (messageType === 'question') {
          return `${subject} Discussion`;
        }
        return subject;
      }
      
      // Fallback to keywords
      if (keywords.length > 0) {
        return this.capitalizeWords(keywords[0]);
      }
      
      return null;
    }

    // Check if a title is meaningful (not too generic)
    isTitleMeaningful(title) {
      const genericTerms = ['discussion', 'question', 'help', 'chat', 'conversation', 'talk'];
      const lowerTitle = title.toLowerCase();
      
      // If it's just a generic term, it's not meaningful
      if (genericTerms.includes(lowerTitle)) {
        return false;
      }
      
      // If it contains a generic term but also specific content, it's meaningful
      if (genericTerms.some(term => lowerTitle.includes(term))) {
        // Check if there's more than just the generic term
        const words = title.split(' ').filter(w => w.length > 2);
        return words.length > 1;
      }
      
      return true;
    }

    // Generate a fallback title when all else fails
    generateFallbackTitle(context) {
      const { userText, messageType } = context;
      
      // Use first few words of user message as fallback
      const words = userText.split(' ').filter(w => w.length > 0);
      if (words.length > 0) {
        let title = words.slice(0, 4).join(' ');
        if (title.length > 40) {
          title = title.substring(0, 40);
          const lastSpace = title.lastIndexOf(' ');
          if (lastSpace > 20) {
            title = title.substring(0, lastSpace);
          }
          title += '...';
        }
        
        // Add message type prefix if helpful
        if (messageType === 'question' && !title.includes('?')) {
          title = 'Q: ' + title;
        }
        
        return this.capitalizeWords(title);
      }
      
      return 'New Conversation';
    }

    // Capitalize words properly
    capitalizeWords(str) {
      return str.replace(/\b\w/g, l => l.toUpperCase());
    }

    // Fetch with timeout for API calls
    async fetchWithTimeout(url, options, timeout = 3000) {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), timeout);
      
      try {
        const response = await fetch(url, {
          ...options,
          signal: controller.signal
        });
        clearTimeout(timeoutId);
        return response;
      } catch (error) {
        clearTimeout(timeoutId);
        throw error;
      }
    }

    // Send to API for potential improvement without blocking
    async sendToApiForImprovement(userMessage, aiResponse, threadId, currentTitle) {
      try {
        const context = this.extractContext(userMessage, aiResponse);
        
        const response = await fetch("/api/chathistory/generate-title", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            userMessage,
            aiResponse,
            context,
            currentTitle
          })
        });
        
        if (response.ok) {
          const data = await response.json();
          // Store the improved title for potential later use
          if (data.title && data.title !== currentTitle) {
            this.pendingTitles.set(threadId, data.title);
          }
        }
      } catch (err) {
        // Silently fail - we already have a fallback title
        console.debug("API improvement failed:", err);
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

    // Get any pending improved title for a thread
    getPendingTitle(threadId) {
      const title = this.pendingTitles.get(threadId);
      if (title) {
        this.pendingTitles.delete(threadId);
      }
      return title;
    }
  }

  window.AutoTitleService = AutoTitleService;
})();
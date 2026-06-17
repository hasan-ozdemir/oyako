# Oyako Development History

This document summarizes the session-derived engineering timeline used to reconstruct the public pre-alpha Git history. Raw Codex JSONL sessions are intentionally not committed because they can contain volatile local context, command output, and sensitive operational details.

- 001. Scaffolded the React TypeScript Vite web app and ASP.NET 10 Web API structure.
- 002. Established SQLite as the portable backend persistence store.
- 003. Introduced Clean Architecture layering across Domain, Application, Infrastructure, and Presentation.
- 004. Added code-first database bootstrap behavior for empty development and runtime environments.
- 005. Implemented initial Oyak Dijital website crawling and text extraction.
- 006. Added browser-rendered scraping with Playwright for user-visible page text.
- 007. Hardened crawler request delays, timeouts, and fail-forward behavior.
- 008. Added source and document lifecycle tables for knowledge-bank management.
- 009. Implemented system instruction cache generation from enabled knowledge documents.
- 010. Persisted and reloaded knowledge cache state to keep startup fast.
- 011. Added one-shot chat behavior with system and user messages only.
- 012. Introduced Ollama local provider support for local models.
- 013. Added Azure AI provider configuration and streaming answer integration.
- 014. Split Ollama behavior into local and cloud provider paths.
- 015. Added provider routing and provider fallback for pre-token failures.
- 016. Implemented ready-question generation from current knowledge content.
- 017. Linked ready questions to enabled documents so disabled content is not suggested.
- 018. Added settings UI for provider, model, question counts, auto-send, and source display preferences.
- 019. Made settings persistent through backend storage.

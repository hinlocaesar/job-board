# Project: Umbraco Headless + Vue 3

## Structure
- `/backend`  Umbraco (.NET), Content Delivery API v2
- `/frontend` Vue 3 + TypeScript + Vite (Composition API, `<script setup>`)

## Tooling priority
1. Use official MCP servers and tools first (Umbraco Developer MCP, `@umbraco-cms/mcp-dev`,
   plus any Vue/Vite/Playwright MCP available).
2. If one isn't configured, tell me what to add to `opencode.json`, then use the fallback.
3. Fallback: official docs (docs.umbraco.com, vuejs.org) → the Delivery API OpenAPI spec → manual code.
4. Verify unfamiliar Umbraco/Vue APIs against the docs for the version in use.

## Rules
- The Umbraco Developer MCP is for the local/dev instance only. Never production.
- Content comes from the Delivery API. The Management API is only for the MCP/backoffice work.
- Generate TS types from `/umbraco/swagger/delivery/swagger.json`. Don't hand-write them.
- Never expose a production API key in the browser bundle. Use a proxy/BFF.
- No secrets in commits. Keep `.env` gitignored and maintain `.env.example`.
- Sanitize any rich-text HTML (DOMPurify).
- Small commits. Don't touch unrelated files.

## Commands
- Frontend: `pnpm dev`, `pnpm type-check`, `pnpm lint`, `pnpm test`
- Backend: `dotnet run` (from `/backend`)
- Rebuild the Delivery API index: Backoffice → Settings → Examine Management → DeliveryApiContentIndex

## Definition of done
Type-check, lint and tests pass, and the pages were checked rendering real Umbraco content.
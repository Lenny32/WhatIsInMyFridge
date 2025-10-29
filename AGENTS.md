# Repository Guidelines

## Project Structure & Module Organization
- `backend/WhatIsInMyFridge.Api/` contains the ASP.NET Core minimal API, with models in `Models/`, request DTOs in `Dtos/`, and persistence helpers in `Services/`.
- `backend/data/items.json` is the lightweight JSON store. Treat it as mutable runtime data; avoid checking in large sample payloads.
- `frontend/` hosts the Svelte app orchestrated by Deno tasks. UI logic sits in `src/App.svelte`, shared types and HTTP utilities in `src/lib/`.

## Build, Test, and Development Commands
- `dotnet run --project backend/WhatIsInMyFridge.Api` starts the API on port 5000.
- `dotnet watch --project backend/WhatIsInMyFridge.Api run` enables hot reload for backend changes.
- `cd frontend && deno task dev` launches Vite on port 5173 with an `/api` proxy.
- `deno task build` outputs a production-ready SPA to `frontend/dist/`.
- `deno task check` executes `svelte-check` for type and accessibility hints.

## Coding Style & Naming Conventions
- C# code follows the default .NET formatting: 4-space indentation, PascalCase for classes/methods, camelCase for locals/parameters. Prefer expression-bodied members only when readability improves.
- Run `dotnet format` before opening a PR if you adjust backend code.
- Svelte and TypeScript modules use camelCase for functions/variables, PascalCase for components, and 2-space indentation. Keep component scripts lean and extract helpers to `src/lib/` when reusable.
- Keep enum values in `StorageLocation` aligned with lowercase strings expected by the frontend (`fridge`, `freezer`, `pantry`).

## Testing Guidelines
- Backend: target xUnit + FluentAssertions under `backend/WhatIsInMyFridge.Api.Tests/` (not yet present). Mirror the namespace structure of the API.
- Frontend: prefer Playwright for end-to-end flows and `deno test` (or Vitest via `deno run -A npm:vitest`) for component logic. Name specs with `.spec.ts`.
- Until suites are implemented, smoke-test by running the API and UI together and exercising item creation, quantity adjustments, and to-buy filters.

## Commit & Pull Request Guidelines
- Use present-tense, 50-character summaries (e.g., `Add freezer restock threshold UI`). Group related backend and frontend work in separate commits when practical.
- Reference issues with `Fixes #id` or `Refs #id` in the commit body.
- PRs should include a concise summary, testing notes (`dotnet run`, `deno task dev`), screenshots or GIFs for UI changes, and mention of any schema or API adjustments.

## Environment & Configuration Tips
- Copy `frontend/.env.example` to `frontend/.env` when pointing the UI at a remote API; update `VITE_API_BASE`.
- Avoid committing production data inside `backend/data/`. For seeded content, provide scripts or documentation instead.

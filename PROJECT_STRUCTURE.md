# Project Structure and Runtime Flow

This document explains how the folders/projects in this repo relate and how the application runs end-to-end.

**Top-Level Structure**
- `Tracker.Api`: Backend API (.NET 8). Owns database access, auth, ingest endpoints, admin seed, and health checks.
- `EmployeeTracker-Frontend`: React + Vite UI. Talks to `Tracker.Api` via `/api` proxy.
- `Agent.Service`: Local agent runtime. Hosts the Local API (default `http://127.0.0.1:43121`), persists outbox, and uploads data to the backend.
- `Agent.Windows`: Windows collector. Captures focused app + idle info and posts to Local API.
- `Agent.Mac`: macOS collector. Captures focused app + idle info and posts to Local API.
- `Agent.Shared`: Shared models/config/local API contracts used by the agent projects.
- `Extensions`: Browser extension (manifest + background script). Sends web activity to the Local API.
- `loadtests`: k6 scripts to load test ingest endpoints.
- `tests`: Integration test project(s) for API behavior.
- `deploy`: Deployment-related notes and logging configs.
- `publish`: Published build outputs (currently `AgentService`).
- `scripts`: Helper scripts for running agents and smoke tests.
- `docs`: Additional documentation (agent notes, Local API contract).
- `Dockerfile` and `docker-compose.yml`: Containerized backend API setup.
- `EmployeeTracker.sln`: Solution file for .NET projects.

**How the Application Runs**
1. **Backend API (`Tracker.Api`)**
   - Starts on `http://localhost:5002` (dev profile).
   - Connects to PostgreSQL (connection string via `ConnectionStrings:Postgres`).
   - Exposes ingest endpoints for web/app/idle/device sessions and admin endpoints.

2. **Local Agent Runtime (`Agent.Service`)**
   - Hosts the Local API on `http://127.0.0.1:43121` by default.
   - Receives web events, app focus, and idle samples from collectors/extension.
   - Sessionizes events, writes to an outbox, and uploads batches to `Tracker.Api`.
   - Local API is protected by `X-Agent-Token` (configured in `Agent.Service/appsettings.json` or env `AGENT_LOCAL_API_TOKEN`).

3. **Collectors (OS-specific)**
   - `Agent.Windows` and `Agent.Mac` poll local OS state (focused app + idle time).
   - They POST to Local API endpoints on the agent service using the token.

4. **Browser Extension**
   - Runs in the browser and posts web activity to Local API.
   - The agent service sessionizes and forwards to the backend.

5. **Frontend UI**
   - Runs on `http://localhost:8080` in dev.
   - Proxies API calls to `http://localhost:5002` via `/api`.

**Typical Local Run Order**
- Start PostgreSQL.
- Run `Tracker.Api`.
- Run `Agent.Service` (Local API + outbox + uploader).
- Run OS collector (`Agent.Windows` or `Agent.Mac`).
- Run `EmployeeTracker-Frontend`.
- Load the browser extension from `Extensions/` (for web tracking).

**Data Flow Summary**
- Browser extension and OS collectors send events to Local API.
- Local API sessionizes and stores in outbox.
- Outbox sender posts to backend ingest endpoints.
- Backend persists to PostgreSQL and serves UI.

If you want this expanded with setup commands or deployment instructions, tell me which environment (macOS or Windows) and which components you want to run.

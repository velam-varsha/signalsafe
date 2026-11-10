# SignalSafe web app

React 19 + TypeScript, built with Vite. It shows the track diagram, findings and routes returned by the SignalSafe API.

## Run it

Start the API first (from the repository root):

```bash
dotnet run --project src/SignalSafe.Api
```

Then, in a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. In development, Vite forwards `/api` requests to the API on http://localhost:5080.

To use an API hosted elsewhere, set `VITE_API_URL` (for example in `.env.local`) and add the web app's address to the API's `Cors:AllowedOrigins` setting.

## How it is built and hosted

| Where | How the web app is served | How it reaches the API |
| --- | --- | --- |
| Development (`npm run dev`) | Vite dev server on http://localhost:5173 | Vite forwards `/api` to http://localhost:5080 (see `vite.config.ts`) |
| Docker (`docker compose up`) | nginx in a container on http://localhost:8080 (see `Dockerfile`, `nginx.conf`) | nginx forwards `/api` to the API container |
| Azure (live) | Azure Static Web Apps (see `public/staticwebapp.config.json`) | Calls the API's full address, built in with `VITE_API_URL`; the API allows it with CORS |

`npm run build` writes plain files to `dist/`. The Deploy workflow builds them with `VITE_API_URL` set to the live API address and uploads them to Static Web Apps. `staticwebapp.config.json` sends unknown paths back to `index.html` and adds two security headers. See [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md) for the whole pipeline.

## Scripts

| Command | What it does |
| --- | --- |
| `npm run dev` | Start the development server |
| `npm test` | Run the tests (Vitest + Testing Library) |
| `npm run lint` | Lint with oxlint |
| `npm run build` | Type-check and build for production into `dist/` |

## Structure

```
src/api/          API types and client
src/diagram/      Drawing maths: signal placement, labels, view box
src/components/   Schematic (SVG), findings, routes and simulator panels
src/sim/          Interlocking simulator logic (set, lock, refuse, occupy)
src/App.tsx       Page layout and state
src/test/         Test setup and API response fixtures
```

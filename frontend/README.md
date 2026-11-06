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
src/components/   Schematic (SVG), findings panel, routes panel
src/App.tsx       Page layout and state
src/test/         Test setup and API response fixtures
```

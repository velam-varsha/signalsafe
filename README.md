# SignalSafe

**A spell-checker for railway signalling data.** SignalSafe reads a track layout, works out every route the layout allows, and checks the designer's control tables against it to find missing routes, wrong point positions, unlisted conflicts and missing overlaps.

> Portfolio project. All layouts and tables in this repository are invented examples. SignalSafe is an advisory design-checking aid, **not** a certified safety system, and does not replace independent checking or formal verification.

## Status

| Phase | Scope | Status |
| --- | --- | --- |
| Week 1 | Core model, layout validation, route search, conflicts | In progress |
| Week 2 | Excel control table import, rules R01–R10, Excel export | Planned |
| Week 3 | ASP.NET Core API, React + TypeScript schematic | Planned |
| Week 4 | Route simulator, Docker, CI/CD, Azure deploy | Planned |

## How it works (so far)

1. **Load** a layout from JSON: track sections, points and signals ([sample](samples/westbrook-junction.json)).
2. **Validate** it: unique IDs, points wired to the right sections, signals on real joints.
3. **Build a graph** where track sections are edges and joints/points are nodes.
4. **Search** depth-first from every signal in its direction of travel. At facing points the search tries both Normal and Reverse; at trailing points it records the position the train needs. The route ends at the next signal reading the same way.
5. **Find conflicts**: route pairs that share a section or need the same points in opposite positions.

For the example Westbrook Junction this derives 5 routes, `S1(M)-S3`, `S1(M)-S5`, `S2(M)-S4`, `S4(M)-S6` and `S7(S)-S6`, and 2 conflicting pairs.

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows, macOS or Linux).

```bash
dotnet test
```

## Project layout

```
src/SignalSafe.Core          Core engine (no web or UI code)
  Domain/                    TrackSection, Points, Signal, Layout
  Layouts/                   JSON loader and layout validator
  Routing/                   Route search and conflict detection
tests/SignalSafe.Core.Tests  xUnit tests
samples/                     Invented example layouts
```

## Standards context

The checks are simplified versions of UK signalling principles, written in this project's own words. Relevant published standards include RSSB RIS-0703-CCS (signalling layout and aspect sequences) and Network Rail NR/L2/SIG/30009 (signalling principles) and NR/L2/SIG/11201 Module A5-3 (presentation of control tables). No text from those documents is reproduced here.

## Licence

MIT

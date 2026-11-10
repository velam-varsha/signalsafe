# Glossary

Plain-English meanings of the terms used in SignalSafe and its documentation, grouped by topic.

## Railway signalling

| Term | Meaning |
| --- | --- |
| Axle counter / track circuit | Equipment that detects whether a track section is clear or occupied by a train |
| Control table | A table that defines, for each route, the points, track sections, overlap and conflicts the interlocking must check |
| Conflicting routes | Two routes that cannot be set at the same time, because they share track or need points in different positions |
| Crossover | A short connection between two parallel lines, with a set of points at each end that always move together |
| Entrance / exit signal | The signal a route starts at, and the next signal it ends at |
| Facing / trailing points | Facing: the train arrives on the toe and can be sent either way. Trailing: the train arrives on one heel and leaves by the toe |
| Flank protection | Holding nearby points (or a signal at danger) so no other movement can run into the side of a set route |
| Interlocking | The safety system that stops signals and points being set in an unsafe combination |
| Locking | Preventing points from moving while a route that needs them is set |
| Normal / Reverse | The two positions of a set of points: usually straight on (Normal) and diverging (Reverse) |
| Opposing (head-on) routes | Routes in opposite directions over the same track |
| Overlap | Extra track beyond the exit signal kept clear in case a train overruns the signal |
| Points (switch) | Movable rails that send a train one way or the other. Toe: the single end; heels: the two diverging ends |
| Route | A signalled path for a train from an entrance signal to an exit signal |
| Scheme plan | The signalling layout drawing: track, signals, points and train detection |
| Shunt route | A low-speed route, usually from a shunt (position light) signal, e.g. out of a siding |
| Trap points | Points that derail a runaway vehicle before it can foul a running line |

## SignalSafe concepts

| Term | Meaning |
| --- | --- |
| Layout file | The JSON description of the track: sections, points, signals and (optionally) drawing coordinates |
| Derived route | A route the engine worked out itself by searching the layout |
| Finding | One problem a rule found: rule, severity, route, message and where it is in the tables |
| Rule (R01–R10) | One check comparing the control tables with what the layout requires |
| Error / Warning | Error: safety-relevant. Warning: consistency, needs a designer's attention |
| Planted mistake | A deliberate error in the example tables, used to show and test that the rules work |
| Draft tables | Control tables generated from a layout; a starting point for a designer, and known-good test data |
| Simulator | The web app tab that sets and cancels routes, moves and locks points and clears signals |

## Software development

| Term | Meaning |
| --- | --- |
| .NET / C# | Microsoft's platform and programming language used for the engine, CLI and API |
| Class library | A project of reusable code with no user interface (e.g. `SignalSafe.Core`) |
| Solution (`.sln`) | A file that groups the .NET projects so they build together |
| NuGet | The package manager for .NET libraries (e.g. ClosedXML) |
| Depth-first search | A way of exploring a graph by following one path as far as it goes before trying the next; used to find routes |
| Unit test | A small automated test of one piece of code (xUnit for C#, Vitest for TypeScript) |
| Integration test | A test of several parts working together (e.g. the API tests run the real server in memory) |
| Code coverage | The share of code lines run by the tests |
| Linter | A tool that checks code for likely mistakes and style problems (oxlint, `dotnet format`) |
| Git, commit, push, pull | Git tracks changes. A commit is a saved set of changes; push uploads commits to GitHub; pull downloads them |
| Repository (repo) | A project folder tracked by Git, here hosted on GitHub |
| Branch / main | A line of development; `main` is the one that gets deployed |

## Web

| Term | Meaning |
| --- | --- |
| API | A set of addresses a program can call to ask for something, e.g. "check these files" |
| Endpoint | One API address, e.g. `POST /api/v1/check` |
| HTTP, GET, POST | The protocol of the web. GET asks for something; POST sends something |
| JSON | A text format for data, used by the API and the layout files |
| ASP.NET Core | Microsoft's framework for web servers and APIs in C# |
| OpenAPI / Swagger | A standard description of an API, and a web page to read and try it (`/swagger`) |
| React | A JavaScript library for building interactive pages from components |
| TypeScript | JavaScript with types, which catches mistakes before the code runs |
| Vite | The tool that runs the web app in development and builds it for production |
| npm / Node.js | Node.js runs JavaScript outside the browser; npm installs JavaScript packages |
| SVG | A vector drawing format for the web; used for the track diagram |
| Proxy | Something that forwards requests. Vite and nginx forward `/api` calls to the API |
| CORS | A browser rule: a page may only call an API on another address if the API allows it |
| localhost | Your own computer |
| nginx | A small, fast web server; serves the web app in the Docker setup |

## DevOps and cloud

| Term | Meaning |
| --- | --- |
| CI (continuous integration) | Every change is automatically built and tested |
| CD (continuous deployment) | Every change that passes is automatically released |
| GitHub Actions | GitHub's automation service, which runs the CI and Deploy workflows |
| Workflow / job / step | A workflow (a `.yml` file) contains jobs; each job runs steps on a fresh machine |
| Secret / variable (GitHub) | A secret is an encrypted, hidden value (passwords, tokens); a variable is a visible setting |
| Docker | A tool for building and running containers |
| Image / container | An image is a packaged program and everything it needs; a container is a running copy of it |
| Dockerfile | The recipe for building an image |
| Multi-stage build | A Dockerfile that builds in one image and copies only the result into a smaller one |
| docker compose | Runs several containers together from `docker-compose.yml` |
| Container registry | A store for images; we use GitHub Container Registry (`ghcr.io`) |
| Image tag | A label on an image version; we use the commit ID and `latest` |
| Azure | Microsoft's cloud platform |
| Subscription | An Azure container for resources, linked to how they are paid for |
| Resource group | A folder for related Azure resources (`signalsafe-rg`) |
| Region | A location of Azure data centres (e.g. UK South) |
| Azure Container Apps | Runs containers without managing servers, and can scale to zero |
| Revision | A version of a container app; each change of image or settings creates one |
| Replica | A running copy of a container app; ours runs 0 or 1 |
| Scale to zero | Stopping all copies when there are no requests, so it costs nothing; the next request starts it again |
| Ingress | Container Apps' public HTTPS front door for an app |
| Azure Static Web Apps | Hosts web pages and their files on a global network |
| Cloud Shell | A terminal in the Azure portal, already signed in |
| Azure CLI (`az`) | The command-line tool for managing Azure |
| Microsoft Entra ID | Azure's identity service (formerly Azure Active Directory) |
| Service principal | A non-human identity (for GitHub Actions) with only the permissions it needs |
| Budget alert | An email when spending reaches a set amount; it does not stop charges |
| Pay-as-you-go | The normal Azure plan: pay only for use above the free amounts |

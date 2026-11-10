# SignalSafe documentation

Start here to find the right guide.

| If you want to... | Read |
| --- | --- |
| Understand the whole project and why each step was taken | [The SignalSafe journey](PROJECT-JOURNEY.md) |
| See how the parts fit together, and which file does what | [How SignalSafe works (architecture)](ARCHITECTURE.md) |
| Check exactly what each rule and layout check does | [The checks: rules R01–R10 and layout checks L01–L13](RULES.md) |
| Run it and test it yourself, step by step | [Testing guide](../TESTING.md) |
| Make a change and get it onto the live site | [Development workflow](WORKFLOW.md) |
| Understand Azure, the deployment pipeline, costs and maintenance | [Deployment](DEPLOYMENT.md) |
| Look up a term (railway, software or cloud) | [Glossary](GLOSSARY.md) |
| Work on the React web app | [Web app README](../frontend/README.md) |

## Suggested order for a first read

1. [README](../README.md): what SignalSafe is (5 minutes).
2. [The SignalSafe journey](PROJECT-JOURNEY.md): how it was built and why (15 minutes).
3. [How SignalSafe works](ARCHITECTURE.md), sections 1–5: the big picture and the engine (20 minutes).
4. [The checks](RULES.md): what each rule does, with examples (20 minutes).
5. [Testing guide](../TESTING.md): run it on your Mac (1 hour).
6. [Development workflow](WORKFLOW.md): how changes reach the live site (15 minutes).
7. [Deployment](DEPLOYMENT.md), Part 1: how Azure runs it (20 minutes).
8. [How SignalSafe works](ARCHITECTURE.md), section 8: the code reading order (about 4 hours, spread over a few days).

Keep the [Glossary](GLOSSARY.md) open alongside.

## Keeping the documentation up to date

When you change the project, update the matching guide in the same commit:

| Change | Update |
| --- | --- |
| New or changed rule | RULES (details), README (rules table), PROJECT-JOURNEY if it is a milestone |
| New file or project | ARCHITECTURE (file map, reading order) |
| New command or test | TESTING, WORKFLOW |
| Azure or pipeline change | DEPLOYMENT, WORKFLOW |
| New term | GLOSSARY |

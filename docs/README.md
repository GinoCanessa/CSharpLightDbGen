# CSharpLightDbGen Documentation

This folder contains the developer onboarding and maintenance documentation for `CSharpLightDbGen`.

## Document Map

- [Onboarding Guide](./onboarding.md)
- [Architecture Overview](./architecture.md)
- [Process Flows](./process-flows.md)
- [Modules and Libraries](./modules-and-libraries.md)
- [Generated API Contract](./api-contracts.md)
- [Commands and Options](./commands.md)
- [Dependencies](./dependencies.md)

## Audience

This documentation is written for developers who need to:

- understand how the source generator works,
- modify or extend generation behavior,
- validate changes with unit/integration/performance suites,
- and onboard quickly without reverse-engineering the codebase.

## Repository At-a-Glance

- Solution: `db-gen.slnx`
- Generator project: `src/cslightdbgen.sqlitegen`
- Unit tests: `tests/cslightdbgen.sqlitegen.tests`
- Integration tests: `tests/cslightdbgen.sqlitegen.integration`
- Benchmarks: `tests/cslightdbgen.performance`
- Benchmark outputs: `BenchmarkDotNet.Artifacts/results`

## Recommended Read Order

1. [Onboarding Guide](./onboarding.md)
2. [Architecture Overview](./architecture.md)
3. [Process Flows](./process-flows.md)
4. [Generated API Contract](./api-contracts.md)
5. [Commands and Options](./commands.md)
6. [Dependencies](./dependencies.md)

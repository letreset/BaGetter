@AGENTS.md

## Code navigation: CodeGraph

This repo is indexed by CodeGraph (`.codegraph/`, local to each machine; only `.codegraph/.gitignore` is committed). Reach for it **before** grep/glob or reading whole files when you need to find or understand code:

- **MCP (preferred):** `codegraph_explore` answers most questions in one call. It returns the relevant symbols' current, line-numbered source plus the call paths between them, including DI and dynamic-dispatch hops that grep can't follow. Name the symbols or files in the query, e.g. `"FeedResolutionMiddleware IFeedContext CurrentFeed"`. Related tools: `codegraph_callers` (who calls X), `codegraph_impact` (what a change to X affects) and `codegraph_files`. If the tools are deferred, load them via tool search.
- **CLI fallback:** `codegraph explore "<symbols or question>"` prints the same output.
- The index can lag behind uncommitted edits. Re-read a file before editing it, and fall back to grep for string literals, config keys, `.cshtml`, JSON and YAML.

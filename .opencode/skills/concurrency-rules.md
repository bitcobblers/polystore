---
name: Concurrency Rules
description: Establish rules for concurrency.
---

## Concurrency Rules

When invoking subagents it is permissible to delegate to more than one subagent
if and only if:

1. No subagent requires the output of another concurrent subagent.
2. The invoking agent's behavior does not depend on the order in which the subagents complete.
3. The subagents do not perform conflicting mutations of shared state.

When these conditions hold, prefer concurrent invocation over sequential
invocation.

### Use Concurrent Execution

- Multiple reviewers independently evaluating the same completed artifact.
- Independent research tasks whose results will later be combined.
- Independent validation tasks against an immutable implementation.

### Use Sequential Execution

- Writing tests for code that is still being written.
- Reviewing output that has not yet been produced.
- Two subagents modifying overlapping files.
- A task whose inputs depend on decisions made by another subagent.

## Execution Caveat

Concurrent delegation expresses that the tasks are independent and may execute
in parallel. Actual execution may be serialized by inference-capacity or
runtime constraints.

Do not treat execution time alone as evidence that a subagent has stalled.

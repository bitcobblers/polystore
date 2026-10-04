---
description: Implement an approved PolyStore design
mode: subagent
permissions:
  - action: subagent
    resource: "*"
    effect: deny
  - action: subagent
    resource: "code/*"
    effect: allow
  - action: subagent
    resource: "review/*"
    effect: allow
  - action: skill
    resource: 'concurrency.md'
    effect: allow
---

<|think_low|>

# Feature Implementation Workflow

You orchestrate implementation of an approved PolyStore design.

The invoking command will provide:

- the exact path to the approved design document
- the feature branch name

Treat these supplied values as authoritative for this workflow.

Before delegating implementation:

1. Read the approved design completely.
2. Read `ARCHITECTURE.md` completely.
3. Read `AGENTS.md` completely.
4. Follow the `concurrency-rules` skill when delegating work.

The approved design is the feature specification.

Use only the following subagents for implementation and review:

1. `code/implementer`
2. `code/test-adversary`
3. `review/code-reviewer`

### Initial implementation

Invoke `code/implementer` with:
- the approved design path
- the feature branch
- instructions to implement the design and its normal test suite

### Adversarial validation

Invoke `code/test-adversary` with:
- the approved design path
- the feature branch

If adversarial tests expose an implementation defect relative to the approved
design, send the failures to `code/implementer` for correction.

If `code/implementer` disputes that an adversarial failure represents
a requirement of the approved design, do not instruct it to change production
behavior merely to satisfy the test. Treat the disagreement according to the
Escalation rules.

### Review Cycle

When `code/test-adversary` reports that all required tests pass, invoke `review/code-reviewer`.

If the reviewer returns `CHANGES_REQUESTED`, send its findings to
`code/implementer`.

After changes:
1. invoke `code/test-adversary` again;
2. invoke `review/code-reviewer` again.

A review cycle ends when `review/code-reviewer` returns a review result.

Stop when:
- `code/test-adversary` reports that all required tests pass; and
- `review/code-reviewer` returns `APPROVED`.

Allow at most 10 review cycles.

## Escalation

Stop and request human review rather than inventing behavior when:

- the approved design conflicts with ARCHITECTURE.md;
- implementation requires resolving a question explicitly deferred by the
  design;
- a reviewer requests an architectural change rather than correction of an
  implementation defect;
- the implementer and adversary disagree about behavior the specification
  does not clearly determine;
- review findings oscillate rather than converge;
- the tenth cycle completes without approval.

Do not modify the approved design during feature development.

At completion, report:
- implementation summary;
- tests and adversarial scenarios added;
- review cycles used;
- remaining warnings or deferred issues;
- final reviewer status.

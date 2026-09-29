---
description: Implement an approved PolyStore design
agent: plan
---

Implement the approved design in `$2`.

Feature branch: `feat/$1`

The approved design is the feature specification. Read it in full before
delegating implementation.

ARCHITECTURE.md defines repository-wide architectural constraints.
AGENTS.md defines repository-wide development rules. The approved design
defines the requirements, decisions, invariants, scope, non-goals, and
deferred questions for this feature.

Do not reconstruct the feature from ARCHITECTURE.md when the approved
design already makes a decision.

Do not silently resolve contradictions between the approved design and
ARCHITECTURE.md. If they cannot be reconciled, stop and report the conflict
for human review.

## Workflow

Use the following three subagents:

1. `feature-implementer`
2. `feature-adversary`
3. `reviewer`

Run them sequentially.

### Initial implementation

Invoke `feature-implementer` with:
- the approved design path
- the feature branch
- instructions to implement the design and its normal test suite

The implementer owns production code and ordinary implementation tests.

After it completes, verify that the repository builds and the normal test
suite passes.

### Adversarial validation

Invoke `feature-adversary` with:
- the approved design path
- the feature branch

The adversary must independently derive edge cases and failure scenarios
from the approved design and current implementation.

It may add or modify test and benchmark code, but must not modify production
code.

After it completes, run the resulting tests.

If adversarial tests fail, invoke `feature-implementer` with the failures.
The implementer must fix production code when the implementation violates
the approved design.

If the implementer believes a failing adversarial test asserts behavior not
required by the approved design, it must not change production behavior
merely to satisfy the test. It should explain the disagreement.

Invoke `feature-adversary` again after substantive implementation changes.

### Final review

When all tests pass, invoke `reviewer`.

The reviewer must evaluate the complete feature branch against its base
branch and the approved design.

If the reviewer returns `CHANGES_REQUESTED`, send its findings to
`feature-implementer`.

After changes:
1. run normal tests;
2. invoke `feature-adversary` again if behavior changed;
3. run all tests;
4. invoke `reviewer` again.

A review cycle consists of implementation/fixes, adversarial validation,
tests, and final review.

Allow at most 10 review cycles.

Stop earlier when the reviewer returns `APPROVED` and all tests pass.

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

---
name: code-reviewer
description: Shared review protocol for PolyStore feature implementations.
---

# Code Review Protocol

## Context

Before reviewing:

1. Read the approved design in full.
2. Read `ARCHITECTURE.md` and `AGENTS.md`.
3. Inspect the complete feature diff (feature branch against base branch).
4. Inspect the relevant production source, ordinary tests, adversarial
   tests, and benchmarks.

Established architecture takes precedence over the implementation.
An intentional architectural change must be identified and justified.
Report unresolved conflicts rather than silently choosing an
interpretation.

## Review Rules

- Review only within the invoking agent's assigned domain.
- Findings must identify code in the feature diff or behavior the diff
  introduces; do not report pre-existing defects in unchanged code unless
  the change exposes or relies on them.
- A reviewer may build affected projects and run tests to verify suspected
  defects but must not modify source, tests, the approved design, or the
  branch.
- Distinguish defects introduced or amplified by the change from existing
  risks.
- Do not report stylistic preferences.

## Severity

- **Blocking:** The implementation violates a requirement or invariant of
  the approved design, or a defect prevents correct behavior of the
  specified feature.
- **Major:** A material defect that must be corrected before approval.
- **Minor:** A localized improvement that does not prevent correct
  behavior.

All findings require CHANGES_REQUESTED.

## Findings

For each finding provide:

- ID
- Severity
- File and line
- Basis — the design requirement, invariant, or repository constraint
  violated
- Concrete defect description or failure scenario
- Consequence
- Resolution criterion

Report each distinct root cause once.

Combine related symptoms.

A suspected defect that cannot be verified is reported as unverified, with
the evidence available, rather than asserted as established — suspicion is a
category, not a verdict.

## Re-review

When re-reviewing a changed implementation:

- Preserve finding IDs and mark each prior finding resolved or unresolved.
- Check changed areas for new issues within the assigned domain.
- Do not repeat resolved findings in full.
- Do not assume that addressing a finding preserved other guarantees.

## Output

Return:

1. Review Summary — maximum 100 words.
2. Findings — grouped by severity.
3. Required Changes — finding IDs and concise resolution criteria,
   only when requesting changes.
4. Final Disposition.

Omit empty categories.

Do not restate the implementation.

End with exactly APPROVED or CHANGES_REQUESTED.

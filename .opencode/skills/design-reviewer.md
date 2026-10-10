
---
name: design-review
description: Shared review protocol for PolyStore design proposals.
---

# Design Review Protocol

## Context

Before reviewing:

1. Read `ARCHITECTURE.md` and `AGENTS.md`.
2. Read the complete proposal.
3. Inspect relevant source code to verify material claims.

Established architecture takes precedence over proposal assumptions.

## Review Rules

- Review only within the invoking agent's assigned domain.
- Report plausible failures, not speculative weaknesses.
- Identify the affected construct and violated invariant.
- Distinguish existing risks from risks introduced by the proposal.
- Do not prescribe implementation mechanisms when a behavioral
  guarantee is sufficient.
- Do not modify proposals or source code.
- Ignore the proposal's Executive Summary.

## Severity

- **Blocking:** Fundamental design flaw that prevents safe
  implementation.
- **Major:** Material design decision or guarantee must be resolved.
- **Minor:** Localized improvement or clarification.

Blocking or Major findings require CHANGES_REQUESTED.

## Output

Return:

1. Review Summary — maximum 100 words.
2. Findings — grouped by severity.
3. Missing Considerations — material omissions only.
4. Final Disposition.

For each finding provide:

- Location
- Failure scenario
- Consequence
- Required resolution

End with exactly APPROVED or CHANGES_REQUESTED.

For CHANGES_REQUESTED, include a concise required-changes checklist.

Do not include empty finding categories.
Do not repeat findings across sections.

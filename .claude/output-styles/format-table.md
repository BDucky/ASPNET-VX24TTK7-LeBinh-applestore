---
name: Table Based
description: Markdown tables for better organization and scanning
---

Structure your responses using markdown tables wherever appropriate to improve clarity and organization. Key guidelines:

## Table Usage Patterns

| Pattern | When to Use | Example |
|---------|-------------|---------|
| **Comparison Tables** | When contrasting options, tools, or approaches | Features vs benefits, tool comparisons |
| **Step Tables** | For multi-step processes with details | Step number, action, description, notes |
| **Information Tables** | To organize related data points | Configuration options, parameters, results |
| **Analysis Tables** | When breaking down findings or issues | Issue, severity, solution, priority |

## Table Formatting Standards

- Use clear, descriptive headers
- Keep cell content concise but informative
- Include relevant details in additional columns (e.g., notes, links, status)
- Use formatting within cells when helpful (bold for emphasis, code for technical terms)
- Align content logically (left for text, center for status, right for numbers)

## Response Structure

| Section | Format | Purpose |
|---------|--------|---------|
| **Summary** | Brief paragraph + summary table | Quick overview of key points |
| **Details** | Structured tables by category | Organized information presentation |
| **Actions** | Step table with priorities | Clear next steps with context |

## Code and Technical Content

When presenting code-related information, use tables to organize:

| Category | Example Columns |
|----------|-----------------|
| File changes | `apps/designer/...`, action, description |
| Component props | prop name, type, required, description |
| Store actions | action, parameters, return type |
| GraphQL operations | operation name, variables, response type |
| Test results | test suite, status, duration |

## Project-Specific Examples

| App/Package | Purpose | Key Files |
|-------------|---------|-----------|
| `apps/designer` | Design editor app | `src/views/`, `src/components/` |
| `apps/enduser` | End user application | `src/pages/`, `src/stores/` |
| `packages/core` | Shared utilities | `src/utils/`, `src/types/` |
| `packages/editor` | Editor components | `src/canvas/`, `src/tools/` |
| `packages/plugins` | GraphQL & integrations | `graphql/operations/` |

Always prioritize readability and scannability. Use tables to reduce cognitive load and make information easier to digest at a glance.
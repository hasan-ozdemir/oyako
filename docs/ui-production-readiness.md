# Oyako UI Production Readiness

This document records the production readiness criteria used for the Oyako web UI hardening pass.

## Reference standards and product benchmarks

- WCAG 2.2 is the accessibility baseline for contrast, keyboard operation, focus visibility, reflow, and target sizing.
- WCAG 2.2 target-size guidance is treated as the minimum pointer target rule for visible controls.
- Material adaptive layout guidance is used for responsive behavior across mobile, tablet, and desktop viewports.
- Fluent 2 accessibility guidance is used for focus management, especially restoring focus after dialogs and menus close.
- Apple Human Interface Guidelines are used as a cross-check for adaptive layout, readability, and ergonomic controls.
- ChatGPT, Gemini, and Copilot-style assistant interfaces are used as product benchmarks for a simple composer, readable answer flow, clear assistant state, visible suggestions, and transparent source access.

## Production UI checklist

- Main shell, header, workspace, status bar, menus, dialogs, tables, textareas, and progress views must not create page-level horizontal overflow.
- Content that exceeds its allocated area must scroll in the local container rather than becoming clipped or unreachable.
- Visible buttons, menu items, selects, textareas, and checkboxes must provide at least a 24-by-24 CSS pixel pointer target.
- Dialogs and menus must be keyboard reachable, close with Escape, and restore focus to the trigger when dismissed.
- Main screen, knowledge bank, settings, help, and document editor surfaces must pass axe checks for `critical` and `serious` impacts.
- The status bar must always show a contextual status label with a contextual icon.

## Tested viewport matrix

- `360x740`
- `390x844`
- `768x1024`
- `1024x768`
- `1366x768`
- `1440x900`
- `1920x1080`

## Release gate commands

- `npm run build`
- `npm run lint`
- `npx playwright test`
- `dotnet test .\webapi-oyako\webapi-oyako.Tests\webapi-oyako.Tests.csproj`

## Current production-readiness status

- The UI is considered production-ready only when all release gate commands pass.
- The Playwright suite covers responsive layout, scroll safety, long answers, document editing, knowledge bank actions, settings, help, user menus, utility menus, offline status, keyboard shortcuts, Escape behavior, focus restoration, and serious-level accessibility checks.


## Release 2026.6.18.300 UI Gate

The UI remains shippable only when modal focus behavior, keyboard access, responsive scroll safety, source/document management workflows, status bar messages, and Q&A flows pass the local and deployed smoke checks. Public Azure validation must include a real Web App load and a real streamed Q&A request.

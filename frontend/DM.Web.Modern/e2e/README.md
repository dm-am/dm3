# Playwright E2E Tests

This directory contains end-to-end tests for the DM3 application using Playwright.

## Setup

Install Playwright browsers:

```bash
npx playwright install
```

## Running Tests

```bash
# Run all tests
npm run test:e2e

# Run tests with UI mode (interactive)
npm run test:e2e:ui

# Run tests in debug mode
npm run test:e2e:debug

# Run specific test file
npx playwright test e2e/tests/auth/login.spec.ts

# Run tests in a specific browser
npx playwright test --project=chromium
npx playwright test --project=firefox
```

## Test Structure

```
e2e/
├── fixtures/
│   └── auth.ts                    # Authentication fixture for authenticated tests
└── tests/
    ├── auth/
    │   ├── login.spec.ts          # Login flow tests
    │   ├── registration.spec.ts   # Registration flow tests
    │   └── password-reset.spec.ts # Password reset tests
    ├── community/
    │   ├── profiles.spec.ts       # User profile tests
    │   ├── polls.spec.ts          # Poll voting tests
    │   └── forum-index.spec.ts    # Forum index page tests
    ├── forum/
    │   ├── topics.spec.ts         # Forum topic tests
    │   ├── comments.spec.ts       # Comment tests
    │   └── likes.spec.ts          # Like/unlike tests
    ├── gaming/
    │   ├── browse-games.spec.ts   # Game browsing tests
    │   ├── subscribe.spec.ts      # Game subscription tests
    │   └── characters.spec.ts     # Character display tests
    └── messaging/
        ├── direct-messages.spec.ts    # Direct messaging tests
        ├── edit-delete.spec.ts        # Message editing tests
        └── conversation-list.spec.ts  # Conversation list tests
```

## Authentication Fixture

The `auth.ts` fixture provides an `authenticatedPage` that automatically logs in a test user before running tests. Use it for tests that require authentication:

```typescript
import { test, expect } from '../fixtures/auth';

test('should do something as authenticated user', async ({ authenticatedPage }) => {
  await authenticatedPage.goto('/some-page');
  // ... test code
});
```

## Environment Variables

- `E2E_BASE_URL`: Base URL for the application (default: http://localhost:5173)
- `VITE_API_URL`: API URL for authentication (default: http://localhost:5051)
- `CI`: Set to enable CI mode (more retries, single worker)

## Test Reports

After running tests, view the HTML report:

```bash
npx playwright show-report
```

## Writing Tests

1. Follow the existing test structure
2. Use data-testid attributes for reliable element selection
3. Use the authentication fixture for tests requiring login
4. Write descriptive test names using "should" convention
5. Keep tests independent and idempotent

## Configuration

Test configuration is in `playwright.config.ts` at the project root. Key settings:

- **Test directory**: `./e2e/tests`
- **Browsers**: Chromium and Firefox
- **Retries**: 2 in CI, 0 locally
- **Workers**: 1 in CI, CPU count locally
- **Web server**: Automatically starts dev server on port 5173

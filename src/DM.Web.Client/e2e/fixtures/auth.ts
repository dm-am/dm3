import { test as base, Page, APIRequestContext } from "@playwright/test";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

// Test credentials - loaded from environment variables for security
// Set these in your .env.local or CI/CD environment:
// E2E_TEST_USERNAME, E2E_TEST_PASSWORD
const TEST_USER = {
  username: process.env.E2E_TEST_USERNAME || "Alice",
  password: process.env.E2E_TEST_PASSWORD || "",
};

// Validate credentials are set
if (!TEST_USER.password) {
  console.warn(
    "Warning: E2E_TEST_PASSWORD environment variable not set. " +
      "E2E tests requiring authentication will fail.",
  );
}

/**
 * Helper function to login via cookie-based API
 * Returns a new request context with the session cookie
 */
export async function loginWithCookies(
  request: APIRequestContext,
  username: string,
  password: string,
): Promise<APIRequestContext> {
  // Create a new context to isolate cookies
  const context = await request.newContext();

  const response = await context.post(`${API_URL}/v1/account/login`, {
    headers: {
      "Content-Type": "application/json",
    },
    data: {
      email: username,
      password: password,
    },
  });

  if (!response.ok()) {
    const error = await response.text();
    throw new Error(
      `Auth failed for ${username}: ${response.status()} - ${error}`,
    );
  }

  // Cookie is automatically stored in the context
  return context;
}

export const test = base.extend<{ authenticatedPage: Page }>({
  authenticatedPage: async ({ page }, use) => {
    // Login via cookie-based API
    const response = await page.request.post(`${API_URL}/v1/account/login`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        email: TEST_USER.username,
        password: TEST_USER.password,
      },
    });

    if (!response.ok()) {
      const error = await response.text();
      throw new Error(`Auth failed: ${response.status()} - ${error}`);
    }

    // Cookie is automatically stored and will be sent with subsequent requests
    await use(page);
  },
});

export { expect } from "@playwright/test";

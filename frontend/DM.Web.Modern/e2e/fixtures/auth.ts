import { test as base, Page } from '@playwright/test';

const API_URL = process.env.VITE_API_URL || 'http://localhost:5051';

// Test credentials - should match database seed data
const TEST_USER = {
  username: 'Alice',
  password: 'Test123!',
};

export const test = base.extend<{ authenticatedPage: Page }>({
  authenticatedPage: async ({ page }, use) => {
    // Login via API using OpenIddict password flow
    const response = await page.request.post(`${API_URL}/connect/token`, {
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      form: {
        grant_type: 'password',
        username: TEST_USER.username,
        password: TEST_USER.password,
        client_id: 'dm3-web',
        scope: 'openid profile offline_access',
      },
    });

    if (!response.ok()) {
      throw new Error(`Auth failed: ${response.status()}`);
    }

    const { access_token, refresh_token } = await response.json();

    // Store tokens using the same keys as the frontend API
    await page.addInitScript(({ accessToken, refreshToken }) => {
      localStorage.setItem('dm-access-token', accessToken);
      localStorage.setItem('dm-refresh-token', refreshToken);
    }, { accessToken: access_token, refreshToken: refresh_token });

    await use(page);
  },
});

export { expect } from '@playwright/test';

import { test, expect, APIRequestContext } from '@playwright/test';
import { loginWithCookies } from '../../fixtures/auth';

const API_URL = process.env.VITE_API_URL || 'http://localhost:5000';

const TEST_USER = {
  username: 'Alice',
  password: 'Xk9#mQz2$vL7nW',
};

let authContext: APIRequestContext;

test.beforeAll(async ({ request }) => {
  try {
    authContext = await loginWithCookies(request, TEST_USER.username, TEST_USER.password);
  } catch (e) {
    console.error('Failed to login:', e);
  }
});

test.afterAll(async () => {
  if (authContext) await authContext.dispose();
});

test.describe('Blacklist API', () => {
  test('should get my blacklist', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/blacklist`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('resources');
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test('should get blacklist settings', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/blacklist/settings`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('hideComments');
    expect(data).toHaveProperty('hideMessages');
    expect(data).toHaveProperty('blockDirectMessages');
    expect(data).toHaveProperty('autoPopulateContentBlacklist');
  });

  test('should update blacklist settings', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.patch(`${API_URL}/v1/users/me/blacklist/settings`, {
      headers: { 'Content-Type': 'application/json' },
      data: {
        hideComments: true,
        blockDirectMessages: false,
      },
    });

    expect(response.ok()).toBeTruthy();
  });

  test('should require authentication', async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/blacklist`);
    expect(response.status()).toBe(401);
  });
});

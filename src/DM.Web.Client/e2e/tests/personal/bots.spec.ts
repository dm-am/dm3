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

test.describe('Notification Bots API', () => {
  test('should get telegram link code', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.post(`${API_URL}/v1/users/me/notifications/bots/telegram`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('code');
    expect(data).toHaveProperty('expiresUtc');
    expect(data.code).toHaveLength(6);
  });

  test('should get discord link code', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.post(`${API_URL}/v1/users/me/notifications/bots/discord`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('code');
    expect(data.code).toHaveLength(6);
  });

  test('should reject invalid bot type', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.post(`${API_URL}/v1/users/me/notifications/bots/invalid`);

    expect(response.status()).toBe(400);
  });

  test('should require authentication', async ({ request }) => {
    const response = await request.post(`${API_URL}/v1/users/me/notifications/bots/telegram`);
    expect(response.status()).toBe(401);
  });
});

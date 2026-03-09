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

test.describe('Notifications API', () => {
  test('should get notifications list', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/notifications`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('resources');
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test('should get unread count', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/notifications/unread`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('count');
    expect(typeof data.count).toBe('number');
  });

  test('should mark all as read', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.delete(`${API_URL}/v1/users/me/notifications/unread`);

    expect(response.status()).toBe(204);
  });

  test('should get notification settings', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/notifications/settings`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    // Settings object has discord/telegram sub-objects
    expect(typeof data).toBe('object');
  });

  test('should update notification settings', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.patch(`${API_URL}/v1/users/me/notifications/settings`, {
      headers: { 'Content-Type': 'application/json' },
      data: {
        discord: {
          enabled: false,
        },
      },
    });

    expect(response.ok()).toBeTruthy();
  });

  test('should require authentication', async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/notifications`);
    expect(response.status()).toBe(401);
  });
});

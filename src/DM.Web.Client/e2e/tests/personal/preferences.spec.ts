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

test.describe('Preferences API', () => {
  test('should get my preferences', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.get(`${API_URL}/v1/users/me/preferences`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty('paging');
  });

  test('should update paging preferences', async () => {
    test.skip(!authContext, 'Auth failed');

    const response = await authContext.patch(`${API_URL}/v1/users/me/preferences`, {
      headers: { 'Content-Type': 'application/json' },
      data: {
        paging: {
          postsPerPage: 25,
          commentsPerPage: 20,
          topicsPerPage: 30,
          messagesPerPage: 20,
        },
      },
    });

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data.paging).toHaveProperty('postsPerPage', 25);
  });

  test('should require authentication', async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/users/me/preferences`);
    expect(response.status()).toBe(401);
  });
});

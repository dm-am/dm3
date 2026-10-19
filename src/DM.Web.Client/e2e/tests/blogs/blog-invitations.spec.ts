import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  API_BASE_URL,
  authenticatedContext,
  secondaryUser,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";

const INVITED_USER = secondaryUser;

let ownerContext: APIRequestContext;
let invitedContext: APIRequestContext;
let testBlogId: string;

test.beforeAll(async () => {
  ownerContext = await authenticatedContext(PRIMARY_STORAGE_STATE);
  invitedContext = await authenticatedContext(SECONDARY_STORAGE_STATE);

  const createResponse = await ownerContext.post(`${API_BASE_URL}/v1/blogs`, {
    data: {
      title: "Invitation Test Blog",
      isPublic: false,
    },
  });

  expect(createResponse.status()).toBe(201);
  const { resource } = await createResponse.json();
  testBlogId = resource.id;
});

test.afterAll(async () => {
  if (testBlogId && ownerContext) {
    await ownerContext.delete(`${API_BASE_URL}/v1/blogs/${testBlogId}`);
  }

  if (ownerContext) {
    await ownerContext.dispose();
  }
  if (invitedContext) {
    await invitedContext.dispose();
  }
});

test.describe("Blog Invitations API", () => {
  test("should create and get pending invitations", async () => {
    // The route is plural: `assistants` and `readers`.
    const createResponse = await ownerContext.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations/assistants`,
      {
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    // An invitation is returned bare, and its identifier is `id`.
    const invitation = await createResponse.json();
    expect(invitation).toHaveProperty("id");

    const listResponse = await ownerContext.get(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations`,
    );

    expect(listResponse.ok()).toBeTruthy();
    const list = await listResponse.json();
    expect(list.resources.length).toBeGreaterThan(0);

    // Cancelling is an owner action inside the blog's own collection.
    const cancelResponse = await ownerContext.delete(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations/${invitation.id}`,
    );

    expect(cancelResponse.status()).toBe(204);
  });

  test("should get user pending invitations", async () => {
    const createResponse = await ownerContext.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations/readers`,
      {
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    // A recipient handles their own invitations through the Personal API.
    const myInvitationsResponse = await invitedContext.get(
      `${API_BASE_URL}/v1/users/me/invitations`,
    );

    expect(myInvitationsResponse.ok()).toBeTruthy();
    const myInvitations = await myInvitationsResponse.json();
    expect(myInvitations.resources.length).toBeGreaterThan(0);

    const rejectResponse = await invitedContext.post(
      `${API_BASE_URL}/v1/users/me/invitations/${invitation.id}/reject`,
    );

    expect(rejectResponse.status()).toBe(204);
  });

  test("should accept invitation and become participant", async () => {
    const createResponse = await ownerContext.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations/readers`,
      {
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    const acceptResponse = await invitedContext.post(
      `${API_BASE_URL}/v1/users/me/invitations/${invitation.id}/accept`,
    );

    expect(acceptResponse.status()).toBe(204);

    // Verify invited user can now access the draft blog
    const getBlogResponse = await invitedContext.get(
      `${API_BASE_URL}/v1/blogs/${testBlogId}`,
    );

    expect(getBlogResponse.ok()).toBeTruthy();
  });

  test("should require authentication for invitations", async ({ request }) => {
    const response = await request.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/invitations/assistants`,
      {
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(response.status()).toBe(401);
  });
});

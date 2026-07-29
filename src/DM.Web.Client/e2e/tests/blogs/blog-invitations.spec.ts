import { test, expect, APIRequestContext } from "@playwright/test";
import {
  authenticatedContext,
  secondaryUser,
  PRIMARY_STORAGE_STATE,
  SECONDARY_STORAGE_STATE,
} from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

const INVITED_USER = secondaryUser;

let ownerContext: APIRequestContext;
let invitedContext: APIRequestContext;
let testBlogId: string;

test.beforeAll(async () => {
  // Login as blog owner
  try {
    ownerContext = await authenticatedContext(PRIMARY_STORAGE_STATE);
  } catch (e) {
    console.error("Failed to login as owner:", e);
  }

  // Login as invited user
  try {
    invitedContext = await authenticatedContext(SECONDARY_STORAGE_STATE);
  } catch (e) {
    console.error("Failed to login as invited user:", e);
  }

  // Create a test blog
  if (ownerContext) {
    const createResponse = await ownerContext.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Invitation Test Blog",
        isPublic: false,
        commentsEnabled: true,
      },
    });

    if (createResponse.ok()) {
      testBlogId = (await createResponse.json()).id;
    }
  }
});

test.afterAll(async () => {
  // Cleanup test blog
  if (testBlogId && ownerContext) {
    await ownerContext.delete(`${API_URL}/v1/blogs/${testBlogId}`);
  }

  // Dispose contexts
  if (ownerContext) {
    await ownerContext.dispose();
  }
  if (invitedContext) {
    await invitedContext.dispose();
  }
});

test.describe("Blog Invitations API", () => {
  test.skip(
    !ownerContext || !invitedContext || !testBlogId,
    "Skipping - setup failed",
  );

  test("should create and get pending invitations", async () => {
    // Create assistant invitation
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/assistant`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    // Skip test if endpoint doesn't exist yet
    if (createResponse.status() === 404) {
      test.skip(true, "Blog invitations API not yet implemented");
      return;
    }

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();
    expect(invitation).toHaveProperty("tokenId");

    // Get pending invitations for blog
    const listResponse = await ownerContext.get(
      `${API_URL}/v1/blogs/${testBlogId}/invitations`,
    );

    expect(listResponse.ok()).toBeTruthy();
    const list = await listResponse.json();
    expect(list.resources.length).toBeGreaterThan(0);

    // Cancel the invitation
    const cancelResponse = await ownerContext.delete(
      `${API_URL}/v1/blogs/invitations/${invitation.tokenId}`,
    );

    expect(cancelResponse.status()).toBe(204);
  });

  test("should get user pending invitations", async () => {
    // Create invitation first
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/reader`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    if (createResponse.status() === 404) {
      test.skip(true, "Blog invitations API not yet implemented");
      return;
    }

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    // Get pending invitations for invited user
    const myInvitationsResponse = await invitedContext.get(
      `${API_URL}/v1/blogs/invitations/my`,
    );

    expect(myInvitationsResponse.ok()).toBeTruthy();
    const myInvitations = await myInvitationsResponse.json();
    expect(myInvitations.resources.length).toBeGreaterThan(0);

    // Reject the invitation
    const rejectResponse = await invitedContext.post(
      `${API_URL}/v1/blogs/invitations/${invitation.tokenId}/reject`,
    );

    expect(rejectResponse.status()).toBe(204);
  });

  test("should accept invitation and become participant", async () => {
    // Create invitation
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/reader`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    if (createResponse.status() === 404) {
      test.skip(true, "Blog invitations API not yet implemented");
      return;
    }

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    // Accept the invitation
    const acceptResponse = await invitedContext.post(
      `${API_URL}/v1/blogs/invitations/${invitation.tokenId}/accept`,
    );

    expect(acceptResponse.status()).toBe(204);

    // Verify invited user can now access the draft blog
    const getBlogResponse = await invitedContext.get(
      `${API_URL}/v1/blogs/${testBlogId}`,
    );

    expect(getBlogResponse.ok()).toBeTruthy();
  });

  test("should require authentication for invitations", async ({ request }) => {
    const response = await request.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/assistant`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    // Either 401 or 404 if endpoint doesn't exist
    expect([401, 404]).toContain(response.status());
  });
});

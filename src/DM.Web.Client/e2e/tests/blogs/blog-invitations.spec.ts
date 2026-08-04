import { test, expect, type APIRequestContext } from "@playwright/test";
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

/**
 * Setup is allowed to fail the run, and nothing here may turn a failure into a
 * pass. What stood here before did both: the two logins were wrapped in
 * `try/catch` that only logged, the blog id was read from `.id` instead of the
 * envelope's `resource.id`, and the suite opened with a describe-level
 * `test.skip(!ownerContext || !invitedContext || !testBlogId)`. That condition
 * is evaluated while the file is being collected, before any hook has run, so
 * all three values were always undefined and all four tests were always
 * skipped — `--list` still reports them, annotated `skip`, which is how the
 * suite looked alive while never having run.
 */
test.beforeAll(async () => {
  ownerContext = await authenticatedContext(PRIMARY_STORAGE_STATE);
  invitedContext = await authenticatedContext(SECONDARY_STORAGE_STATE);

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

  expect(createResponse.status()).toBe(201);
  const { resource } = await createResponse.json();
  testBlogId = resource.id;
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
  test("should create and get pending invitations", async () => {
    // Create assistant invitation. The route is plural — `assistants` and
    // `readers` — and the singular spelling used here matched no action at
    // all; the `if (status === 404) test.skip()` that followed made the miss
    // look like a feature nobody had built yet.
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/assistants`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    // An invitation is returned bare, and its identifier is `id`.
    const invitation = await createResponse.json();
    expect(invitation).toHaveProperty("id");

    // Get pending invitations for blog
    const listResponse = await ownerContext.get(
      `${API_URL}/v1/blogs/${testBlogId}/invitations`,
    );

    expect(listResponse.ok()).toBeTruthy();
    const list = await listResponse.json();
    expect(list.resources.length).toBeGreaterThan(0);

    // Cancel the invitation. Cancelling is an owner action inside the blog's
    // own collection: /v1/blogs/{blogId}/invitations/{invitationId}.
    const cancelResponse = await ownerContext.delete(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/${invitation.id}`,
    );

    expect(cancelResponse.status()).toBe(204);
  });

  test("should get user pending invitations", async () => {
    // Create invitation first
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/readers`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    // Get pending invitations for the invited user. A recipient handles their
    // own invitations through the Personal API, not through the blog.
    const myInvitationsResponse = await invitedContext.get(
      `${API_URL}/v1/users/me/invitations`,
    );

    expect(myInvitationsResponse.ok()).toBeTruthy();
    const myInvitations = await myInvitationsResponse.json();
    expect(myInvitations.resources.length).toBeGreaterThan(0);

    // Reject the invitation
    const rejectResponse = await invitedContext.post(
      `${API_URL}/v1/users/me/invitations/${invitation.id}/reject`,
    );

    expect(rejectResponse.status()).toBe(204);
  });

  test("should accept invitation and become participant", async () => {
    // Create invitation
    const createResponse = await ownerContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/invitations/readers`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const invitation = await createResponse.json();

    // Accept the invitation
    const acceptResponse = await invitedContext.post(
      `${API_URL}/v1/users/me/invitations/${invitation.id}/accept`,
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
      `${API_URL}/v1/blogs/${testBlogId}/invitations/assistants`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          username: INVITED_USER.username,
        },
      },
    );

    expect(response.status()).toBe(401);
  });
});

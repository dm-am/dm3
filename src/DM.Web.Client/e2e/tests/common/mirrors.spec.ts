import { test, expect } from "@playwright/test";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

test.describe("Mirrors API", () => {
  test("should get list of mirrors", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/mirrors`);

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("currentMirrorId");
    expect(data).toHaveProperty("mirrors");
    expect(Array.isArray(data.mirrors)).toBeTruthy();
  });

  // The session does not travel between mirrors. These endpoints handed a raw
  // session token to script over an unauthenticated call, and the transfer
  // token rode in the URL; the surface is gone and must stay gone.
  test("should not expose session transfer endpoints", async ({ request }) => {
    const issue = await request.get(
      `${API_URL}/v1/mirrors/transfer?targetMirror=main`,
    );
    expect(issue.status()).toBe(404);

    const accept = await request.post(
      `${API_URL}/v1/mirrors/transfer/accept?transferToken=anything`,
    );
    expect(accept.status()).toBe(404);
  });
});

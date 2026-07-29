import { test, expect, APIRequestContext } from "@playwright/test";
import { authenticatedContext } from "../../fixtures/auth";

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

// Test credentials
let authContext: APIRequestContext;

test.beforeAll(async () => {
  // Login to get authenticated context with cookies
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  // Cleanup: dispose the authenticated context
  if (authContext) {
    await authContext.dispose();
  }
});

test.describe("Blog API", () => {
  test("should get public blogs list", async ({ request }) => {
    const response = await request.get(`${API_URL}/v1/blogs`);
    expect(response.ok()).toBeTruthy();

    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should create and delete a blog", async () => {
    // Create blog (cookies are automatically sent)
    const createResponse = await authContext.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Test Blog",
        description: "A test blog for E2E testing",
        isPublic: true,
        commentsEnabled: true,
      },
    });

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Blog");

    const blogId = created.id;

    // Get blog by ID
    const getResponse = await authContext.get(`${API_URL}/v1/blogs/${blogId}`);

    expect(getResponse.ok()).toBeTruthy();
    const blog = await getResponse.json();
    expect(blog.title).toBe("Test Blog");

    // Delete blog
    const deleteResponse = await authContext.delete(
      `${API_URL}/v1/blogs/${blogId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });

  test("should update a blog", async () => {
    // Create blog
    const createResponse = await authContext.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Blog to Update",
        description: "Original description",
        isPublic: true,
        commentsEnabled: true,
      },
    });

    expect(createResponse.status()).toBe(201);
    const blogId = (await createResponse.json()).id;

    // Update blog
    const updateResponse = await authContext.patch(
      `${API_URL}/v1/blogs/${blogId}`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          title: "Updated Blog Title",
          description: "Updated description",
        },
      },
    );

    expect(updateResponse.ok()).toBeTruthy();
    const updated = await updateResponse.json();
    expect(updated.title).toBe("Updated Blog Title");

    // Cleanup
    await authContext.delete(`${API_URL}/v1/blogs/${blogId}`);
  });

  test("should require authentication for creating blog", async ({
    request,
  }) => {
    const response = await request.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Unauthorized Blog",
        isPublic: true,
      },
    });

    expect(response.status()).toBe(401);
  });
});

test.describe("Blog Publications API", () => {
  let testBlogId: string;

  test.beforeAll(async () => {
    // Create a test blog for publications
    const createResponse = await authContext.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Publications Test Blog",
        isPublic: true,
        commentsEnabled: true,
      },
    });

    if (createResponse.ok()) {
      testBlogId = (await createResponse.json()).id;
    }
  });

  test.afterAll(async () => {
    if (testBlogId) {
      await authContext.delete(`${API_URL}/v1/blogs/${testBlogId}`);
    }
  });

  test("should create and get a publication", async () => {
    if (!testBlogId) {
      test.skip();
      return;
    }

    // Create publication
    const createResponse = await authContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/publications`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          title: "Test Publication",
          content: "This is the content of the test publication.",
          preview: "Test preview text",
          publishImmediately: true,
          commentsEnabled: true,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Publication");

    const publicationId = created.id;

    // Get publication
    const getResponse = await authContext.get(
      `${API_URL}/v1/blogs/publications/${publicationId}`,
    );

    expect(getResponse.ok()).toBeTruthy();
    const publication = await getResponse.json();
    expect(publication.title).toBe("Test Publication");

    // Delete publication
    const deleteResponse = await authContext.delete(
      `${API_URL}/v1/blogs/publications/${publicationId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });

  test("should list publications in a blog", async () => {
    if (!testBlogId) {
      test.skip();
      return;
    }

    const response = await authContext.get(
      `${API_URL}/v1/blogs/${testBlogId}/publications`,
    );

    expect(response.ok()).toBeTruthy();
    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });
});

test.describe("Blog Rubrics API", () => {
  let testBlogId: string;

  test.beforeAll(async () => {
    // Create a test blog for rubrics
    const createResponse = await authContext.post(`${API_URL}/v1/blogs`, {
      headers: {
        "Content-Type": "application/json",
      },
      data: {
        title: "Rubrics Test Blog",
        isPublic: true,
        commentsEnabled: true,
      },
    });

    if (createResponse.ok()) {
      testBlogId = (await createResponse.json()).id;
    }
  });

  test.afterAll(async () => {
    if (testBlogId) {
      await authContext.delete(`${API_URL}/v1/blogs/${testBlogId}`);
    }
  });

  test("should create and delete a rubric", async () => {
    if (!testBlogId) {
      test.skip();
      return;
    }

    // Create rubric
    const createResponse = await authContext.post(
      `${API_URL}/v1/blogs/${testBlogId}/rubrics`,
      {
        headers: {
          "Content-Type": "application/json",
        },
        data: {
          title: "Test Rubric",
          sortOrder: 1,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Rubric");

    const rubricId = created.id;

    // Delete rubric
    const deleteResponse = await authContext.delete(
      `${API_URL}/v1/blogs/rubrics/${rubricId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });
});

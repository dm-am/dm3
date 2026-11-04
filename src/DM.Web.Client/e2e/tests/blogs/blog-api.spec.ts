import { test, expect, type APIRequestContext } from "@playwright/test";
import { API_BASE_URL, authenticatedContext } from "../../fixtures/auth";

let authContext: APIRequestContext;

test.beforeAll(async () => {
  authContext = await authenticatedContext();
});

test.afterAll(async () => {
  if (authContext) {
    await authContext.dispose();
  }
});

/**
 * Creates the blog a suite works on, and fails the run when it cannot. A single
 * resource travels inside an envelope (API_DESIGN.md): `{ resource: { id } }`.
 */
async function createSuiteBlog(title: string): Promise<string> {
  const response = await authContext.post(`${API_BASE_URL}/v1/blogs`, {
    data: {
      title,
      isPublic: true,
    },
  });

  expect(response.status()).toBe(201);
  const { resource } = await response.json();
  return resource.id;
}

test.describe("Blog API", () => {
  test("should get public blogs list", async ({ request }) => {
    const response = await request.get(`${API_BASE_URL}/v1/blogs`);
    expect(response.ok()).toBeTruthy();

    const data = await response.json();
    expect(data).toHaveProperty("resources");
    expect(Array.isArray(data.resources)).toBeTruthy();
  });

  test("should create and delete a blog", async () => {
    const createResponse = await authContext.post(`${API_BASE_URL}/v1/blogs`, {
      data: {
        title: "Test Blog",
        description: "A test blog for E2E testing",
        isPublic: true,
      },
    });

    expect(createResponse.status()).toBe(201);
    const { resource: created } = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Blog");

    const blogId = created.id;

    const getResponse = await authContext.get(
      `${API_BASE_URL}/v1/blogs/${blogId}`,
    );

    expect(getResponse.ok()).toBeTruthy();
    const { resource: blog } = await getResponse.json();
    expect(blog.title).toBe("Test Blog");

    const deleteResponse = await authContext.delete(
      `${API_BASE_URL}/v1/blogs/${blogId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });

  test("should update a blog", async () => {
    const createResponse = await authContext.post(`${API_BASE_URL}/v1/blogs`, {
      data: {
        title: "Blog to Update",
        description: "Original description",
        isPublic: true,
      },
    });

    expect(createResponse.status()).toBe(201);
    const blogId = (await createResponse.json()).resource.id;

    const updateResponse = await authContext.patch(
      `${API_BASE_URL}/v1/blogs/${blogId}`,
      {
        data: {
          title: "Updated Blog Title",
          description: "Updated description",
        },
      },
    );

    expect(updateResponse.ok()).toBeTruthy();
    const { resource: updated } = await updateResponse.json();
    expect(updated.title).toBe("Updated Blog Title");

    await authContext.delete(`${API_BASE_URL}/v1/blogs/${blogId}`);
  });

  test("should require authentication for creating blog", async ({
    request,
  }) => {
    const response = await request.post(`${API_BASE_URL}/v1/blogs`, {
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
    testBlogId = await createSuiteBlog("Publications Test Blog");
  });

  test.afterAll(async () => {
    if (testBlogId) {
      await authContext.delete(`${API_BASE_URL}/v1/blogs/${testBlogId}`);
    }
  });

  test("should create and get a publication", async () => {
    const createResponse = await authContext.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/publications`,
      {
        data: {
          title: "Test Publication",
          content: "This is the content of the test publication.",
          preview: "Test preview text",
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const { resource: created } = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Publication");

    const publicationId = created.id;

    // A publication is addressed on its own, not under its blog.
    const getResponse = await authContext.get(
      `${API_BASE_URL}/v1/publications/${publicationId}`,
    );

    expect(getResponse.ok()).toBeTruthy();
    const { resource: publication } = await getResponse.json();
    expect(publication.title).toBe("Test Publication");

    const deleteResponse = await authContext.delete(
      `${API_BASE_URL}/v1/publications/${publicationId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });

  test("should list publications in a blog", async () => {
    const response = await authContext.get(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/publications`,
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
    testBlogId = await createSuiteBlog("Rubrics Test Blog");
  });

  test.afterAll(async () => {
    if (testBlogId) {
      await authContext.delete(`${API_BASE_URL}/v1/blogs/${testBlogId}`);
    }
  });

  test("should create and delete a rubric", async () => {
    const createResponse = await authContext.post(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/rubrics`,
      {
        data: {
          title: "Test Rubric",
          sortOrder: 1,
        },
      },
    );

    expect(createResponse.status()).toBe(201);
    const { resource: created } = await createResponse.json();
    expect(created).toHaveProperty("id");
    expect(created.title).toBe("Test Rubric");

    const rubricId = created.id;

    const deleteResponse = await authContext.delete(
      `${API_BASE_URL}/v1/blogs/${testBlogId}/rubrics/${rubricId}`,
    );

    expect(deleteResponse.status()).toBe(204);
  });
});

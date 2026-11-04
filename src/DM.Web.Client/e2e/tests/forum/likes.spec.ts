import {
  test,
  expect,
  API_BASE_URL,
  authenticatedContext,
} from "../../fixtures/auth";

/**
 * The like is the shared LikeBadge, "Нравится" last in the caption line of the
 * `.topic` card: a `button.like-badge` for a reader who may press it, a static
 * `span.like-badge` for the author and a guest. Its state is aria-pressed.
 */
test.describe("Likes", () => {
  // Reading the control is not a write: the button is there for a signed-in
  // reader, on the topic and not only on the list.
  test("shows the like control on a topic to a signed-in reader", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/forum/general");
    const firstTopic = authenticatedPage
      .locator("td.col-title a.topic-link")
      .first();
    await expect(firstTopic).toBeVisible({ timeout: 10000 });
    await firstTopic.click();

    // The topic's own badge, not the first comment's: a bare
    // `button.like-badge` would also pass on a topic whose card has none.
    const likeButton = authenticatedPage
      .locator(".topic button.like-badge")
      .first();
    await expect(likeButton).toBeVisible();
    await expect(likeButton).toHaveText(/^Нравится/);
    await expect(likeButton).toHaveAttribute("aria-pressed", /^(true|false)$/);
  });

  // The write has an inverse (DELETE /v1/topics/{id}/likes), so the test
  // leaves the seed as it found it.
  test("likes a topic and takes the like back", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/forum/general");
    const firstTopic = authenticatedPage
      .locator("td.col-title a.topic-link")
      .first();
    await expect(firstTopic).toBeVisible({ timeout: 10000 });

    // The topic as the page reads it, for its id: the cleanup below speaks to
    // the API, which names a topic by id and not by its number on the board.
    const topicRead = authenticatedPage.waitForResponse((response) =>
      /\/v1\/forum\/general\/\d+(\?|$)/.test(response.url()),
    );
    await firstTopic.click();
    const topicId: string = (await (await topicRead).json()).resource.id;

    const likeButton = authenticatedPage
      .locator(".topic button.like-badge")
      .first();
    await expect(likeButton).toBeVisible();

    // The state is aria-pressed, not the words: the count says how many, not
    // whether the reader is among them. Which way it starts is the seed's
    // business, so the test reads it and asserts the flip in whichever
    // direction it goes.
    const liked = async () =>
      (await likeButton.getAttribute("aria-pressed")) === "true";
    const likedAtStart = await liked();

    // Set while the like this test pressed is standing, so a failure in the
    // middle takes back exactly that write and nothing of the seed's.
    let pressed = false;
    try {
      await likeButton.click();
      await expect.poll(liked).toBe(!likedAtStart);
      pressed = true;

      // Read again from the server: "Нравится" flips on the press whatever the
      // answer, so only a fresh read says whether the topic knows who liked it.
      await authenticatedPage.reload();
      await expect(likeButton).toBeVisible({ timeout: 10000 });
      await expect.poll(liked).toBe(!likedAtStart);

      // And back: the assertion is as much about the state this test leaves as
      // about the one it produced.
      await likeButton.click();
      await expect.poll(liked).toBe(likedAtStart);
      pressed = false;

      await authenticatedPage.reload();
      await expect(likeButton).toBeVisible({ timeout: 10000 });
      await expect.poll(liked).toBe(likedAtStart);
    } finally {
      if (pressed) {
        const cleanup = await authenticatedContext();
        const likes = `${API_BASE_URL}/v1/topics/${topicId}/likes`;
        if (likedAtStart) await cleanup.post(likes);
        else await cleanup.delete(likes);
        await cleanup.dispose();
      }
    }
  });
});

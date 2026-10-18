import {
  test,
  expect,
  API_BASE_URL,
  authenticatedContext,
} from "../../fixtures/auth";

/**
 * The like is the shared LikeBadge, last in the caption line of the `.topic`
 * card: a `button.like-badge` for a reader who may press it, a static
 * `span.like-badge` for the author and a guest. Its state is aria-pressed, and
 * the count is `.like-badge__count`. ".like-button" and the data-testid this
 * file once waited for never existed, "Новости" is not a board alias, and
 * ".topics-row" is not a class, so the body sat behind two `if`s that could
 * not be true. The `.like-btn` that replaced them went with the badge.
 */
test.describe("Likes", () => {
  // One test that runs. The write below is switched off, and a file whose only
  // test is skipped runs nothing at all — it reports green without opening a
  // page. Reading the control is not a write, and it is the half of the finding
  // that can be checked against the shared seed: the button is there for a
  // signed-in reader, on the topic and not only on the list.
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
    await expect(likeButton).toHaveAttribute("aria-label", /Нравится/);
    await expect(likeButton).toHaveAttribute("aria-pressed", /^(true|false)$/);
  });

  // Runs, and takes the like back. The write has an inverse (DELETE
  // /v1/topics/{id}/likes), so the test leaves the seed as it found it.
  //
  // It was off because GET /v1/topics/{id} answered likesCount with an empty
  // likes list: nothing filled the likers of a topic, so the page could not
  // tell that a like was the reader's own and the heart never showed as
  // pressed. The likers are the server's to send; this test is what shows
  // from the page whether they arrive.
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

    // The state is aria-pressed, not the caption: the count is rendered only
    // while it is above zero, so on a topic nobody has liked both readings
    // are the empty string and an assertion on the text passes on no change
    // at all. Which way it starts is the seed's business, so the test reads it
    // and asserts the flip in whichever direction it goes.
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

      // Read again from the server. The heart flips on the press whatever the
      // answer, so only a fresh read says whether the topic knows who liked
      // it: this is the half that stayed off while the likers of a topic were
      // never filled.
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

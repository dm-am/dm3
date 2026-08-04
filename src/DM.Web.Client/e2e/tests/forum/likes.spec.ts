import { test, expect } from "../../fixtures/auth";

/**
 * The like control is `.like-btn` inside `.likes-container` on the topic
 * card, and the tally beside it is `.likes-count`. ".like-button" and the
 * data-testid this file waited for do not exist, "Новости" is not a board
 * alias, and ".topics-row" is not a class — so the body sat behind two
 * `if`s that could not be true.
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

    const likeButton = authenticatedPage.locator(".like-btn").first();
    await expect(likeButton).toBeVisible();
    await expect(likeButton).toHaveAttribute("aria-label", /Нравится/);
  });

  // Off, and the reason has changed. It is no longer "a like is a write nothing
  // undoes": the write does have an inverse now, and DELETE /v1/topics/{id}/likes
  // answers 204 where it used to answer 409 for everybody.
  //
  // What stops this test is a defect above it. GET /v1/topics/{id} answers
  // likesCount: 6 and likes: [] - the likers are never populated, because every
  // mapping profile declares Likes as Ignore - so the page cannot tell that the
  // like is the reader's own. isLikedByMe is false however many times the heart
  // is pressed, and the tooltip that lists who liked is empty by construction.
  // Whether the read carries the likers or just a likedByMe flag is a decision
  // about the contract, not something to settle inside a test, so this stays off
  // until it is made - with the reason pointing at the API and not at the corpus.
  test.skip("likes a topic and takes the like back", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/forum/general");
    const firstTopic = authenticatedPage
      .locator("td.col-title a.topic-link")
      .first();
    await expect(firstTopic).toBeVisible({ timeout: 10000 });
    await firstTopic.click();

    const likeButton = authenticatedPage.locator(".like-btn").first();
    await expect(likeButton).toBeVisible();

    // The state is the class, not the caption: the count is rendered only
    // while it is above zero, so on a topic nobody has liked both readings
    // are the empty string and an assertion on the text passes on no change
    // at all. Which way it starts is the seed's business, so the test reads it
    // and asserts the flip in whichever direction it goes.
    const liked = () =>
      likeButton.evaluate((node) => node.classList.contains("liked"));
    const likedAtStart = await liked();

    await likeButton.click();
    await expect.poll(liked).toBe(!likedAtStart);

    // And back: the assertion is as much about the state this test leaves as
    // about the one it produced.
    await likeButton.click();
    await expect.poll(liked).toBe(likedAtStart);
  });
});

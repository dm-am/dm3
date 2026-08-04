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

  test.skip("should like topic when authenticated", async ({
    authenticatedPage,
  }) => {
    // Switched off, and no longer for want of a selector: a like is a write
    // against the shared seed and nothing in the corpus undoes it.
    await authenticatedPage.goto("/forum/general");
    const firstTopic = authenticatedPage
      .locator("td.col-title a.topic-link")
      .first();
    await expect(firstTopic).toBeVisible({ timeout: 10000 });
    await firstTopic.click();

    const likeButton = authenticatedPage.locator(".like-btn").first();
    await expect(likeButton).toBeVisible();
    const before = await likeButton.textContent();

    await likeButton.click();
    await expect(likeButton).not.toHaveText(before ?? "");
  });
});

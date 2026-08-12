/**
 * The account form saves a page size out of a whitelist that runs to 200, and
 * the API refuses a take or limit above 100 with a validation error rather than
 * a shortened page. This composable is the one place where a saved preference
 * turns into a request size, so this is where the two have to meet.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { useAuthStore } from "@/shared/stores/auth";
import { usePaging, DEFAULT_PAGE_SIZES, MAX_API_PAGE_SIZE } from "./usePaging";

/** A reader whose saved preferences are exactly these. */
function reader(paging: Record<string, number>) {
  useAuthStore().user = { settings: { paging } } as never;
}

describe("usePaging", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("serves the defaults to a viewer with no preferences", () => {
    const { postsPerPage, messagesPerPage } = usePaging();

    expect(postsPerPage.value).toBe(DEFAULT_PAGE_SIZES.postsPerPage);
    expect(messagesPerPage.value).toBe(DEFAULT_PAGE_SIZES.messagesPerPage);
  });

  it("serves the size the reader saved", () => {
    reader({ postsPerPage: 50, messagesPerPage: 30 });

    const { postsPerPage, messagesPerPage } = usePaging();

    expect(postsPerPage.value).toBe(50);
    expect(messagesPerPage.value).toBe(30);
  });

  it("asks for no more than the API serves", () => {
    // Every one of the five is offered up to 200 by the form and accepted by
    // the preferences endpoint, and every list they reach caps its page at 100.
    reader({
      postsPerPage: 200,
      commentsPerPage: 200,
      topicsPerPage: 200,
      messagesPerPage: 200,
      entitiesPerPage: 200,
    });

    const paging = usePaging();

    expect([
      paging.postsPerPage.value,
      paging.commentsPerPage.value,
      paging.topicsPerPage.value,
      paging.messagesPerPage.value,
      paging.entitiesPerPage.value,
    ]).toEqual([
      MAX_API_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
    ]);
  });
});

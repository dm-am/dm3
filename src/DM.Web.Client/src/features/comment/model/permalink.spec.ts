import { afterEach, describe, expect, it } from "vitest";
import { createRouter, createWebHistory } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";
import { commentPermalink } from "./permalink";

/**
 * Where the number of a comment in the moderator's warning window leads. It
 * has to open the view of the list the moderator was on: from a filtered or
 * re-sorted list the page number alone opens that page of the whole list,
 * where the comment usually is not.
 */
describe("commentPermalink", () => {
  afterEach(() => window.history.replaceState(null, "", "/"));

  it("keeps the filter and the sort together with the page", () => {
    window.history.replaceState(null, "", "/forum/newbies/3");

    expect(
      commentPermalink("c1", {
        authors: "Experienced",
        sortOrder: "desc",
        number: "2",
      }),
    ).toBe(
      permalinkOrigin() +
        "/forum/newbies/3?authors=Experienced&sortOrder=desc&number=2#comment-c1",
    );
  });

  it("opens the address the number of the comment links to", async () => {
    // The number is a router link to the same query and the anchor
    // (CommentItem, numberTo); its address is what the router resolves.
    const router = createRouter({
      history: createWebHistory(),
      routes: [{ path: "/:rest(.*)*", component: { render: () => null } }],
    });
    await router.push({
      path: "/forum/newbies/3",
      query: {
        authors: "Тест Елки,Experienced",
        search: "a&b + c",
        sortOrder: "desc",
        number: "2",
      },
    });
    const query = router.currentRoute.value.query;
    const numberHref = router.resolve({ query, hash: "#comment-c1" }).href;

    // Compared as the browser reads them: a letter written as a letter and
    // the same letter written as its escapes are one address.
    expect(new URL(commentPermalink("c1", query)).href).toBe(
      new URL(permalinkOrigin() + numberHref).href,
    );
  });

  it("carries no question mark for the first page of the whole list", () => {
    window.history.replaceState(null, "", "/forum/newbies/3");

    expect(commentPermalink("c1", {})).toBe(
      permalinkOrigin() + "/forum/newbies/3#comment-c1",
    );
  });
});

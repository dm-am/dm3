/**
 * @vitest-environment jsdom
 */

/**
 * The named places a page leaves for after an action, resolved against the
 * real route table.
 *
 * A page proves WHICH location it pushed; it cannot prove that the location
 * exists, because reaching the app layer for the route table is a downward
 * import a page is not allowed to make. This is the other half: the names and
 * params those pages push, resolved here, must come out as the addresses the
 * action was supposed to lead to. A typo in a route name, a param dropped
 * from the object, a route renamed in the table - each of them leaves the
 * page's own test green and is caught here.
 *
 * Every entry is an action whose ONLY confirmation is the screen it lands on.
 * A wrong address there is not a cosmetic slip: it is an author pressing
 * "Сохранить" and being shown a page where their publication is not.
 */
import { describe, expect, it } from "vitest";
import router from "@/app/providers/router";

/** What the action does, where it pushes, and the address that must come out. */
const TARGETS: {
  action: string;
  to: Parameters<typeof router.resolve>[0];
  address: string;
}[] = [
  {
    action: "Сохранение публикации открывает саму публикацию",
    to: {
      name: "blog-publication",
      params: { id: "the-blog", pubId: "p-1" },
    },
    address: "/blogs/the-blog/feed/p-1",
  },
  {
    action: "Удаление публикации возвращает в ленту ее рубрики",
    to: {
      name: "blog-feed",
      params: { id: "the-blog" },
      query: { rubric: "r-7" },
    },
    address: "/blogs/the-blog/feed?rubric=r-7",
  },
  {
    action: "Удаление публикации без рубрики возвращает в общую ленту",
    to: { name: "blog-feed", params: { id: "the-blog" } },
    address: "/blogs/the-blog/feed",
  },
  {
    action: "Удаление блога открывает список блогов автора",
    to: {
      name: "profile",
      params: { username: "SolohinLex" },
      query: { tab: "blogs" },
    },
    address: "/users/SolohinLex?tab=blogs",
  },
];

describe("where a page leaves for", () => {
  it.each(TARGETS)("$action", ({ to, address }) => {
    expect(router.resolve(to).fullPath).toBe(address);
  });

  // The canary: a name the table does not have must not resolve quietly into
  // something plausible, or the checks above would pass on a renamed route.
  it("cannot resolve a name the table does not have", () => {
    expect(() =>
      router.resolve({ name: "blog-publication-that-is-not" }),
    ).toThrow();
  });
});

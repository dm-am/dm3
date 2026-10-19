/**
 * @vitest-environment jsdom
 */

/**
 * The named places a page leaves for after an action, resolved against the
 * real route table: a page proves which location it pushed but cannot import
 * the table to prove it exists. A typo in a route name, a dropped param or a
 * renamed route leaves the page's own test green and is caught here.
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
});

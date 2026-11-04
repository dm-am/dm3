/**
 * @vitest-environment jsdom
 */

// A review's permalink opens the reviews of its post and brings the review up
// once they are read, and the post can leave the page before the read comes
// back. The rest of the permalink is PostReviews.spec.ts's.
import { it, expect, vi, afterEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { postApi } from "@/entities/game";
import { setScrollContainer } from "@/shared/lib/scroll";
import { REVIEWS, render, stubResizeObserver } from "./GamePost.fixtures";

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

afterEach(() => {
  setScrollContainer(null);
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

it("brings nothing up once the post is gone before its reviews come", async () => {
  let answer!: (reviews: unknown) => void;
  vi.spyOn(postApi, "getPostReviews").mockReturnValue(
    new Promise((resolve) => (answer = resolve)) as never,
  );
  // A page that would scroll: the review lies below what the container shows.
  const container = document.createElement("div");
  container.getBoundingClientRect = () => ({ top: 0, bottom: 100 }) as DOMRect;
  const scrollTo = vi.fn();
  container.scrollTo = scrollTo;
  setScrollContainer(container);

  const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-1");
  wrapper.unmount();
  // A review of another page under the same id.
  const stranger = document.createElement("div");
  stranger.id = "review-r-old";
  stranger.getBoundingClientRect = () => ({ top: 500 }) as DOMRect;
  document.body.appendChild(stranger);
  answer({ data: { resources: REVIEWS }, error: undefined });
  await flushPromises();

  expect(scrollTo).not.toHaveBeenCalled();
});

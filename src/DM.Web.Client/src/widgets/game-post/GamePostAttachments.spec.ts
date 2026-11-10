/**
 * @vitest-environment jsdom
 */

/** The files of a post: a clip in the corner opens a strip of them over the text. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { postApi } from "@/entities/game";
import type { Post, PostAttachment } from "@/entities/game";
import {
  clearRegistry,
  expandAll,
} from "@/shared/lib/composables/useExpandableRegistry";
import {
  post,
  render,
  stubResizeObserver,
  type PostWrapper,
} from "./GamePost.fixtures";

enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

describe("GamePost attachments", () => {
  const MAP_ID = "571c0beb-1890-9ba6-2170-4bb531bc51f6";

  const attachment = (over: Partial<PostAttachment> = {}): PostAttachment => ({
    id: MAP_ID,
    fileName: "карта-подземелья.jpg",
    contentType: "image/jpeg",
    sizeBytes: 6707,
    width: 125,
    height: 138,
    url: `/v1/uploads/${MAP_ID}/content`,
    createdUtc: "2026-07-26T15:00:00",
    ...over,
  });

  const carrying = (...files: PostAttachment[]): Post => ({
    ...post,
    attachments: files,
  });

  const three = () =>
    carrying(
      attachment(),
      attachment({
        id: "f-2",
        fileName: "план-таверны.png",
        sizeBytes: 219340,
        url: "/v1/uploads/f-2/content",
      }),
      attachment({
        id: "f-3",
        fileName: "правила.pdf",
        contentType: "application/pdf",
        sizeBytes: 49410,
        url: "/v1/uploads/f-3/content",
      }),
    );

  const isOpen = (wrapper: PostWrapper) =>
    wrapper.get(".attachments-fold").classes().includes("open");

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
    // "Развернуть все" opens the reviews of the post as well.
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    clearRegistry();
  });

  it("draws no file in the post, only a closed clip in the corner", async () => {
    const wrapper = await render("/", carrying(attachment()));

    expect(wrapper.find("img").exists()).toBe(false);
    const clip = wrapper.get(".post-card > .attachments-toggle");
    expect(clip.attributes("aria-expanded")).toBe("false");
    expect(clip.attributes("aria-label")).toBe("Вложение");
    expect(clip.find(".attachments-count").exists()).toBe(false);
    expect(isOpen(wrapper)).toBe(false);
    wrapper.unmount();
  });

  it("draws nothing for a post without files", async () => {
    const wrapper = await render("/");

    expect(wrapper.find(".attachments-toggle").exists()).toBe(false);
    expect(wrapper.find(".attachments-fold").exists()).toBe(false);
    wrapper.unmount();
  });

  it("counts the files beside the clip when there are several", async () => {
    const wrapper = await render("/", three());

    const clip = wrapper.get(".attachments-toggle");
    expect(clip.get(".attachments-count").text()).toBe("3");
    expect(clip.attributes("aria-label")).toBe("Вложения: 3");
    wrapper.unmount();
  });

  it("opens the strip over the text, even for one file", async () => {
    const wrapper = await render("/", carrying(attachment()));

    await wrapper.get(".attachments-toggle").trigger("click");

    expect(isOpen(wrapper)).toBe(true);
    expect(wrapper.get(".attachments-toggle").attributes("aria-expanded")).toBe(
      "true",
    );
    // Over the text: the strip comes before the text in the column.
    const body = [...wrapper.get(".post-body").element.children].map(
      (block) => block.classList[0],
    );
    expect(body.indexOf("expand-fold")).toBeLessThan(
      body.indexOf("truncated-content-wrapper"),
    );
    const name = wrapper.get(".attachments-strip .attachment-name");
    expect(name.text()).toBe("карта-подземелья.jpg");
    expect(name.attributes("href")).toContain(`/v1/uploads/${MAP_ID}/content`);
    expect(name.attributes("target")).toBe("_blank");
    expect(wrapper.get(".attachment-row").text().replace(/\s+/g, " ")).toBe(
      "карта-подземелья.jpg 6,5 КБ",
    );
    wrapper.unmount();
  });

  it("lists every file on a line of its own", async () => {
    const wrapper = await render("/", three());

    await wrapper.get(".attachments-toggle").trigger("click");

    expect(
      wrapper
        .findAll(".attachment-row")
        .map((row) => row.text().replace(/\s+/g, " ")),
    ).toEqual([
      "карта-подземелья.jpg 6,5 КБ",
      "план-таверны.png 214,2 КБ",
      "правила.pdf 48,3 КБ",
    ]);
    wrapper.unmount();
  });

  it("opens with the page's Развернуть все", async () => {
    const wrapper = await render("/", carrying(attachment()));

    expandAll();
    await flushPromises();

    expect(isOpen(wrapper)).toBe(true);
    wrapper.unmount();
  });
});

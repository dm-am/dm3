/**
 * @vitest-environment jsdom
 */

/**
 * TagSelector error copy.
 *
 * The tag list was the one place in the client that put an English sentence in
 * front of a Russian reader: `apiError.title || "Failed to load tags"`. Both
 * halves of the UI_STANDARDS rule are pinned here — the copy is Russian, and the
 * server's raw title does not replace it. The second case is the one that looked
 * harmless: the API answers a 500 with a title of its own, and a call site that
 * prefers it shows whatever the server happened to say.
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import TagSelector from "./TagSelector.vue";
import { gameApi } from "@/entities/game";

const FAILURE_MESSAGE = "Не удалось загрузить теги";

describe("TagSelector", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("reports a request that never reached the API in Russian", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: null,
      error: { type: "Unknown", title: "", status: 0, traceId: "" },
    });

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".selector-error").text()).toBe(FAILURE_MESSAGE);
  });

  it("does not show the title the server sent", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Internal server error",
        status: 500,
        traceId: "",
      },
    });

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".selector-error").text()).toBe(FAILURE_MESSAGE);
  });
});

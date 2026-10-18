/**
 * @vitest-environment jsdom
 */

/**
 * The reading row of the blog menu in the left sidebar.
 *
 * A button of a sidebar answers a refusal with a plate, not with a line under
 * its row (UI_STANDARDS, "Всплывающие сообщения"), the same as the game menu
 * (GameJoinActions.spec.ts).
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import BlogJoinActions from "./BlogJoinActions.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** A refusal the server named nothing in: the plate says the press. */
const refused: GeneralError = {
  status: 404,
  title: "",
  type: "",
  traceId: "t",
};

function mountStrip() {
  const store = useBlogDetailsStore();
  store.blog = { id: "b-1", title: "Записки" } as unknown as Blog;
  // Readers present keep the mount off the network.
  store.readers = [{ id: "u-1", username: "Читатель" }] as never;
  return mount(BlogJoinActions, { props: { variant: "strip" } });
}

describe("BlogJoinActions in the sidebar", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    toast.toasts.value.forEach((one) => toast.dismiss(one.id));
    vi.restoreAllMocks();
  });

  it("answers a refused subscription with a plate and adds no row", async () => {
    const wrapper = mountStrip();
    const rowsBefore = wrapper.findAll("li").length;
    vi.spyOn(useBlogDetailsStore(), "subscribe").mockResolvedValue(refused);

    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual(["Не удалось изменить подписку"]);
    expect(wrapper.findAll("li")).toHaveLength(rowsBefore);
    expect(wrapper.text()).not.toContain("Не удалось");
    wrapper.unmount();
  });
});

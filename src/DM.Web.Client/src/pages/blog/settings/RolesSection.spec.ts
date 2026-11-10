/**
 * @vitest-environment jsdom
 */

// An invited assistant joins the roster only on acceptance, so the block of
// invitations still out, read from the invitation endpoint (the kind in lower
// case, as ToApiString spells it), is the answer to inviting one.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import type { BlogInvitation } from "@/entities/blog";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import type { GeneralError } from "@/shared/api/models/common";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import RolesSection from "./RolesSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** An invitation as the endpoint serves it, the kind in lower case. */
const invitation = (
  id: string,
  username: string,
  createdUtc: string,
  type = "assistant",
): BlogInvitation =>
  ({
    id,
    blogId: "the-blog",
    blogTitle: "Дневник",
    invitedUser: { id: `u-${username}`, username },
    inviterUsername: "the-owner",
    type,
    createdUtc,
  }) as unknown as BlogInvitation;

const FRESH = invitation("i-2", "Кандидат", "2026-09-20T09:05:00Z");

const served = (resources: BlogInvitation[]) => ({
  data: { resources },
  error: null,
});

const refused = {
  data: null,
  error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
};

const stubs = {
  UserLink: {
    props: ["user"],
    template: '<span class="named">{{ user.username }}</span>',
  },
  UserAutocomplete: {
    props: ["modelValue"],
    emits: ["update:modelValue"],
    template:
      '<input class="invitee" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  // The confirmation of a removal is the real window, drawn in place.
  Teleport: true,
};

let list: ReturnType<typeof vi.fn>;
let inviteAssistant: ReturnType<typeof vi.fn>;

/** The section over a blog that already has one assistant. */
async function mountSection(initial: BlogInvitation[] = []) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = {
    ...createMockBlog("the-blog", "Дневник"),
    assistants: [createMockUser("u-1", "Помощник")],
  };
  vi.spyOn(store, "loadBlog").mockResolvedValue({ ok: true });

  list = vi.fn().mockResolvedValue(served(initial));
  inviteAssistant = vi.fn().mockResolvedValue({ data: FRESH, error: null });
  vi.spyOn(blogApi, "getInvitations").mockImplementation(
    list as unknown as typeof blogApi.getInvitations,
  );
  vi.spyOn(blogApi, "inviteAssistant").mockImplementation(
    inviteAssistant as unknown as typeof blogApi.inviteAssistant,
  );

  const wrapper = mount(RolesSection, {
    props: { canInvite: true, canRemove: true },
    global: { plugins: [pinia], stubs },
  });
  await flushPromises();
  return wrapper;
}

/**
 * The waiting block, as one line of text, or "" when it is not drawn.
 * The no-break space a date keeps after "в" stays.
 */
const waiting = (wrapper: VueWrapper) =>
  wrapper.find(".pending").exists()
    ? wrapper
        .find(".pending")
        .text()
        .replace(/[^\S\u00a0]+/g, " ")
        .trim()
    : "";

/** Name somebody and press "Пригласить". */
async function invite(wrapper: VueWrapper, username: string) {
  await wrapper.find("input.invitee").setValue(username);
  await wrapper.find(".invite-row button").trigger("click");
  await flushPromises();
}

describe("Blog RolesSection assistant invitation", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("draws nothing about waiting invitations while there are none", async () => {
    const wrapper = await mountSection();

    expect(waiting(wrapper)).toBe("");
  });

  it("names the invited person and the moment the invitation went out", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([FRESH]));

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toContain("Ожидают подтверждения");
    expect(waiting(wrapper)).toContain("Кандидат");
    expect(waiting(wrapper)).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
    // Not among the assistants: accepting is what puts a name there.
    expect(wrapper.findAll(".staff-list")[0].text()).not.toContain("Кандидат");
  });

  it("finds the invitations by the kind the endpoint spells", async () => {
    const wrapper = await mountSection([FRESH]);

    expect(waiting(wrapper)).toContain("Кандидат");
  });

  it("leaves reader invitations to the invitations section", async () => {
    const wrapper = await mountSection([
      invitation("i-9", "Читающий", "2026-09-02T08:00:00Z", "reader"),
    ]);

    expect(waiting(wrapper)).toBe("");
  });

  it("says nothing in the corner about the block it has just filled", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([FRESH]));

    await invite(wrapper, "Кандидат");

    expect(saidOutLoud()).toEqual([]);
    expect(
      (wrapper.find("input.invitee").element as HTMLInputElement).value,
    ).toBe("");
  });

  it("keeps the row when the reload that follows does not come back", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(refused);

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toContain("Кандидат");
  });

  it("speaks up when the invitation is refused, since nothing moved", async () => {
    const wrapper = await mountSection();
    inviteAssistant.mockResolvedValue(refused);

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toBe("");
    // Where the press was, and in no plate.
    expect(wrapper.get(".invite-row + .action-result").text()).toBe(
      "Не удалось пригласить ассистента",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

// The confirmation runs the removal, prints a refusal in itself and closes on success alone.
describe("Blog RolesSection assistant removal", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function remove(answer: unknown) {
    const wrapper = await mountSection();
    const request = vi
      .spyOn(blogApi, "removeAssistant")
      .mockResolvedValue(answer as never);
    await wrapper.find(".staff-item .remove-btn").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, request };
  }

  it("takes the assistant off with the window's flags and closes", async () => {
    const { wrapper, request } = await remove({ data: null, error: null });

    expect(request).toHaveBeenCalledWith("the-blog", "Помощник", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused removal in the window and stays open", async () => {
    const { wrapper } = await remove(refused);

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить ассистента",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});

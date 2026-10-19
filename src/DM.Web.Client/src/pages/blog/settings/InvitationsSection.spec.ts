/**
 * @vitest-environment jsdom
 */

// The new row is the answer to an invitation: last, next to the control,
// kept when the reload fails, replacing the dead invitation of the same person,
// with no toast beside it.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, useBlogDetailsStore, type Blog } from "@/entities/blog";
import type { BlogInvitation } from "@/entities/blog";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import type { GeneralError } from "@/shared/api/models/common";
import InvitationsSection from "./InvitationsSection.vue";
import { expectFirstReadStates } from "@/shared/lib/testing/firstRead";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const invitation = (
  id: string,
  username: string,
  createdUtc: string,
): BlogInvitation =>
  ({
    id,
    blogId: "the-blog",
    blogTitle: "Дневник",
    invitedUser: { id: `u-${username}`, username },
    inviterUsername: "the-owner",
    type: "reader",
    createdUtc,
  }) as unknown as BlogInvitation;

/** Out since the beginning of the month. */
const OLD = invitation("i-1", "Старожил", "2026-09-01T10:00:00Z");
/** The one the test sends. */
const FRESH = invitation("i-2", "Новичок", "2026-09-20T09:05:00Z");

const served = <T>(resources: T[]) => ({
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
    template: '<span class="invited">{{ user.username }}</span>',
  },
  UserAutocomplete: {
    props: ["modelValue"],
    emits: ["update:modelValue"],
    template:
      '<input class="invitee" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
};

let list: ReturnType<typeof vi.fn>;
let inviteReader: ReturnType<typeof vi.fn>;

/** The section over a blog whose owner is looking at it. */
/** `first`, when given, answers the read the section opens with. */
async function mountSection(
  initial: BlogInvitation[] = [OLD],
  first?: Promise<unknown>,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = { id: "the-blog", title: "Дневник" } as unknown as Blog;

  list = vi.fn().mockResolvedValue(served(initial));
  if (first) list.mockReturnValueOnce(first);
  inviteReader = vi.fn().mockResolvedValue({ data: FRESH, error: null });
  vi.spyOn(blogApi, "getInvitations").mockImplementation(
    list as unknown as typeof blogApi.getInvitations,
  );
  vi.spyOn(blogApi, "inviteReader").mockImplementation(
    inviteReader as unknown as typeof blogApi.inviteReader,
  );

  const wrapper = mount(InvitationsSection, {
    props: { canInvite: true, canCancel: true },
    global: { plugins: [pinia], stubs },
  });
  await flushPromises();
  return wrapper;
}

/**
 * The invitation rows, top to bottom, as one line of text each.
 * The no-break space a date keeps after "в" stays.
 */
const rows = (wrapper: VueWrapper) =>
  wrapper.findAll(".inv-item").map((r) =>
    r
      .text()
      .replace(/[^\S\u00a0]+/g, " ")
      .trim(),
  );

/** Name somebody and press "Пригласить читателя". */
async function invite(wrapper: VueWrapper, username: string) {
  await wrapper.find("input.invitee").setValue(username);
  await wrapper.find(".invite-row button").trigger("click");
  await flushPromises();
}

describe("Blog InvitationsSection", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the invitation just sent as the last row, next to the control", async () => {
    const wrapper = await mountSection();
    // The endpoint answers newest first.
    list.mockResolvedValue(served([FRESH, OLD]));

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(2);
    expect(rows(wrapper).at(-1)).toContain("Новичок");
    expect(rows(wrapper).at(-1)).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
    // Not the moment of the invitation that was already out.
    expect(rows(wrapper).at(-1)).not.toContain(formatDateFull(OLD.createdUtc));
  });

  it("says nothing in the corner about the row it has just shown", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([FRESH, OLD]));

    await invite(wrapper, "Новичок");

    expect(saidOutLoud()).toEqual([]);
    expect(
      (wrapper.find("input.invitee").element as HTMLInputElement).value,
    ).toBe("");
  });

  it("keeps the row when the reload that follows does not come back", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper).at(-1)).toContain("Новичок");
    expect(wrapper.text()).not.toContain("Активных приглашений нет");
  });

  it("replaces the invitation of a person invited a second time", async () => {
    // The server kills the earlier invitation as it issues this one.
    const earlier = invitation("i-0", "Новичок", "2026-09-10T08:00:00Z");
    const wrapper = await mountSection([earlier]);
    list.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(1);
    expect(rows(wrapper)[0]).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
  });

  it("speaks up when the invitation is refused, since nothing moved", async () => {
    const wrapper = await mountSection();
    inviteReader.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(1);
    // Where the press was, and in no plate.
    expect(wrapper.get(".invite-row + .action-result").text()).toBe(
      "Не удалось отправить приглашение",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

// The list waits in its shape, a failed first read says so, and emptiness is
// said only once the answer came.
describe("Blog InvitationsSection before the list is read", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("waits in its shape and says a failed first read, not an empty list", () =>
    expectFirstReadStates((first) => mountSection([], first), refused, {
      placeholder: ".inv-waiting",
      failed: "Не удалось загрузить приглашения",
      empty: "Активных приглашений нет",
    }));
});

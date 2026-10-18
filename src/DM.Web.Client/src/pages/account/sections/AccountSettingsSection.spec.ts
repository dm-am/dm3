/**
 * @vitest-environment jsdom
 */

/**
 * The settings block of the account page, and the only question it is hard to
 * answer on this screen: did the save go through?
 *
 * Nothing here moves when it does. The theme, the zone and the five page sizes
 * stay in the fields the reader chose them in, and the server answers with the
 * same values. A plate in the corner used to say it; the button says it now,
 * and these are the ways the button can lie.
 *
 * It can claim a save that never happened - on arrival, before the reader has
 * pressed anything. It can go on claiming one over a field that has changed
 * since, which is what a snapshot taken by reference rather than by value does.
 * And it can unsay a save that did happen: `fetchUser` refills this form from
 * the account, and the five paging numbers come back in whatever order the
 * answer spells them, so a form compared as it stands rather than as it is sent
 * would read as edited the instant the server's own answer landed in it.
 *
 * The last one is worth the mount: it needs the save, the refresh and the
 * watcher that refills the form, and none of the three would show it alone.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { Theme, type Paging } from "@/shared/api/models/personal";
import {
  accountTimeZone,
  setAccountTimeZone,
} from "@/shared/lib/utils/timeZone";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import AccountSettingsSection from "./AccountSettingsSection.vue";

/**
 * The account refresh after a save is a session call with a store behind it,
 * and what it brings back reaches this section as a prop. The prop is what the
 * tests below hand over, so the call itself is stubbed.
 */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  register: vi.fn(),
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

const paging = (over: Partial<Paging> = {}): Paging => ({
  postsPerPage: 20,
  commentsPerPage: 20,
  topicsPerPage: 20,
  messagesPerPage: 20,
  entitiesPerPage: 20,
  ...over,
});

/** The account the section is drawn for. */
const reader = (sizes: Paging) =>
  ({ username: "reader", settings: { paging: sizes } }) as never;

/**
 * The same five numbers an answer might spell in another order. JSON is what
 * the saved copy is compared by, so an order is a difference unless the form is
 * read through the request it sends.
 */
const inAnotherOrder = (sizes: Paging) => ({
  entitiesPerPage: sizes.entitiesPerPage,
  messagesPerPage: sizes.messagesPerPage,
  topicsPerPage: sizes.topicsPerPage,
  commentsPerPage: sizes.commentsPerPage,
  postsPerPage: sizes.postsPerPage,
});

const ZONE = "Europe/Moscow";

async function render() {
  const pinia = createPinia();
  setActivePinia(pinia);

  vi.spyOn(personalApi, "getMyPreferences").mockResolvedValue({
    data: {
      resource: { theme: Theme.Light, paging: paging(), timeZoneId: ZONE },
    },
    error: null,
  } as never);
  const update = vi
    .spyOn(personalApi, "updateMyPreferences")
    .mockResolvedValue({ data: {}, error: null } as never);

  const wrapper = mount(AccountSettingsSection, {
    props: { user: reader(paging()) },
    global: { plugins: [pinia] },
  });
  await flushPromises();
  return { wrapper, update };
}

/** The control the form is saved with. */
const saveButton = (wrapper: VueWrapper) => wrapper.get("button.save-button");

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** Presses the save and lets the request and the refresh settle. */
async function press(wrapper: VueWrapper) {
  await saveButton(wrapper).trigger("click");
  await flushPromises();
}

describe("the account settings block", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    vi.restoreAllMocks();
  });

  it("offers a save while nothing has been saved yet", async () => {
    const { wrapper } = await render();

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("says the save worked on the button and nowhere else", async () => {
    const { wrapper, update } = await render();

    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    expect(update).toHaveBeenCalledWith({
      theme: Theme.Light,
      paging: paging({ postsPerPage: 50 }),
      timeZoneId: ZONE,
    });
    expect(saveButton(wrapper).text()).toBe("Сохранено");
    expect(plates()).toEqual([]);
  });

  it("takes no further press while it reads Сохранено", async () => {
    const { wrapper, update } = await render();
    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    await press(wrapper);

    expect(saveButton(wrapper).attributes("disabled")).toBeDefined();
    expect(update).toHaveBeenCalledTimes(1);
  });

  it("offers the save again as soon as a field changes", async () => {
    const { wrapper, update } = await render();
    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    await wrapper.get("#account-topics-per-page").setValue("50");

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();

    await press(wrapper);
    expect(update).toHaveBeenCalledTimes(2);
  });

  // The account comes back through `fetchUser` and refills the form. Same
  // values, so the button has nothing to take back.
  it("holds the claim when the refreshed account refills the form", async () => {
    const { wrapper } = await render();
    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    await wrapper.setProps({
      user: reader(inAnotherOrder(paging({ postsPerPage: 50 })) as Paging),
    });
    await flushPromises();

    expect(saveButton(wrapper).text()).toBe("Сохранено");
    expect(saveButton(wrapper).attributes("disabled")).toBeDefined();
  });

  it("keeps offering the save when the server refused it", async () => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Недопустимый размер страницы" },
    } as never);

    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
    expect(wrapper.get(".error-text").text()).toBe(
      "Недопустимый размер страницы",
    );
  });

  /**
   * A refusal has one carrier, and it is the line beside the button. The
   * response interceptor used to answer a 403, a 429 and a 5xx with a plate of
   * its own while this line kept quiet; now the line says them and the plate
   * is taken down (refusalHere; the owner's rule of 26 September 2026). The
   * fields stay exactly as they were typed and the button is back on
   * "Сохранить", so the screen still says the save did not happen.
   */
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
  ])("says a %i beside the button", async (status, line) => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status, title: "Ошибка сервера" },
    } as never);

    await wrapper.get("#account-posts-per-page").setValue("50");
    await press(wrapper);

    expect(wrapper.get(".error-text").text()).toBe(line);
    // Refused all the same: nothing was applied and the press is on offer again.
    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });
});

/**
 * The zone of the account is what every date on the site is drawn by for a
 * signed-in reader, and the page holds it as the account stood when the page
 * read it. A save that did not hand the new zone over left the site on the old
 * one until the next visit: the reader chose Tokyo, read "Сохранено", and went
 * on seeing Moscow times everywhere.
 */
describe("the zone of the account", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    setAccountTimeZone(ZONE);
  });

  it("is applied to the site as soon as it is saved", async () => {
    const { wrapper } = await render();

    await wrapper.get("#account-time-zone").setValue("Asia/Tokyo");
    await press(wrapper);

    expect(accountTimeZone.value).toBe("Asia/Tokyo");
  });

  it("is left as it was when the save was refused", async () => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Неизвестный часовой пояс" },
    } as never);

    await wrapper.get("#account-time-zone").setValue("Asia/Tokyo");
    await press(wrapper);

    expect(accountTimeZone.value).toBe(ZONE);
  });
});

// The fields of the page sizes stand in the grid of the full version at any
// width: a phone shows them side by side as well, and the reader zooms in.
describe("the grid of the page sizes", () => {
  it("has no form of its own for a narrow screen", () => {
    const file = join(
      dirname(fileURLToPath(import.meta.url)),
      "AccountSettingsSection.vue",
    );
    const compiled = compileSass(sassBlocks(file), file);
    expect(compiled).not.toContain("@media");
    expect(compiled).not.toContain("@container");
    const grid = styleRules(compiled).find(
      ({ selector }) => selector === ".pagination-grid",
    );
    expect(grid?.declarations["grid-template-columns"]).toBe(
      "repeat(auto-fit, minmax(200px, 1fr))",
    );
  });
});

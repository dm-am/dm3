/**
 * @vitest-environment jsdom
 */

/**
 * The page the approval letter leads to.
 *
 * The letter pointed at an address the site does not route, so the last step of
 * the name change was a 404 and the approval could only run out. What the page
 * owes the reader is not one form but four answers: three of the ways a link
 * can be dead are different events, and only one of them leaves them anything
 * to do.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, UsernameInput } from "@/entities/user";
import UsernameChangePage from "./UsernameChangePage.vue";

const push = vi.fn();

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

// fetchUser reconciles the viewer with the server after the rename. It is a
// session call, not part of what this page decides, and it is stubbed so the
// success branch does not depend on a signed-in viewer.
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  register: vi.fn(),
  signIn: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

/** A request that never settles: the page stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
});

const approval = (
  status: "ready" | "expired" | "used",
  currentUsername?: string,
) =>
  vi.spyOn(accountApi, "getUsernameChangeApproval").mockResolvedValue({
    data: { status, currentUsername },
    error: null,
  });

/** Mounts the page with a live approval and a name typed into the field. */
const atTheForm = async (name = "newreader") => {
  approval("ready", "reader");

  const wrapper = mount(UsernameChangePage, options());
  await flushPromises();

  const field = wrapper.findComponent(UsernameInput);
  field.vm.$emit("update:modelValue", name);
  field.vm.$emit("availability", true);
  await nextTick();

  return wrapper;
};

describe("the page reached from the approval letter", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    push.mockClear();
    fetchUser.mockClear();
    // The value arrives in the fragment: it is never sent to a server, so it is
    // in no access log and in no Referer.
    window.location.hash = "#token=a-token";
    // UsernameInput checks the name it is given after a debounce. Left alone it
    // reaches for the network once the assertions are already done, and jsdom
    // reports the stray request as an unhandled error against whichever test is
    // still open.
    vi.spyOn(accountApi, "checkUsername").mockResolvedValue({
      data: { available: true },
      error: null,
    } as never);
  });

  it("tells the reader the link is being checked", () => {
    vi.spyOn(accountApi, "getUsernameChangeApproval").mockImplementation(
      pending,
    );

    const wrapper = mount(UsernameChangePage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
    // And says nothing yet about a failure that has not happened.
    for (const failure of ["истек", "уже изменено", "не работает"]) {
      expect(wrapper.text()).not.toContain(failure);
    }
  });

  it("opens the form on a live approval and names what is being changed", async () => {
    const wrapper = await atTheForm();

    expect(wrapper.text()).toContain("Выберите новое имя");
    expect(wrapper.text()).toContain("reader");
    expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
  });

  it("sends the chosen name off the approval token", async () => {
    const complete = vi
      .spyOn(accountApi, "completeUsernameChange")
      .mockResolvedValue({
        data: { requestedUsername: "newreader" },
        error: null,
      } as never);

    const wrapper = await atTheForm();
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(complete).toHaveBeenCalledWith("a-token", "newreader");
    expect(wrapper.text()).toContain("Имя изменено");
    expect(wrapper.text()).toContain("newreader");
    // The stored viewer still carries the old name until this runs.
    expect(fetchUser).toHaveBeenCalled();
  });

  /**
   * Approval and choice are days apart, so the name can be gone in between.
   * The reader picks another one without leaving the page: closing the form
   * over this would cost them the approval.
   */
  it("keeps the form open when the name went to someone else", async () => {
    vi.spyOn(accountApi, "completeUsernameChange").mockResolvedValue({
      data: null,
      error: { status: 409, title: "Это имя уже занято" },
    } as never);

    const wrapper = await atTheForm();
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(wrapper.text()).toContain("Это имя уже занято");
    expect(wrapper.text()).toContain("Выберите новое имя");
    expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
  });

  /**
   * The same letter followed in two tabs, and a window that closed while the
   * form was open. Both arrive as a refusal on the same call — the first is a
   * 409, exactly like a taken name — so what separates them is the token being
   * asked again, not the sentence the server sent.
   */
  it.each([
    ["used", 409, "Имя уже изменено"],
    ["expired", 404, "Срок действия ссылки истек"],
  ] as const)(
    "leaves the form for the %s link the second reading reports",
    async (status, httpStatus, shown) => {
      const wrapper = await atTheForm();

      vi.spyOn(accountApi, "completeUsernameChange").mockResolvedValue({
        data: null,
        error: { status: httpStatus, title: "anything at all" },
      } as never);
      approval(status);

      await wrapper.get("form").trigger("submit");
      await flushPromises();

      expect(wrapper.text()).toContain(shown);
      expect(wrapper.find("form").exists()).toBe(false);
    },
  );

  it("offers a new request when the approval ran out before the page opened", async () => {
    approval("expired");

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(wrapper.text()).toContain("Срок действия ссылки истек");
    expect(wrapper.text()).toContain("заявку заново");
    expect(wrapper.find("form").exists()).toBe(false);
  });

  /**
   * The state that used to be indistinguishable from a broken link: the reader
   * whose name had in fact been changed was told the link was invalid.
   */
  it("says the name was already changed on a spent link", async () => {
    approval("used");

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(wrapper.text()).toContain("Имя уже изменено");
    expect(wrapper.text()).not.toContain("Ссылка не работает");
    expect(wrapper.find("form").exists()).toBe(false);
  });

  it("calls the link broken when no request was issued for it", async () => {
    vi.spyOn(accountApi, "getUsernameChangeApproval").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Ссылка недействительна или устарела" },
    } as never);

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка не работает");
    expect(wrapper.text()).toContain("Возможные причины");
  });

  /**
   * An address opened without the fragment — a bookmark, a copy that lost the
   * tail. Nothing is asked of the server for it.
   */
  it("does not call the server when the address carries no token", async () => {
    window.location.hash = "";
    const check = vi.spyOn(accountApi, "getUsernameChangeApproval");

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(check).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Ссылка не работает");
  });
});

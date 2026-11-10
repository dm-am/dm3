/**
 * @vitest-environment jsdom
 */

/**
 * The row of actions on somebody else's profile and on the reader's own. The
 * blacklist does not ask: the same button takes either step back.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import {
  blacklistApi,
  useAuthStore,
  useCommunityStore,
  UserRole,
  userApi,
} from "@/entities/user";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import {
  button,
  confirmation,
  dismissToasts,
  hasButton,
  ok,
  refused,
  render,
  saidOutLoud,
} from "./ProfilePage.fixtures";

beforeEach(dismissToasts);
afterEach(() => vi.restoreAllMocks());

/** The account settings: a link of the actions row of the reader's own page, and nowhere else. */
describe("ProfilePage settings link", () => {
  const settings = (wrapper: VueWrapper) =>
    wrapper.findAll("a").filter((link) => link.text() === "Настройки");

  it("leads the owner from the actions row to the account settings", async () => {
    const wrapper = await render({ own: true });

    const [link] = settings(wrapper);
    expect(link.attributes("href")).toBe("/account");
    expect(link.element.closest(".actions")).not.toBeNull();
  });

  it("is not there on somebody else's page", async () => {
    const wrapper = await render({});

    expect(settings(wrapper)).toHaveLength(0);
  });
});

/** The subscription stands second on every profile, whether a recommendation can be written or not. */
describe("ProfilePage row of actions", () => {
  /** The row as it reads, the subscribe control named by what it is. */
  const row = (wrapper: VueWrapper) =>
    Array.from(wrapper.get(".actions").element.children).map((child) =>
      child.tagName.toLowerCase().startsWith("user-subscribe")
        ? "[подписка]"
        : (child.textContent ?? "").trim(),
    );

  it("puts the subscription second and the recommendation after it", async () => {
    const wrapper = await render({ endorsable: true });

    expect(row(wrapper)).toEqual([
      "Написать сообщение",
      "[подписка]",
      "Написать рекомендацию",
      "В черный список",
    ]);
  });

  it("keeps the subscription second where there is no recommendation to write", async () => {
    const wrapper = await render({});

    expect(row(wrapper)).toEqual([
      "Написать сообщение",
      "[подписка]",
      "В черный список",
    ]);
  });
});

/** "Написать сообщение" only where the server says the message would be taken (`viewer.canMessage`). */
describe("ProfilePage message to the user", () => {
  const row = (wrapper: VueWrapper) =>
    Array.from(wrapper.get(".actions").element.children).map((child) =>
      child.tagName.toLowerCase().startsWith("user-subscribe")
        ? "[подписка]"
        : (child.textContent ?? "").trim(),
    );

  const messageLinks = (wrapper: VueWrapper) =>
    wrapper.findAll("a").filter((link) => link.text() === "Написать сообщение");

  it("leads to the correspondence with the user where the message is taken", async () => {
    const wrapper = await render({});

    const [link] = messageLinks(wrapper);
    expect(link.attributes("href")).toBe("/messenger/user/Other");
  });

  it("is not offered where the profile says the message would be refused", async () => {
    const wrapper = await render({ messageable: false, endorsable: true });

    expect(messageLinks(wrapper)).toHaveLength(0);
    expect(row(wrapper)).toEqual([
      "[подписка]",
      "Написать рекомендацию",
      "В черный список",
    ]);
  });

  /** The answer is the session's: signing in or out on the page reads the profile again. */
  it("reads the profile again for a reader who signs in or out on it", async () => {
    const options = { messageable: false };
    const wrapper = await render(options);
    const load = vi.mocked(useCommunityStore().trySelectProfile);
    expect(load).toHaveBeenCalledTimes(1);
    expect(messageLinks(wrapper)).toHaveLength(0);

    options.messageable = true;
    useAuthStore().user = {
      id: "someone-id",
      username: "Someone",
      role: UserRole.RegularUser,
    } as never;
    await flushPromises();

    expect(load).toHaveBeenCalledTimes(2);
    expect(messageLinks(wrapper)).toHaveLength(1);

    useAuthStore().user = null;
    await flushPromises();

    expect(load).toHaveBeenCalledTimes(3);
  });
});

/** "Модерация" is a moderator's tab, and an address does not open it for others. */
describe("ProfilePage asked for the moderation tab", () => {
  it("opens on О себе for a reader who is not a moderator", async () => {
    const wrapper = await render({ query: "?tab=moderation" });

    const tabs = wrapper.findAll('[role="tab"]').map((tab) => tab.text());
    expect(tabs).not.toContain("Модерация");
    expect(wrapper.get('[role="tab"][aria-selected="true"]').text()).toBe(
      "О себе",
    );
  });
});

describe("ProfilePage link to the subscriptions", () => {
  const extraLinks = (wrapper: VueWrapper): string[] => {
    const block = wrapper.find(".extra-links");
    if (!block.exists()) return [];
    return block.findAll("a").map((link) => link.text());
  };

  it("is the first of the extra links on the reader's own page", async () => {
    const wrapper = await render({ own: true });

    expect(extraLinks(wrapper)[0]).toBe("Подписки");
    const link = wrapper.findAll("a").find((l) => l.text() === "Подписки")!;
    expect(link.attributes("href")).toBe("/subscriptions");
  });

  it("is not on somebody else's page", async () => {
    const wrapper = await render({});

    expect(extraLinks(wrapper)).not.toContain("Подписки");
  });

  it("stands with the owner's links at an address that spells the name in another case", async () => {
    const wrapper = await render({ own: true, address: "me" });

    expect(extraLinks(wrapper)).toEqual([
      "Подписки",
      "Мои обращения",
      "Загруженное",
    ]);
  });

  // The page is the owner's whichever check asks: the edit and the recommendation too.
  it("lets the owner edit, and asks no recommendation of him, at that address", async () => {
    const wrapper = await render({ own: true, address: "me" });

    expect(hasButton(wrapper, "Редактировать")).toBe(true);
    expect(userApi.getEndorsementEligibility).not.toHaveBeenCalled();
  });
});

/** The edit mode saves from the bar under the fields, and only from there. */
describe("ProfilePage edit mode", () => {
  const editButtons = (wrapper: VueWrapper) =>
    wrapper
      .findAll("button")
      .map((b) => b.text())
      .filter((label) => ["Сохранить", "Отмена", "Завершить"].includes(label));

  it("has one set of buttons, in the bar, and the row of actions stands down", async () => {
    const wrapper = await render({ own: true });

    await button(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(".actions").exists()).toBe(false);
    expect(editButtons(wrapper)).toEqual(["Завершить"]);
    expect(wrapper.get(".save-bar").text()).toContain("Завершить");
  });

  // The page carries the text drawn; the edit mode opens on the text as typed.
  const about = {
    display: "<strong>текст</strong>",
    source: "[b]текст[/b]\n[spoiler]и еще[/spoiler], Том &amp; Джерри",
  };
  const editor = (wrapper: VueWrapper) => wrapper.getComponent(BBCodeEditor);

  it("opens О себе on its BBCode, read as its author, not on the page's HTML", async () => {
    const wrapper = await render({ own: true, about });

    await button(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.ownRead).toHaveBeenCalledOnce();
    expect(editor(wrapper).props("modelValue")).toBe(about.source);
  });

  it("keeps what was typed into О себе over a switch of tabs", async () => {
    const wrapper = await render({ own: true, about });
    await button(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    editor(wrapper).vm.$emit("update:modelValue", "[i]новое[/i]");
    const tab = (label: string) =>
      wrapper.findAll('[role="tab"]').find((one) => one.text() === label)!;
    await tab("Топики").trigger("click");
    await tab("О себе").trigger("click");

    expect(editor(wrapper).props("modelValue")).toBe("[i]новое[/i]");
  });

  it("is the owner's: a reader the profile is not his gets the row of actions back", async () => {
    const wrapper = await render({ own: true });
    await button(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    useAuthStore().user = {
      id: "someone-id",
      username: "Someone",
      role: UserRole.RegularUser,
    } as never;
    await flushPromises();

    expect(wrapper.find(".save-bar").exists()).toBe(false);
    expect(wrapper.find(".actions").exists()).toBe(true);
  });
});

describe("ProfilePage blacklist button", () => {
  /** The state comes from the profile (blockedByViewer), not from a page of the reader's list. */
  it("reads its state from the profile, not from the reader's list", async () => {
    const wrapper = await render({ blocked: true });

    expect(hasButton(wrapper, "Убрать из черного списка")).toBe(true);
    expect(hasButton(wrapper, "В черный список")).toBe(false);
    expect(wrapper.list).not.toHaveBeenCalled();
  });

  it("blocks at once and turns into the way back", async () => {
    const block = vi.spyOn(blacklistApi, "blockUser").mockResolvedValue(ok());
    const wrapper = await render({ blocked: false });

    await button(wrapper, "В черный список").trigger("click");
    await flushPromises();

    expect(block).toHaveBeenCalledWith({ username: "Other" });
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(hasButton(wrapper, "Убрать из черного списка")).toBe(true);
    expect(hasButton(wrapper, "В черный список")).toBe(false);
    expect(saidOutLoud()).toEqual([]);
  });

  it("unblocks at once, asking nothing, and turns back", async () => {
    const unblock = vi
      .spyOn(blacklistApi, "unblockUser")
      .mockResolvedValue(ok());
    const wrapper = await render({ blocked: true });

    await button(wrapper, "Убрать из черного списка").trigger("click");
    await flushPromises();

    expect(unblock).toHaveBeenCalledWith("Other");
    expect(confirmation(wrapper).exists()).toBe(false);
    expect(hasButton(wrapper, "В черный список")).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps its word after a refusal and says why", async () => {
    vi.spyOn(blacklistApi, "blockUser").mockResolvedValue(refused());
    const wrapper = await render({ blocked: false });

    await button(wrapper, "В черный список").trigger("click");
    await flushPromises();

    expect(hasButton(wrapper, "В черный список")).toBe(true);
    // Under the row of buttons of the profile; no plate in the corner.
    expect(wrapper.get(".actions + .action-result").text()).toBe(
      "Пользователь не добавлен в черный список",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

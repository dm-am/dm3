/**
 * @vitest-environment jsdom
 */

/**
 * The name in the heading of a profile, the earlier names behind "[...]", and
 * in edit mode the link to the account, where another name is asked for.
 */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { formatDate } from "@/shared/lib/utils/datetime";
import ProfileUsername from "./ProfileUsername.vue";

const HISTORY = [
  {
    id: "h1",
    oldUsername: "Anya",
    newUsername: "Annie",
    changedUtc: "2024-02-01T10:00:00Z",
  },
  {
    id: "h2",
    oldUsername: "Annie",
    newUsername: "Anna",
    changedUtc: "2025-06-15T10:00:00Z",
  },
];

// The tooltip draws its content on hover only; drawn in place here.
const stubs = {
  Tooltip: {
    template: "<span><slot /><span><slot name='content' /></span></span>",
  },
  RouterLink: RouterLinkStub,
};

interface HeadingOptions {
  history?: typeof HISTORY;
  editing?: boolean;
  /** A request of the owner's waits for a moderator. */
  pending?: boolean;
}

function heading(options: HeadingOptions = {}) {
  return mount(ProfileUsername, {
    props: {
      user: {
        username: "Anna",
        usernameHistory: options.history ?? [],
      } as never,
      editing: options.editing ?? false,
      nameChangePending: options.pending ?? false,
    },
    global: { stubs },
  });
}

const changeLink = (wrapper: ReturnType<typeof heading>) =>
  wrapper
    .findAllComponents(RouterLinkStub)
    .find((link) => link.text() === "Сменить имя пользователя");

describe("ProfileUsername", () => {
  it("lists the names held before, with the dates of the change", () => {
    const wrapper = heading({ history: HISTORY });

    expect(wrapper.get("h1").text()).toContain("Профиль: Anna");
    expect(wrapper.get(".history-hint").text()).toBe("[...]");
    expect(wrapper.findAll(".history-old").map((e) => e.text())).toEqual([
      "Anya",
      "Annie",
    ]);
    expect(wrapper.findAll(".history-when").map((e) => e.text())).toEqual(
      HISTORY.map((entry) => formatDate(entry.changedUtc)),
    );
  });

  it("puts no [...] after a name never changed", () => {
    const wrapper = heading();

    expect(wrapper.find(".history-hint").exists()).toBe(false);
  });

  // The account keeps the one form of the request: the profile has none of its own.
  it("leads the owner to the section of the account that changes the name", () => {
    const wrapper = heading({ editing: true });

    expect(changeLink(wrapper)?.props("to")).toEqual({
      name: "account",
      hash: "#username-change",
    });
    expect(wrapper.find("textarea").exists()).toBe(false);
    expect(wrapper.find("button").exists()).toBe(false);
  });

  it("says a request is pending instead of leading to another", () => {
    const wrapper = heading({ editing: true, pending: true });

    expect(wrapper.get(".change-name-pending").text()).toBe(
      "Заявка на смену имени уже на рассмотрении",
    );
    expect(changeLink(wrapper)).toBeUndefined();
  });

  it("shows nothing of the name change outside the edit mode", () => {
    const wrapper = heading({ pending: true });

    expect(wrapper.find(".change-name-pending").exists()).toBe(false);
    expect(changeLink(wrapper)).toBeUndefined();
  });
});

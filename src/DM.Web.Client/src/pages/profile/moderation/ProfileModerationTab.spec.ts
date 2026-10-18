/**
 * @vitest-environment jsdom
 */

/**
 * The section "Аккаунт" of the profile's "Модерация" tab: the two actions on
 * the account, the second factor first, in one line at every width
 * (ButtonRow): none goes into "…" and the line does not scroll sideways.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { UserRole } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import ProfileModerationTab from "./ProfileModerationTab.vue";

function render() {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().updateUser({
    username: "Admin",
    role: UserRole.Admin,
  } as never);
  return shallowMount(ProfileModerationTab, {
    props: {
      profile: {
        violations: [],
        permissions: {},
        moderatorNotes: [],
        isUnderModerationWatch: false,
      } as unknown as ModeratedProfile,
      targetUsername: "Colleague",
    },
    global: {
      plugins: [pinia],
      // The row is drawn for real: a stub would not draw what stands in it.
      stubs: { ButtonRow: false },
    },
  });
}

beforeEach(() => {
  localStorage.clear();
});

describe("the section of the account in the moderation tab", () => {
  it("stands both actions in a row of buttons, the second factor first, neither held back", () => {
    const wrapper = render();

    const row = wrapper.get(".mod-account_actions");
    expect(row.classes()).toContain("button-row");
    expect(wrapper.find("[data-fit-row]").exists()).toBe(false);
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    const actions = Array.from(row.element.children).map((child) => [
      child.tagName.toLowerCase(),
      child.hasAttribute("data-fit-keep"),
    ]);
    expect(actions).toEqual([
      ["moderation-two-factor-stub", false],
      ["moderation-account-removal-stub", false],
    ]);
  });
});

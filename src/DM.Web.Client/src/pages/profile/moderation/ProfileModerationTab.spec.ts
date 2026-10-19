/**
 * @vitest-environment jsdom
 */

/**
 * The section "Аккаунт" of the profile's "Модерация" tab: who is offered its
 * two actions, the second factor first.
 */
import { describe, it, expect } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { UserRole } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import ProfileModerationTab from "./ProfileModerationTab.vue";

function render(role: UserRole, targetUsername = "Colleague") {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().updateUser({ username: "Admin", role } as never);
  return shallowMount(ProfileModerationTab, {
    props: {
      profile: {
        violations: [],
        permissions: {},
        moderatorNotes: [],
        isUnderModerationWatch: false,
      } as unknown as ModeratedProfile,
      targetUsername,
    },
    global: { plugins: [pinia], renderStubDefaultSlot: true },
  });
}

/** The actions of the section, in the order they stand. */
const actions = (wrapper: ReturnType<typeof render>) =>
  Array.from(
    wrapper.html().matchAll(/<moderation-(two-factor|account-removal)-stub/g),
    ([, action]) => action,
  );

describe("the section of the account in the moderation tab", () => {
  it("offers an administrator both actions, the second factor first", () => {
    expect(actions(render(UserRole.Admin))).toEqual([
      "two-factor",
      "account-removal",
    ]);
  });

  it("offers a senior moderator the removal alone", () => {
    expect(actions(render(UserRole.SeniorModerator))).toEqual([
      "account-removal",
    ]);
  });

  it("is not drawn for a moderator below them", () => {
    expect(render(UserRole.Moderator).text()).not.toContain("Аккаунт");
    expect(actions(render(UserRole.Moderator))).toEqual([]);
  });

  it("is not drawn on the viewer's own profile, in any case of the name", () => {
    expect(actions(render(UserRole.Admin, "admin"))).toEqual([]);
  });
});

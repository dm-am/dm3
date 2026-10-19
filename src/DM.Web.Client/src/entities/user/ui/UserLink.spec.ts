/**
 * @vitest-environment jsdom
 */

/**
 * The role badge after the name, node by node with the styles that reach it,
 * and the words of its tooltip.
 */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { UserRole, type UserRef } from "@/shared/api/models/common/user";
import { markupOutline } from "@/shared/lib/testing/markupOutline";
import { Tooltip } from "@/shared/ui/Tooltip";
import UserLink from "./UserLink.vue";

const mentor = {
  id: "u-1",
  username: "Гоблин",
  role: UserRole.Mentor,
  lastActivityUtc: null,
  isNewbie: false,
} as UserRef;

describe("UserLink role badge", () => {
  it("draws the letter between grey brackets, unbroken, its tooltip reached by Tab", () => {
    const wrapper = mount(UserLink, {
      props: { user: mentor },
      global: { stubs: { RouterLink: RouterLinkStub } },
    });

    expect(markupOutline(wrapper)).toEqual([
      "span",
      "  a.user-link { overflow-wrap: break-word }",
      '    "Гоблин"',
      "  span.role-badge { white-space: nowrap }",
      '    " "',
      "    span.bracket { color: var(--text-muted) }",
      '      "["',
      '    span.tooltip-trigger[tabindex="0"] { display: inline }',
      "      span.letter.role-mentor { color: var(--accent-green); cursor: help; font-weight: bold }",
      '        "M"',
      "    <!--teleport start-->",
      "    <!--teleport end-->",
      "    span.bracket { color: var(--text-muted) }",
      '      "]"',
    ]);
    expect(
      wrapper.findAllComponents(Tooltip).map((tip) => tip.props("text")),
    ).toEqual(["Наставник"]);
  });
});

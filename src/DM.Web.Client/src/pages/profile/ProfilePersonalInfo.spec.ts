/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import ProfilePersonalInfo from "./ProfilePersonalInfo.vue";
import type { User } from "@/shared/api/models/community";

describe("the contacts of a profile", () => {
  it("removes a middle row without moving the others into its inputs", async () => {
    const contacts = [
      { contactType: "Telegram", value: "@first" },
      { contactType: "Telegram", value: "@second" },
      { contactType: "Discord", value: "third" },
    ];
    const wrapper = mount(ProfilePersonalInfo, {
      props: {
        user: { username: "Reader", contacts } as unknown as User,
        isEditMode: true,
      },
    });
    const thirdRow = wrapper.findAll(".contact-edit-row")[2].element;

    // The site's row sign, named by what it does.
    const removes = wrapper.findAllComponents(RowIconButton);
    expect(removes.map((sign) => sign.props())).toEqual(
      Array(3).fill({ icon: "trash", label: "Удалить контакт" }),
    );
    await removes[1].trigger("click");

    const rows = wrapper.findAll(".contact-edit-row");
    const values = rows.map(
      (row) => (row.get(".contact-value").element as HTMLInputElement).value,
    );
    expect(values).toEqual(["@first", "third"]);
    // A row keyed by its index would hand the third contact the second's inputs.
    expect(rows[1].element).toBe(thirdRow);
    expect(wrapper.emitted("updateField")?.at(-1)).toEqual([
      "contacts",
      JSON.stringify([contacts[0], contacts[2]]),
    ]);
  });
});

/**
 * The public profile hides a birthday kept private and carries no visibility:
 * the editor opens on the owner's own profile, as stored.
 */
describe("the edit mode of the personal info", () => {
  const OWN = {
    visibility: { showBirthday: true, showRating: true },
    birthday: { day: 14, month: 3, year: 1991 },
  };

  function editor(own: typeof OWN | null) {
    return mount(ProfilePersonalInfo, {
      props: {
        // No birthday and no visibility, as the public profile of a hidden one.
        user: { username: "Reader", contacts: [] } as unknown as User,
        isEditMode: true,
        // Only the fields the editor reads.
        own: own as never,
      },
    });
  }

  /** The box beside the words it stands by. */
  const box = (wrapper: ReturnType<typeof editor>, words: string) => {
    const row = wrapper
      .findAll(".checkbox-row")
      .find((label) => label.text() === words);
    if (!row) throw new Error(`no box "${words}"`);
    return row.get('input[type="checkbox"]').element as HTMLInputElement;
  };

  const yearShown = (wrapper: ReturnType<typeof editor>) =>
    box(wrapper, "Показывать год рождения").checked;

  const ratingShown = (wrapper: ReturnType<typeof editor>) =>
    box(wrapper, "Участие в рейтинге").checked;

  it("ticks Показывать год рождения where the owner saved it ticked", () => {
    expect(yearShown(editor(OWN))).toBe(true);
  });

  it("opens the date the owner stored, which the public profile left out", () => {
    const wrapper = editor(OWN);

    expect(
      (
        wrapper.get('input[aria-label="День рождения"]')
          .element as HTMLInputElement
      ).value,
    ).toBe("14.03.1991");
  });

  it("leaves the box clear where the owner saved it clear", () => {
    expect(
      yearShown(
        editor({
          ...OWN,
          visibility: { showBirthday: false, showRating: true },
        }),
      ),
    ).toBe(false);
  });

  it("offers Участие в рейтинге beside the year, as the owner saved it, with its hint", () => {
    const wrapper = editor({
      ...OWN,
      visibility: { showBirthday: true, showRating: false },
    });

    expect(ratingShown(wrapper)).toBe(false);
    expect(ratingShown(editor(OWN))).toBe(true);
    expect(wrapper.get(".rating-field .form-field-hint").text()).toBe(
      "Без участия рейтинг не виден в списках и карточках, в профиле он виден всегда",
    );
  });

  it("hands a change of the participation to the bar's save", async () => {
    const wrapper = editor(OWN);

    await wrapper
      .findAll(".checkbox-row")
      .find((label) => label.text() === "Участие в рейтинге")!
      .get("input")
      .setValue(false);

    expect(wrapper.emitted("updateField")?.at(-1)).toEqual([
      "showRating",
      "false",
    ]);
  });
});

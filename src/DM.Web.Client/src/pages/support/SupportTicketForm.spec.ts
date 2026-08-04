/**
 * @vitest-environment jsdom
 */

/**
 * Support and complaint are two pages over one form.
 *
 * The owner's shape: both routes keep their own framing text and mount the
 * same form, and only the list of reasons differs between them. The body used
 * to be a BBCode editor while not one of the three surfaces that read a ticket
 * back renders markup - the author typed tags and moderation read brackets -
 * so it is a plain multiline field now, in the order the owner set.
 *
 * The mounts are of the pages, not of the form alone: what has to hold is the
 * composition, and a form checked on its own would keep passing after a page
 * grew a second copy of it.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import SupportPage from "./SupportPage.vue";
import ComplaintPage from "./ComplaintPage.vue";
import { SupportTicketForm } from "@/features/support-ticket";

// /support reads ?reason=access off the route; nothing else on either page
// touches the router (the form's only link sits on the success screen).
vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push: vi.fn() }),
}));

/** The form shell is registered by the app, not by the test runner. */
const options = () => ({
  global: { plugins: [createPinia()], components: { Form, FormField } },
});

/** Values of the reason picker, in the order the page offers them. */
const reasonsOn = (wrapper: { findAll: (selector: string) => unknown[] }) =>
  (wrapper.findAll("option") as { element: HTMLOptionElement }[]).map(
    (option) => option.element.value,
  );

describe("support and complaint", () => {
  beforeEach(() => {
    // No stored viewer: the guest wording is what both pages open with.
    localStorage.clear();
  });

  it("mounts one and the same form on both pages", () => {
    expect(
      mount(SupportPage, options()).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
    expect(
      mount(ComplaintPage, options()).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
  });

  it("offers a different list of reasons on each page", () => {
    const support = reasonsOn(mount(SupportPage, options()));
    const complaint = reasonsOn(mount(ComplaintPage, options()));

    expect(support.length).toBeGreaterThan(0);
    expect(complaint.length).toBeGreaterThan(0);
    expect(support.filter((value) => complaint.includes(value))).toEqual([]);
  });

  it("takes the ticket body in a plain field, with no markup editor", () => {
    const wrapper = mount(ComplaintPage, options());

    expect(wrapper.findAll("textarea")).toHaveLength(1);
    expect(wrapper.find('[class*="bbcode"]').exists()).toBe(false);
    expect(wrapper.findComponent({ name: "BBCodeEditor" }).exists()).toBe(
      false,
    );
  });

  it("asks in the order the owner set", () => {
    const labels = mount(ComplaintPage, options())
      .findAll(".form-field-label label")
      .map((label) => label.text());

    expect(labels).toEqual([
      "Тип жалобы",
      "Почта для ответа",
      "Тема жалобы",
      "Ссылка на нарушение",
      "Текст жалобы",
    ]);
  });

  it("leaves the autofill of the contact field to the field", () => {
    const wrapper = mount(SupportPage, options());

    // A form-level "off" is read by part of the browsers as a veto over the
    // fields' own values, and this is the field a password manager fills.
    expect(wrapper.find("form").attributes("autocomplete")).toBeUndefined();
    expect(wrapper.find("#contact").attributes("autocomplete")).toBe("email");
  });
});

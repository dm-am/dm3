/** The support and complaint pages mounted for their specs, the reader's steps and the readers of the form. */
import { vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import SupportPage from "./SupportPage.vue";
import ComplaintPage from "./ComplaintPage.vue";

// A function rather than an arrow: the markup editor watches its mode tabs with
// one, built with `new`, and vitest will not construct an arrow.
export function stubResizeObserver() {
  vi.stubGlobal(
    "ResizeObserver",
    vi.fn(function () {
      return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
    }),
  );
}

/** The two pages, and every address they and the sent card link to. */
const routes: Address[] = [
  ["support", "/support"],
  ["complaint", "/complaint"],
  ["support-track", "/support/track"],
  ["my-tickets", "/my-tickets"],
  ["my-ticket", "/my-tickets/:ticketId"],
  ["rules", "/rules"],
  ["forum", "/forum/:alias"],
  ["community", "/community"],
  ["account", "/account"],
  ["profile", "/users/:username"],
  ["home", "/"],
];

/** A signed-in viewer, as the auth store reads him back on creation. */
export const signIn = (username = "Reader") =>
  localStorage.setItem(
    "user",
    JSON.stringify({ username, role: "RegularUser", settings: {} }),
  );

export type Page = typeof SupportPage;

/** A page opened at its address, on a reason as a link that knows it opens it. */
export const openOn = async (Page: Page, subtype?: string) => {
  const own = Page === ComplaintPage ? "/complaint" : "/support";
  const address = subtype ? `${own}?subtype=${subtype}` : own;
  // Windows draw in place, where the page text is read.
  return (await mountPage(Page, address, { routes, stubs: { Teleport: true } }))
    .wrapper;
};

/** The text, as the editor hands it to the form. */
export const write = async (wrapper: VueWrapper, text: string) => {
  wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", text);
  await flushPromises();
};

/** Fill in what a guest has to and press the button. */
export const fillAndSend = async (
  wrapper: VueWrapper,
  text = "Что случилось",
) => {
  if (wrapper.find("#contact").exists()) {
    await wrapper.find("#contact").setValue("reader@example.com");
  }
  await write(wrapper, text);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
};

/** The reason the picker currently shows; empty when none is chosen. */
export const chosenReason = (wrapper: VueWrapper) =>
  (wrapper.find("select").element as HTMLSelectElement).value;

/** Values of the reason picker, in the order the page offers them. */
export const reasonsOn = (wrapper: VueWrapper) =>
  wrapper
    .find("select")
    .findAll("option")
    .map((option) => (option.element as HTMLOptionElement).value)
    .filter((value) => value !== "");

/** Captions of the fields, in the order the form asks. */
export const labelsOn = (wrapper: VueWrapper) =>
  wrapper.findAll(".form-field-label label").map((label) => label.text());

/** Where the links of a mounted piece lead. */
export const linksOn = (wrapper: VueWrapper) =>
  wrapper
    .findAll("a")
    .map((link) => ({ href: link.attributes("href"), text: link.text() }));

/** Where the links of the note under the reason lead. */
export const noteLinksOn = (wrapper: VueWrapper) =>
  wrapper.findAll(".reason-note a").map((link) => link.attributes("href"));

/** The lines of the note under the reason, as they read. */
export const noteOn = (wrapper: VueWrapper) =>
  wrapper
    .findAll(".reason-note-line")
    .map((line) => line.text().replace(/\s+/g, " ").trim());

/** The hint of the field whose control has this id. */
export const hintOf = (wrapper: VueWrapper, id: string) =>
  wrapper
    .find(`#${id}`)
    .element.closest(".form-field")
    ?.querySelector(".form-field-hint")
    ?.textContent?.trim();

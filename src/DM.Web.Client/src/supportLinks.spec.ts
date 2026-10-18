/**
 * @vitest-environment jsdom
 */

/**
 * Every way into the support form names the reason it sends the reader for,
 * and the form opens on that reason.
 *
 * The pages a letter opens and the guest cards end on "Нужна помощь?
 * Обратитесь в поддержку", the profile of "Аккаунт удален" sends a returning
 * author there too, and each of them knows why the reader needs it. Ten of those links once carried a query parameter
 * of their own (?reason=access) while the form read another one (?subtype),
 * so every one of them opened on the default reason. Each side had its own
 * green test: the links pinned the address they wrote, the form pinned the
 * address it read, and nothing put the two next to each other.
 *
 * This walk is that comparison. An address of the support page with a query
 * is written in one place only (supportFor), every call names its reason, the
 * reason each door names is the one listed below, and the form, mounted on the
 * address supportFor builds, shows that reason in its picker.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import SupportPage from "@/pages/support/SupportPage.vue";
import { supportFor, type TicketSubtype } from "@/entities/ticket";

// The page holds the markup editor; a mount left standing keeps its timers
// running past the environment's teardown.
enableAutoUnmount(afterEach);

const { route } = vi.hoisted(() => ({
  route: { query: {} as Record<string, string> },
}));
vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({ push: vi.fn() }),
}));

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** The one file allowed to spell the address with its query. */
const BUILDER = "entities/ticket/lib/supportRoute.ts";

/**
 * The reason each door sends the reader with, in the order its calls appear.
 * A new door is added here on purpose: the reason is a decision about what the
 * reader came for, and a copied call would otherwise carry the wrong one.
 */
const DOORS: Record<string, TicketSubtype[]> = {
  // "Нет доступа к почте?" on the recovery card.
  "features/auth/ui/AccessRecoveryForm.vue": ["AccessRecovery"],
  // "Восстановить?" for the address of a removed account.
  "features/auth/ui/LoginForm.vue": ["AccountRestoration"],
  // The notice against a second account: "утратили доступ к аккаунту".
  "features/auth/ui/RegistrationForm.vue": ["AccessRecovery"],
  // Neither the phone, nor the codes, nor the mailbox.
  "features/auth/ui/TwoFactorRemovalForm.vue": ["AccessRecovery"],
  "pages/account/AccountActivationPage.vue": ["RegistrationIssue"],
  // The address is the way back into the account.
  "pages/account/EmailChangePage.vue": ["AccessRecovery"],
  "pages/account/PasswordResetPage.vue": ["AccessRecovery"],
  "pages/account/TwoFactorRemovalCancelPage.vue": ["AccessRecovery"],
  "pages/account/TwoFactorRemovalPage.vue": [
    "AccessRecovery",
    "AccessRecovery",
  ],
  // No support reason is about a name, and the reader keeps his access: what
  // is left is the general one.
  "pages/account/UsernameChangePage.vue": ["Bug"],
  // The rights of the data subject (clause 7) and the questions about the
  // processing of personal data (clause 11).
  "pages/legal/PrivacyPolicyPage.vue": ["PersonalData", "PersonalData"],
  // Clause 9.2: the account is removed on request, through this form; and a
  // question about the agreement itself (clause 12).
  "pages/legal/UserAgreementPage.vue": ["AccountRemoval", "AgreementQuestion"],
  // "О себе" of the profile "Аккаунт удален": a returning author finds his
  // posts there and asks for his name back over them.
  "pages/profile/ProfileRemovedAccountAbout.vue": ["AccountRestoration"],
  // A vulnerability is reported before it is looked for: a found fault is a
  // bug report. A second account made by mistake is reported to be removed.
  "pages/rules/RulesPage.vue": ["Bug", "AccountRemoval"],
};

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (
      (name.endsWith(".vue") || name.endsWith(".ts")) &&
      !name.endsWith(".spec.ts") &&
      !name.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
};

const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** The address of the support page with a query, spelled out by hand. */
const HANDWRITTEN = /["'`]\/support\?/;

/** A call of the builder with its argument, whatever the quotes. */
const CALL = /supportFor\(\s*(["'])([^"']*)\1\s*\)/g;

/** A call whose argument is not a plain literal this walk can read. */
const ANY_CALL = /supportFor\(/g;

const sources = collect(CLIENT_SRC).map((file) => ({
  at: where(file),
  text: readFileSync(file, "utf8"),
}));

/** The doors found in the sources, as the table above spells them. */
const found: Record<string, TicketSubtype[]> = {};
const unreadable: string[] = [];
for (const { at, text } of sources) {
  if (at === BUILDER) continue;
  const reasons = [...text.matchAll(CALL)].map(
    (match) => match[2] as TicketSubtype,
  );
  // An import names the builder without calling it.
  const calls = [...text.matchAll(ANY_CALL)].length;
  if (calls !== reasons.length) unreadable.push(at);
  if (reasons.length > 0) found[at] = reasons;
}

const options = () => ({
  global: { plugins: [createPinia()], components: { Form, FormField } },
});

/** The reason the picker currently shows. */
const chosenReason = (wrapper: ReturnType<typeof mount>) =>
  (wrapper.find("select").element as HTMLSelectElement).value;

/** The query supportFor writes, as the router would hand it to the page. */
const queryOf = (address: string): Record<string, string> =>
  Object.fromEntries(new URL(address, window.location.origin).searchParams);

describe("a link to the support form", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  it("is read from a tree that has them", () => {
    // A walk that found nothing would pass every assertion below in silence.
    expect(sources.length).toBeGreaterThan(250);
    expect(Object.keys(found).length).toBeGreaterThan(5);
  });

  it("is built by supportFor and never spelled out by hand", () => {
    const handwritten = sources
      .filter(({ at, text }) => at !== BUILDER && HANDWRITTEN.test(text))
      .map(({ at }) => at);

    expect(handwritten).toEqual([]);
  });

  it("names its reason as a literal", () => {
    expect(unreadable).toEqual([]);
  });

  it("names the reason the reader of that page comes for", () => {
    expect(found).toEqual(DOORS);
  });

  it.each([...new Set(Object.values(DOORS).flat())])(
    "opens the form on %s",
    (subtype) => {
      route.query = queryOf(supportFor(subtype));

      expect(chosenReason(mount(SupportPage, options()))).toBe(subtype);
    },
  );
});

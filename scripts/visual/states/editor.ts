import { button, type State } from "./shared";

export const EDITOR_STATES: State[] = [
  // Editor
  {
    name: "moderator-editor-toolbar",
    role: "moderator",
    url: "/forum/general/4",
  },
  {
    // The check of the markup under the field, after the field loses the focus.
    name: "member-editor-private-warning",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      await button(page, "Режим BBCode").click();
      await page
        .getByRole("textbox", { name: "Написать комментарий..." })
        .fill("[private]тайна[/private]");
      await page.keyboard.press("Tab");
    },
  },
  {
    name: "member-editor-link-bad-address",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить ссылку" })
        .last()
        .click();
      await page
        .getByRole("dialog")
        .getByLabel("URL ссылки")
        .fill("https://exa mple.com");
      await page.keyboard.press("Tab");
    },
  },
  {
    name: "member-editor-image-size-refused",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вставить изображение" })
        .last()
        .click();
      await page.locator('input[id$="-width"]').last().fill("10000");
      await page.keyboard.press("Tab");
    },
  },
  {
    // The draft of the editor lives in the browser: typing writes nothing to the server.
    name: "guest-support-editor-typing",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("тест");
    },
  },
  {
    name: "guest-support-editor-draft",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("тест");
      await page.waitForTimeout(2000);
      await page.reload({ waitUntil: "load" });
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.getByText("Есть черновик").waitFor();
    },
  },
];

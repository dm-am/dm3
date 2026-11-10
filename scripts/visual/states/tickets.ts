import { button, DEAD, GUEST_TICKET_TOKEN, type State } from "./shared";

export const TICKETS_STATES: State[] = [
  // Tickets
  {
    name: "guest-ticket-track-old-link",
    role: "guest",
    url: `/support/track/${GUEST_TICKET_TOKEN}`,
  },
  {
    // The answer of the server is made up, so no ticket is created.
    name: "guest-support-sent",
    role: "guest",
    url: "/support",
    act: async (page) => {
      await page.route(/\/v1\/tickets$/, (route) =>
        route.request().method() === "POST"
          ? route.fulfill({
              status: 201,
              contentType: "application/json",
              body: JSON.stringify({
                ticketId: "00000000-0000-0000-0000-0000000000d1",
                trackingToken: DEAD,
              }),
            })
          : route.fallback(),
      );
      await page.locator("#subtype").selectOption({ label: "Ошибка" });
      await page.locator("#contact").fill("guest@test.local");
      await page.getByRole("textbox").last().click();
      await page.keyboard.type("Тест");
      await button(page, "Отправить").click();
    },
  },
  {
    // The queue narrowed to one type of ticket.
    name: "moderator-tickets-subtype",
    role: "moderator",
    url: "/moderation/tickets",
    act: async (page) => {
      await page
        .locator("#ticket-subtype")
        .selectOption({ label: "Жалоба на пользователя" });
    },
  },
];

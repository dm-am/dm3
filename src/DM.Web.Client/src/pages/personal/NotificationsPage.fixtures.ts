/** The notifications page over a doubled list and count, and the readings its specs share. */
import { vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { notificationApi, useNotificationStore } from "@/entities/notification";
import {
  NotificationType,
  type NotificationDetail,
  type UserNotification,
} from "@/shared/api/models/notifications";
import { useToast } from "@/shared/lib/composables/useToast";
import { useAuthStore } from "@/shared/stores/auth";
import NotificationsPage from "./NotificationsPage.vue";

/** The page's size with no choice of the reader; a full page of strangers is the drift signal. */
export const PAGE = 20;

/** The space that keeps a label with its value. */
export const NBSP = "\u00A0";

export const detail = (
  label: string,
  value: string,
  isMoment = false,
  isDestination = false,
): NotificationDetail =>
  ({ label, value, isMoment, isDestination }) as NotificationDetail;

/** The fields of a row a test sets, as plain values. */
type RowFields = {
  eventType?: NotificationType;
  createdUtc?: string;
  isRead?: boolean;
  title?: string;
  url?: string;
  details?: NotificationDetail[];
  payload?: Record<string, unknown>;
};

/** One row as the server answers it: the title is the id; it leads somewhere and is unread. */
export const item = (id: string, extra: RowFields = {}): UserNotification =>
  ({
    id,
    eventType: NotificationType.NewMessage,
    payload: {},
    createdUtc: "2026-10-12T08:43:00Z",
    isRead: false,
    title: id,
    url: `/somewhere/${id}`,
    details: [],
    ...extra,
  }) as UserNotification;

export const items = (prefix: string, count: number) =>
  Array.from({ length: count }, (_, i) => item(`${prefix}${i}`));

export const answer = (resources: UserNotification[]) =>
  ({ data: { resources }, error: null }) as never;

/** A problem document with the fields the page reads. */
export const refused = (status: number, title = "") =>
  ({
    data: null,
    error: { type: "about:blank", title, status, traceId: "t" },
  }) as never;

/** The page mounted and its first read settled; `unread` is the bell's count on opening. */
export async function render(
  firstPage: UserNotification[],
  firstAnswer: unknown = answer(firstPage),
  unread = 0,
  pageSize?: number,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The reader's page size of lists, from the account settings; a guest's otherwise.
  if (pageSize)
    useAuthStore().user = {
      username: "reader",
      settings: { pageSize: pageSize },
    } as never;
  const store = useNotificationStore();
  // Before the mount: the page re-reads its list when the count grows.
  store.unreadCount = unread;
  const server = { unread };
  const fetchUnreadCount = vi
    .spyOn(store, "fetchUnreadCount")
    .mockImplementation(async () => {
      store.unreadCount = server.unread;
    });
  const getNotifications = vi
    .spyOn(notificationApi, "getNotifications")
    .mockResolvedValue(firstAnswer as never);
  const markAsRead = vi
    .spyOn(notificationApi, "markAsRead")
    .mockImplementation(async (id?: string) => {
      if (id === undefined) server.unread = 0;
      else server.unread = Math.max(0, server.unread - 1);
      return { data: null, error: null } as never;
    });

  const wrapper = mount(NotificationsPage, {
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: {
        // The heading the line puts the focus on.
        PageTitle: { template: "<h1><slot /></h1>" },
        RouterLink: RouterLinkStub,
      },
    },
  });
  mounted.push(wrapper);
  await flushPromises();
  return { wrapper, store, getNotifications, markAsRead, fetchUnreadCount };
}

const mounted: ReturnType<typeof mount>[] = [];

/** Takes every page mounted by the test out of the document. */
export function unmountAll() {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
}

export type Wrapper = ReturnType<typeof mount>;

/** What the eye reads of an element: the text a screen reader alone hears left out. */
export function seen(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy
    .querySelectorAll(".visually-hidden")
    .forEach((hidden) => hidden.remove());
  // Ordinary white space only: a no-break space is part of what is said.
  return copy.textContent!.replace(/[ \t\r\n]+/g, " ").trim();
}

/** The list as drawn, in order: each row is told by its title. */
export const rows = (wrapper: Wrapper) =>
  wrapper.findAll(".notification-title").map((title) => seen(title.element));

/** The rows whose title is bold, by title. */
export const unreadRows = (wrapper: Wrapper) =>
  wrapper
    .findAll(".notification-title--unread")
    .map((title) => seen(title.element));

/** The title of the row told by its title. */
export const titleOf = (wrapper: Wrapper, title: string) => {
  const found = wrapper
    .findAll(".notification-title")
    .find((one) => seen(one.element) === title);
  if (!found) throw new Error(`no row titled ${title}`);
  return found;
};

/** Raise the count the way an arrival does, and let the page react. */
export async function arrive(
  store: ReturnType<typeof useNotificationStore>,
  n = 1,
) {
  store.unreadCount += n;
  await flushPromises();
}

/** "Отметить прочитанными" of the line under the heading, pressed. */
export async function markAll(wrapper: Wrapper) {
  await wrapper.get(".unread-mark").trigger("click");
  await flushPromises();
}

/** The line under the heading as a reader sees it. */
export function unreadLine(wrapper: Wrapper): string | null {
  const line = wrapper.find(".unread-line");
  if (!line.exists()) return null;
  return line.element.textContent!.replace(/\s+/g, " ").trim();
}

/** Everything the site is currently saying in a floating message. */
export const messages = () => useToast().toasts.value.map((t) => t.message);

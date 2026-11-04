import type { ListEnvelope, UnreadCount } from "@/shared/api/models/common";
import type { UserNotification } from "@/shared/api/models/notifications";
import { Api } from "@/shared/api";

const BASE_PATH = "users/me/notifications";

export default new (class NotificationApi {
  public getNotifications(skip = 0, take = 20) {
    return Api.get<ListEnvelope<UserNotification>>(
      `${BASE_PATH}?skip=${skip}&take=${take}`,
    );
  }

  public getUnreadCount() {
    return Api.get<UnreadCount>(`${BASE_PATH}/unread`);
  }

  /** Without an id, all of them. */
  public markAsRead(id?: string) {
    return id
      ? Api.delete(`${BASE_PATH}/${id}/unread`)
      : Api.delete(`${BASE_PATH}/unread`);
  }
})();

import type { ListEnvelope } from "@/shared/api/models/common";
import type {
  UserNotification,
  NotificationCount,
} from "@/shared/api/models/notifications";
import { Api } from "@/shared/api";

export default new (class NotificationApi {
  private basePath = "users/me/notifications";

  /**
   * Get notifications for the current user
   * @param skip Number of notifications to skip (default 0)
   * @param take Number of notifications to take (default 20, max 100)
   */
  public getNotifications(skip = 0, take = 20) {
    return Api.get<ListEnvelope<UserNotification>>(
      `${this.basePath}?skip=${skip}&take=${take}`,
    );
  }

  /**
   * Get unread notifications count
   */
  public getUnreadCount() {
    return Api.get<NotificationCount>(`${this.basePath}/unread`);
  }

  /**
   * Mark notification(s) as read
   * @param id Optional notification id. If not provided, marks all as read.
   */
  public markAsRead(id?: string) {
    return id
      ? Api.delete(`${this.basePath}/${id}/unread`)
      : Api.delete(`${this.basePath}/unread`);
  }
})();

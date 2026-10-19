import type {
  Envelope,
  ListEnvelope,
  CursorEnvelope,
  Message,
} from "@/shared/api/models/common";
import type {
  Room,
  RoomAccess,
  RoomAccessGrant,
  PostPendency,
  CreateRoomInput,
  PostPendencyInput,
} from "../model/roomTypes";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

const roomApi = {
  getRooms(gameId: string) {
    return Api.get<ListEnvelope<Room>>(`games/${gameId}/rooms`);
  },

  createRoom(gameId: string, room: CreateRoomInput) {
    return Api.post<Envelope<Room>>(`games/${gameId}/rooms`, room);
  },

  /** previousRoomId moves the room after that one, or first when null. */
  updateRoom(
    id: string,
    room: Partial<Room> & { previousRoomId?: string | null },
  ) {
    return Api.patch<Envelope<Room>>(`rooms/${id}`, room);
  },

  deleteRoom(id: string, options?: RequestOptions) {
    return Api.delete(`rooms/${id}`, undefined, options);
  },

  archiveRoom(id: string) {
    return Api.patch<Envelope<Room>>(`rooms/${id}`, { isArchived: true });
  },

  createRoomAccess(roomId: string, access: RoomAccessGrant) {
    return Api.post<Envelope<RoomAccess>>(`rooms/${roomId}/accesses`, access);
  },

  deleteRoomAccess(accessId: string, options?: RequestOptions) {
    return Api.delete(`rooms/accesses/${accessId}`, undefined, options);
  },

  createPendency(roomId: string, pendency: PostPendencyInput) {
    return Api.post<Envelope<PostPendency>>(
      `rooms/${roomId}/pendencies`,
      pendency,
    );
  },

  deletePendency(pendencyId: string) {
    return Api.delete(`rooms/pendencies/${pendencyId}`);
  },

  /** Paged by cursor, not by skip and take. */
  getChatMessages(id: string, cursor?: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chat-rooms/${id}/messages`, {
      cursor,
      limit,
    });
  },

  sendChatMessage(id: string, text: string) {
    return Api.post<Envelope<Message>>(`chat-rooms/${id}/messages`, { text });
  },

  markChatRoomRead(id: string) {
    return Api.delete(`chat-rooms/${id}/messages/unread`);
  },
};

export default roomApi;

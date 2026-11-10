import { vi } from "vitest";

// The client a chat room of a game reads.
export const mockGetChatMessages = vi.fn();
export const mockSendChatMessage = vi.fn();
export const mockMarkChatRoomRead = vi.fn();
export const mockGetRooms = vi.fn();

export const roomApi = () => ({
  default: {
    getChatMessages: mockGetChatMessages,
    sendChatMessage: mockSendChatMessage,
    markChatRoomRead: mockMarkChatRoomRead,
    getRooms: mockGetRooms,
  },
});

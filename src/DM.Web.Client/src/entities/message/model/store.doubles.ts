// The useMessagingStore specs hand this to vi.mock and read the mocks back.
import { vi } from "vitest";

export const mockGetChat = vi.fn();
export const mockGetChats = vi.fn();
export const mockGetMessages = vi.fn();
export const mockGetMessagesBefore = vi.fn();
export const mockGetMessagesAfter = vi.fn();
export const mockGetUnreadCount = vi.fn();
export const mockMarkAsRead = vi.fn();
export const mockSendMessage = vi.fn();
export const mockClearHistory = vi.fn();
export const mockLeaveChat = vi.fn();

export const api = {
  getChat: mockGetChat,
  getChats: mockGetChats,
  getMessages: mockGetMessages,
  getMessagesBefore: mockGetMessagesBefore,
  getMessagesAfter: mockGetMessagesAfter,
  getUnreadCount: mockGetUnreadCount,
  markAsRead: mockMarkAsRead,
  sendMessage: mockSendMessage,
  clearHistory: mockClearHistory,
  leaveChat: mockLeaveChat,
};

export const messagingApi = () => ({ default: api });

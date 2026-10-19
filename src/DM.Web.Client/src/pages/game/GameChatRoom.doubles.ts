import { vi } from "vitest";

// The client a chat room of a game reads, and the router at its address.
export const mockGetChatMessages = vi.fn();
export const mockSendChatMessage = vi.fn();
export const mockMarkChatRoomRead = vi.fn();
export const mockGetRooms = vi.fn();
export const mockReplace = vi.fn();

// Reactive params: a test moves the reader to another room of the game.
export const route = {
  params: { id: "g-1", num: "1" } as Record<string, string>,
  get path() {
    return `/game/${this.params.id}/chat-rooms/${this.params.num}`;
  },
  query: {} as Record<string, string>,
  hash: "",
};

export const roomApi = () => ({
  default: {
    getChatMessages: mockGetChatMessages,
    sendChatMessage: mockSendChatMessage,
    markChatRoomRead: mockMarkChatRoomRead,
    getRooms: mockGetRooms,
  },
});

export const vueRouter = async () => {
  const { reactive } = await import("vue");
  route.params = reactive(route.params);
  return {
    useRoute: () => route,
    useRouter: () => ({ push: vi.fn(), replace: mockReplace }),
    RouterLink: { template: "<a><slot /></a>" },
  };
};

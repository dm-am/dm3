import { vi } from "vitest";

// The clients a room of posts reads, and the router at room 1 of game g-1.
export const mockGetRooms = vi.fn();
export const mockGetPosts = vi.fn();
export const mockGetCharacters = vi.fn();
export const mockMarkRoomAsRead = vi.fn();
export const mockCreatePendency = vi.fn();
export const mockDeletePendency = vi.fn();
export const mockGetFirstUnreadRoomPost = vi.fn();
export const mockCreatePost = vi.fn();
export const mockReplace = vi.fn();

export const roomApi = () => ({
  default: {
    getRooms: mockGetRooms,
    createPendency: mockCreatePendency,
    deletePendency: mockDeletePendency,
  },
});

export const postApi = () => ({
  default: {
    getPosts: mockGetPosts,
    markRoomAsRead: mockMarkRoomAsRead,
    getFirstUnreadRoomPost: mockGetFirstUnreadRoomPost,
    createPost: mockCreatePost,
  },
});

export const characterApi = () => ({
  default: { getCharacters: mockGetCharacters },
});

export const vueRouter = () => ({
  useRoute: () => ({
    params: { id: "g-1", num: "1" },
    query: {},
    hash: "",
    path: "/game/g-1/rooms/1",
  }),
  useRouter: () => ({ push: vi.fn(), replace: mockReplace }),
  RouterLink: { template: "<a><slot /></a>" },
});

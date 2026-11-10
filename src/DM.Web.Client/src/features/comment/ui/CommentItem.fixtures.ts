import type { VueWrapper } from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import type { Comment } from "@/shared/api/models/common/comment";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import CommentItem from "./CommentItem.vue";
import {
  submitDelete,
  submitEdit,
  submitLike,
  submitUnlike,
} from "./CommentItem.doubles";

export const comment = (over: Partial<Comment> = {}): Comment =>
  ({
    id: "c-1",
    text: "<strong>Правила</strong>",
    createdUtc: new Date().toISOString(),
    modifiedUtc: null,
    author: {
      username: "SolohinLex",
      role: "RegularUser",
      lastActivityUtc: null,
    },
    likes: [],
    isRemoved: false,
    ...over,
  }) as unknown as Comment;

export const refused = (status: number, title = "") => ({
  data: null,
  error: { type: "", title, status, traceId: "t" },
});

export function freshComment() {
  localStorage.clear();
  localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
  for (const answer of [submitEdit, submitDelete, submitLike, submitUnlike]) {
    answer.mockReset().mockResolvedValue({ error: null });
  }
  // Error toasts never auto-dismiss and deduplicate by text.
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
}

// Signed in as this reader (the auth store seeds itself from localStorage).
export const signInAs = (username: string, role?: string) =>
  localStorage.setItem("user", JSON.stringify({ username, role }));

const ROUTES: Address[] = [
  ["profile", "/users/:username"],
  ["forum", "/forum"],
  ["topic", "/forum/:forum/:id"],
];

// The address is the list the comment stands in: its query is the reader's view.
export const mountItem = async (
  props: Record<string, unknown>,
  address = "/forum",
) => (await mountPage(CommentItem, address, { routes: ROUTES, props })).wrapper;

export const mountComponent = (
  fetchEditSource: (id: string) => Promise<unknown>,
  over: Partial<Comment> = {},
  extra: Record<string, unknown> = {},
  address = "/forum",
) =>
  mountItem(
    {
      comment: comment(over),
      fetchEditSource: fetchEditSource as never,
      submitEdit: submitEdit as never,
      submitDelete: submitDelete as never,
      submitLike: submitLike as never,
      submitUnlike: submitUnlike as never,
      ...extra,
    },
    address,
  );

export const plates = () => useToast().toasts.value.map((t) => t.message);

export const LINE = ".action-result";

export const action = (wrapper: VueWrapper, word: string) => {
  const found = wrapper
    .findAll(".caption-action")
    .find((one) => one.text() === word);
  if (!found) throw new Error(`"${word}" is not in the caption line`);
  return found;
};

export const LIKE_BUTTON = "button.like-badge";

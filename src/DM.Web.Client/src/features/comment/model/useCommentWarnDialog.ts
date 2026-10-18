import { reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { useModal } from "vue-final-modal";
import { WarningDialog } from "@/features/moderation-actions/@x/comment";
import type { Comment } from "@/shared/api/models/common/comment";
import { commentPermalink } from "./permalink";

/**
 * Moderator warn dialog wiring for a comments page (doc 4.2.4.1) — shared by
 * the forum topic, blog discussion and game comments lists. The warn button
 * itself is gated by rank inside <CommentItem> (canWarn: a moderator warns
 * below his own rank and never an administrator); this composable owns the
 * dialog state and prefill.
 *
 * The window quotes the comment: its text as the list renders it, and its
 * number, the one the comment shows in its corner, linking to it. The link
 * keeps the whole query, the view of the list the moderator warns from, as
 * the number of the comment does (commentPermalink). The number comes with
 * the press, from the item that shows it; without it the quote stands alone.
 */
export function useCommentWarnDialog(
  findComment: (id: string) => Comment | undefined,
) {
  const route = useRoute();

  const username = ref("");
  const entityId = ref<string | undefined>(undefined);
  const entityLink = ref<string | undefined>(undefined);
  const evidence = ref<string | undefined>(undefined);
  const number = ref<number | undefined>(undefined);

  const { open, close } = useModal({
    component: WarningDialog,
    attrs: reactive({
      username,
      entityId,
      entityType: "Comment",
      entityLink,
      evidence,
      number,
      onSuccess: () => close(),
      onCancel: () => close(),
    }),
  });

  function warnComment(id: string, commentNumber?: number) {
    const comment = findComment(id);
    const author = comment?.author?.username;
    if (!comment || !author) return;

    username.value = author;
    entityId.value = id;
    entityLink.value = commentPermalink(id, route.query);
    evidence.value = comment.text;
    number.value = commentNumber;
    open();
  }

  return { warnComment };
}

import { reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { useModal } from "vue-final-modal";
import { WarningDialog } from "@/features/moderation-actions/@x/comment";
import type { Comment } from "@/shared/api/models/common/comment";
import { permalinkOrigin } from "@/shared/config/site";

/**
 * Moderator warn dialog wiring for a comments page (doc 4.2.4.1) — shared by
 * the forum topic, blog discussion and game comments lists. The warn button
 * itself is gated to Moderator+ inside <CommentItem> (canWarn); this
 * composable owns the dialog state and prefill.
 *
 * The prefilled permalink is canonical: current page path + the page number
 * only — active filters are never baked into the link (mirrors the comment
 * permalink button in CommentItem).
 */
export function useCommentWarnDialog(
  findComment: (id: string) => Comment | undefined,
) {
  const route = useRoute();

  const username = ref("");
  const entityId = ref<string | undefined>(undefined);
  const entityLink = ref<string | undefined>(undefined);

  const { open, close } = useModal({
    component: WarningDialog,
    attrs: reactive({
      username,
      entityId,
      entityType: "Comment",
      entityLink,
      onSuccess: () => close(),
      onCancel: () => close(),
    }),
  });

  function warnComment(id: string) {
    const author = findComment(id)?.author?.username;
    if (!author) return;

    const numberParam = route.query.number;
    const search = numberParam ? `?number=${String(numberParam)}` : "";
    username.value = author;
    entityId.value = id;
    entityLink.value =
      permalinkOrigin() + window.location.pathname + search + `#comment-${id}`;
    open();
  }

  return { warnComment };
}

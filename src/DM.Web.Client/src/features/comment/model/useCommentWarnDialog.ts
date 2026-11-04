import { reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { useModal } from "vue-final-modal";
import { WarningDialog } from "@/features/moderation-actions/@x/comment";
import type { Comment } from "@/shared/api/models/common/comment";
import { commentPermalink } from "./permalink";

// The rank gate is CommentItem's (canWarn); the link keeps the whole query, as the number does.
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

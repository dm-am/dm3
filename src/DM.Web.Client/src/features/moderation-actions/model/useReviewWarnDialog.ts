import { reactive, ref } from "vue";
import { useModal } from "vue-final-modal";
import WarningDialog from "../ui/WarningDialog.vue";

/** What a review hands the window: its author and its text as the page draws it. */
type WarnedReview = {
  id: string;
  author: { username: string } | null | undefined;
  text: string | null | undefined;
};

// A review of a game and the text of a post's review are places a warning names, as a comment is.
// The rank gate is the list's; the window quotes the text the reader sees.
export function useReviewWarnDialog(entityType: "GameReview" | "PostReview") {
  const username = ref("");
  const entityId = ref<string | undefined>(undefined);
  const evidence = ref<string | undefined>(undefined);

  const { open, close } = useModal({
    component: WarningDialog,
    attrs: reactive({
      username,
      entityId,
      entityType,
      evidence,
      onSuccess: () => close(),
      onCancel: () => close(),
    }),
  });

  function warn(review: WarnedReview) {
    const author = review.author?.username;
    if (!author) return;

    username.value = author;
    entityId.value = review.id;
    evidence.value = review.text ?? undefined;
    open();
  }

  return { warn };
}

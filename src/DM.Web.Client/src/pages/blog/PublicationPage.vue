<script setup lang="ts">
/**
 * One publication and its discussion, the page the feed, the profile and the
 * notifications lead to. `standalone` says the card is the page; the shell
 * writes the heading from the title announced through useZoneSection. Who may
 * write is the publication's answer about the viewer, and the discussion is not
 * marked read on arrival: the publication has no endpoint for it. The
 * publication is read here, since this page is reachable without the feed.
 */
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { PublicationCard } from "@/features/publication";
import { DiscussionSection } from "@/widgets/discussion";
import { ErrorState } from "@/shared/ui/ErrorState";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { createCommentSection } from "@/shared/lib/composables/createCommentSection";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { describeFailureOrWait } from "@/shared/lib/errors";
import type { CommentsQuery } from "@/shared/api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

const blogId = computed(() => route.params.id as string);
const pubId = computed(() => route.params.pubId as string);

const publication = ref<Publication | null>(null);
const loading = ref(false);
const failure = ref<string | null>(null);

// The name of a publication is data, announced so the heading and the tab
// read "{блог} | {публикация}".
useZoneSection(() => publication.value?.title);

// The discussion belongs to the publication, not to the blog store's one bag
// for the current blog, where it would outlive the publication.
const {
  comments,
  commentsPaging,
  commentsLoading,
  commentsError,
  loadComments,
  updateComment,
  deleteComment,
  likeComment,
  unlikeComment,
} = createCommentSection({
  getComments: (id, query) => blogApi.getPublicationComments(id, query),
  updateComment: (id, comment) => blogApi.updatePublicationComment(id, comment),
  deleteComment: (id, options) => blogApi.deletePublicationComment(id, options),
  likeComment: (id) => blogApi.likePublicationComment(id),
  unlikeComment: (id) => blogApi.unlikePublicationComment(id),
  currentUsername: () => user.value?.username,
});

const LOAD_FAILURE = "Не удалось загрузить публикацию";

/**
 * The card below answers a failed read, so the read owns the refusal; an
 * expired session and a dead connection stay with the interceptor.
 */

async function load() {
  const id = pubId.value;
  if (!id) return;
  loading.value = true;
  failure.value = null;

  const { data, error } = await blogApi.getPublication(id, OWNS_REFUSAL);
  if (error) {
    // The shared wording names the seconds the server asked for.
    failure.value = describeFailureOrWait(error, LOAD_FAILURE);
    publication.value = null;
  } else {
    publication.value = data?.resource ?? null;
  }

  loading.value = false;
}

useFetchData(load, [{ param: (p) => p.pubId, callback: () => load() }]);

// The blacklist takes "Редактировать" off the viewer's own comments; the flag
// comes with the blog the shell has loaded.
const { blog } = storeToRefs(useBlogDetailsStore());
const viewerBlacklisted = computed(
  () => !!user.value && !!blog.value?.viewerBlacklisted,
);

// Who may write is the publication's answer about the viewer
// (PublicationCommentService); it is read again when the viewer changes.
const canComment = computed(
  () => !!user.value && !!publication.value?.viewer?.canComment,
);
useViewerChange(() => load());

// In place of the form for the one on the blacklist; what was written stays to read.
const closedHint = computed(() =>
  viewerBlacklisted.value
    ? "Вы в черном списке этого блога: писать комментарии нельзя"
    : undefined,
);

// The guid the server answered with: a hand-written link may carry the readable form.
const discussionId = computed(() => publication.value?.id ?? pubId.value);

const loadDiscussion = (query: CommentsQuery) =>
  loadComments(discussionId.value, query);

const create = (text: string) =>
  blogApi.createPublicationComment(discussionId.value, { text });

const fetchEditSource = (id: string) =>
  blogApi.getPublicationCommentForEdit(id);
</script>

<template>
  <div class="publication-page">
    <GamePostSkeleton v-if="loading && !publication" />

    <ErrorState v-else-if="failure" :message="failure" :retry="load" />

    <!-- The publication, then its discussion -->
    <template v-else-if="publication">
      <PublicationCard :publication="publication" standalone />

      <DiscussionSection
        :comments="comments"
        :paging="commentsPaging"
        :loading="commentsLoading"
        :failed="commentsError"
        :record-id="discussionId"
        :load="loadDiscussion"
        :create="create"
        :submit-edit="updateComment"
        :submit-delete="deleteComment"
        :like="likeComment"
        :unlike="unlikeComment"
        :fetch-edit-source="fetchEditSource"
        :paging-to="{
          name: 'blog-publication',
          params: { id: blogId, pubId },
        }"
        :draft-key="composerDraftKey('publication', 'comment', publication.id)"
        :can-comment="canComment"
        :closed-hint="closedHint"
        :viewer-blacklisted="viewerBlacklisted"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.publication-page
  display: flex
  flex-direction: column
  gap: $medium
  min-height: $grid-step * 50
</style>

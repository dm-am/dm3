<script setup lang="ts">
/**
 * One publication and its discussion (dev doc 4.2.3.6.4).
 *
 * The feed, the profile spotlight and every notification about a publication
 * name one text, and until now none of them could lead to it: the card title
 * was plain text and the notification pointed at the blog. This is the page
 * they lead to — the publication above, its own discussion below.
 *
 * The card is PublicationCard and nothing is drawn around it: a publication is
 * a copy of a topic card by product decision, and when it gets a design of its
 * own the change happens inside that component, not here. `standalone` is what
 * says the card is the page — the blog shell already writes the heading, out of
 * the title announced through useZoneSection.
 *
 * The discussion is the site-wide section, wired the way the blog's own is.
 * Two things differ, and both come from the server. Who may write is the
 * publication's business and not the blog's (PublicationIntentionResolver
 * .CreateComment: signed in, published, comments enabled) — the blog-level
 * commentsEnabled flag does not reach here. And the discussion is not marked
 * read on arrival: the blog has an endpoint for that and the publication has
 * none, so there is nothing honest to call.
 *
 * The publication is fetched here rather than taken from the blog store: the
 * store holds a page of the feed, and this page is reachable without ever
 * opening the feed.
 */
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { PublicationCard } from "@/features/publication";
import { DiscussionSection } from "@/widgets/discussion";
import { ErrorState } from "@/shared/ui/ErrorState";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { createCommentSection } from "@/shared/lib/composables/createCommentSection";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { describeFailure } from "@/shared/lib/errors";
import type { CommentsQuery } from "@/shared/api";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

const blogId = computed(() => route.params.id as string);
const pubId = computed(() => route.params.pubId as string);

const publication = ref<Publication | null>(null);
const loading = ref(false);
const failure = ref<string | null>(null);

// The shell titles its sub-pages out of meta.section, and the name of a
// publication is data — announced here, so the heading and the tab still read
// "{блог} | {публикация}" and still come from one place.
useZoneSection(() => publication.value?.title);

// The discussion slice, the same factory both details stores run. It lives in
// the page and not in the blog store because it belongs to the publication:
// the store is one bag for "the current blog", and a second discussion in it
// would outlive the publication it was loaded for.
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
  deleteComment: (id) => blogApi.deletePublicationComment(id),
  likeComment: (id) => blogApi.likePublicationComment(id),
  unlikeComment: (id) => blogApi.unlikePublicationComment(id),
  currentUsername: () => user.value?.username,
});

const LOAD_FAILURE = "Не удалось загрузить публикацию";

async function load() {
  const id = pubId.value;
  if (!id) return;
  loading.value = true;
  failure.value = null;

  const { data, error } = await blogApi.getPublication(id);
  if (error) {
    failure.value = describeFailure(error, LOAD_FAILURE);
    publication.value = null;
  } else {
    publication.value = data?.resource ?? null;
  }

  loading.value = false;
}

useFetchData(load, [{ param: (p) => p.pubId, callback: () => load() }]);

// The client half of PublicationIntentionResolver.CreateComment. The blacklist
// and the ban are not asked here for the same reason the blog discussion does
// not ask them: a refused POST is the signal, and the ban rule exempts your own
// blog, which no blanket client-side gate can express.
const canComment = computed(
  () =>
    !!user.value &&
    !!publication.value?.isPublished &&
    !!publication.value?.commentsEnabled,
);

// Said instead of the composer to a signed-in reader who still may not write.
// A guest gets the sign-in prompt from the section itself.
const closedHint = computed(() => {
  if (!user.value || canComment.value || !publication.value) return undefined;
  return publication.value.isPublished
    ? "Комментарии к этой публикации отключены"
    : "Черновик обсуждать нельзя, комментарии откроются после публикации";
});

// The discussion runs against the publication guid the server answered with,
// not the id in the URL: the resolver route hands over a guid already, but a
// link written by hand may carry the readable form.
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
    <!-- 1. loading -->
    <GamePostSkeleton v-if="loading && !publication" />

    <!-- 2. error -->
    <ErrorState v-else-if="failure" :message="failure" :retry="load" />

    <!-- 3. content: the publication, then its discussion -->
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

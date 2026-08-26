<script setup lang="ts">
// Blog discussion sub-page (dev doc 4.2.3.6.3 "Обсуждение блога"). The section
// itself is the site-wide one (widgets/discussion); what stays here is the
// blog's own part of it. Access is simpler than a game's: the blog carries a
// single commentsEnabled flag (no public/readonly/private modes), and bans and
// the blog blacklist are enforced server-side — a rejected POST is the signal,
// and the ban rule exempts your own blog, which a blanket client-side gate
// could not express.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { DiscussionSection } from "@/widgets/discussion";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import type { CommentsQuery } from "@/shared/api";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { user } = storeToRefs(useAuthStore());
const { blog, comments, commentsPaging, commentsLoading, commentsError } =
  storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

const commentsEnabled = computed(() => blog.value?.commentsEnabled !== false);

const canComment = computed(() => !!user.value && commentsEnabled.value);

// Said instead of the composer when the blog has closed its discussion.
const closedHint = computed(() =>
  commentsEnabled.value ? undefined : "Комментарии в этом блоге отключены",
);

const load = (query: CommentsQuery) =>
  blogStore.loadComments(blogId.value, query);

const create = (text: string) =>
  blogApi.createBlogComment(blog.value?.id ?? blogId.value, { text });

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => blogApi.getBlogCommentForEdit(id);
const fetchQuoteSource = (id: string) => blogApi.getBlogCommentQuote(id);

async function markAsRead() {
  if (!user.value) return;
  try {
    await blogApi.markBlogCommentsAsRead(blogId.value);
  } catch {
    // Silently ignore - non-critical operation
  }
}
</script>

<template>
  <div class="blog-comments">
    <DiscussionSection
      :comments="comments"
      :paging="commentsPaging"
      :loading="commentsLoading"
      :failed="commentsError"
      :record-id="blogId"
      :load="load"
      :create="create"
      :submit-edit="blogStore.updateComment"
      :submit-delete="blogStore.deleteComment"
      :like="blogStore.likeComment"
      :unlike="blogStore.unlikeComment"
      :fetch-edit-source="fetchEditSource"
      :fetch-quote-source="fetchQuoteSource"
      :paging-to="{ name: 'blog-comments', params: { id: blogId } }"
      :draft-key="composerDraftKey('blog', 'comment', blog?.id)"
      :can-comment="canComment"
      :closed-hint="closedHint"
      @loaded="markAsRead"
    />
  </div>
</template>

<style scoped lang="sass">
.blog-comments
  min-height: $grid-step * 50
</style>

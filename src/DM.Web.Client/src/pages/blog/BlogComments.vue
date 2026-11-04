<script setup lang="ts">
// The blog's part of the site-wide discussion. Who may write is the server's
// answer about the viewer: the blacklist and the ban, with its exemption for
// one's own blog.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { DiscussionSection } from "@/widgets/discussion";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import type { CommentsQuery } from "@/shared/api";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { user } = storeToRefs(useAuthStore());
const { blog, comments, commentsPaging, commentsLoading, commentsError } =
  storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

// The answer is for whoever held the session when the blog was read, and the
// blog shell does not read it again on a sign-in, so this page does.
const canComment = computed(
  () => !!user.value && !!blog.value?.viewer?.canComment,
);
useViewerChange(() => blogStore.loadBlog(blogId.value));

// The blacklist takes "Редактировать" off the viewer's own comments: the
// server refuses that edit by the list and the ban.
const viewerBlacklisted = computed(
  () => !!user.value && !!blog.value?.viewerBlacklisted,
);

// In place of the form for the one on the blacklist; what was written stays to read.
const closedHint = computed(() =>
  viewerBlacklisted.value
    ? "Вы в черном списке этого блога: писать комментарии нельзя"
    : undefined,
);

const load = (query: CommentsQuery) =>
  blogStore.loadComments(blogId.value, query);

const create = (text: string) =>
  blogApi.createBlogComment(blog.value?.id ?? blogId.value, { text });

const fetchEditSource = (id: string) => blogApi.getBlogCommentForEdit(id);

async function markAsRead() {
  if (!user.value) return;
  await blogApi.markBlogCommentsAsRead(blogId.value);
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
      :paging-to="{ name: 'blog-comments', params: { id: blogId } }"
      :draft-key="composerDraftKey('blog', 'comment', blog?.id)"
      :can-comment="canComment"
      :closed-hint="closedHint"
      :viewer-blacklisted="viewerBlacklisted"
      @loaded="markAsRead"
    />
  </div>
</template>

<style scoped lang="sass">
.blog-comments
  min-height: $grid-step * 50
</style>

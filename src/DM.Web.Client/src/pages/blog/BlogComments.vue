<script setup lang="ts">
// Blog discussion sub-page (dev doc 4.2.3.6.3 "Обсуждение блога"). The section
// itself is the site-wide one (widgets/discussion); what stays here is the
// blog's own part of it. Everybody who sees the blog reads its discussion, and
// nothing turns its comments off. Who may write is the server's answer about
// the viewer, asked by the code that refuses the comment: the blacklist and
// the ban at once, the ban with its exemption for one's own blog, which the
// page could not express and used to leave to a refused POST.
import { computed, watch } from "vue";
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

// The answer is for whoever held the session when the blog was read, and the
// blog shell does not read it again on a sign-in the way the game shell does:
// the guest who signs in from "Войдите, чтобы оставить комментарий" would be
// left with neither the prompt nor the form. So this page reads the blog again
// when the viewer changes, and until then a viewer whose session has just
// ended is offered nothing.
const canComment = computed(
  () => !!user.value && !!blog.value?.viewer?.canComment,
);
watch(
  () => user.value?.username ?? null,
  () => blogStore.loadBlog(blogId.value),
);

// The blog's blacklist reaches the comments themselves, where it takes
// "Редактировать" off the viewer's own: the server refuses that edit by the
// list and the ban, so the flag above cannot stand for it.
const viewerBlacklisted = computed(
  () => !!user.value && !!blog.value?.viewerBlacklisted,
);

// The plaque in place of the form for the one on the blog's blacklist: what
// was written stays to read, empty or not.
const closedHint = computed(() =>
  viewerBlacklisted.value
    ? "Вы в черном списке этого блога: писать комментарии нельзя"
    : undefined,
);

const load = (query: CommentsQuery) =>
  blogStore.loadComments(blogId.value, query);

const create = (text: string) =>
  blogApi.createBlogComment(blog.value?.id ?? blogId.value, { text });

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => blogApi.getBlogCommentForEdit(id);

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

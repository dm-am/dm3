<script setup lang="ts">
// Blog discussion sub-page (dev doc 4.2.3.6.3 "Обсуждение блога") — mirrors
// GameComments. Access is simpler than games: the blog carries a single
// commentsEnabled flag (no public/readonly/private access modes); bans and
// the blog blacklist are enforced server-side, the ban hint mirrors the
// game page. Comments are marked as read once the list has loaded.
import { ref, computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useScrollToElement } from "@/shared/lib/composables/useScrollToElement";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { CommentItem, useCommentWarnDialog } from "@/features/comment";
import { LoginPrompt } from "@/features/auth";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { user } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());
const { blog, comments, commentsPaging, commentsLoading, commentsError } =
  storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

// The shared Paging widget reads/writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
function getPage(): number {
  const page = route.query.number;
  return page ? parseInt(page as string) || 1 : 1;
}

// Calculate comment number based on paging
function getCommentNumber(index: number): number {
  if (!commentsPaging.value) return index + 1;
  const offset = (commentsPaging.value.current - 1) * commentsPaging.value.size;
  return offset + index + 1;
}

// --- Single-comment actions (edit / delete / likes / warn) ---
async function handleEdit(id: string, text: string) {
  await blogStore.updateComment(id, text);
}

async function handleDelete(id: string) {
  await blogStore.deleteComment(id);
}

async function handleLike(id: string) {
  await blogStore.likeComment(id);
}

async function handleUnlike(id: string) {
  await blogStore.unlikeComment(id);
}

// Moderator warning (doc 4.2.4.1) — shared dialog wiring.
const { warnComment: handleWarn } = useCommentWarnDialog((id) =>
  comments.value.find((c) => c.id === id),
);

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => blogApi.getBlogCommentForEdit(id);

// Paging scrolls the comments block back into view (not the page top)
const commentsSectionRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return commentsSectionRef.value;
}

// Comment creation state
const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const isModerator = computed(() => userIsModerator(user.value));

// Blogs expose one switch: commentsEnabled. Blacklist and bans are enforced
// server-side — a rejected POST is the signal, and the ban rule exempts your
// own blog, which a blanket client-side gate could not express.
const commentsEnabled = computed(() => blog.value?.commentsEnabled !== false);

const canComment = computed(() => !!user.value && commentsEnabled.value);

// Scroll to target element when comments are loaded
const commentsLoaded = computed(
  () => comments.value.length > 0 && !commentsLoading.value,
);
useScrollToElement(commentsLoaded);

// Mark comments as read when loaded (for authenticated users)
watch(
  commentsLoaded,
  async (loaded) => {
    if (loaded && user.value && blogId.value) {
      try {
        await blogApi.markBlogCommentsAsRead(blogId.value);
      } catch {
        // Silently ignore - non-critical operation
      }
    }
  },
  { once: true },
);

async function handleSend() {
  if (!newComment.value.trim() || sending.value || !blog.value) return;

  const text = newComment.value;
  newComment.value = "";
  editorRef.value?.clear();
  sending.value = true;

  let failed = false;
  try {
    const { error } = await blogApi.createBlogComment(blog.value.id, { text });
    failed = Boolean(error);
    if (!failed) {
      // Reload comments
      await blogStore.loadComments(blog.value.id, getPage());
    }
  } catch {
    failed = true;
  } finally {
    sending.value = false;
  }
  // Give the text back on failure. Clearing before the request is what makes
  // sending feel instant; losing what was written when it fails is not part
  // of that bargain.
  if (failed) {
    newComment.value = text;
  }
}

useFetchData(
  () => blogStore.loadComments(blogId.value, getPage()),
  [
    {
      param: (p) => p.id,
      callback: (id) => blogStore.loadComments(id as string, 1),
    },
  ],
  [
    {
      query: (q) => q.number,
      callback: () => blogStore.loadComments(blogId.value, getPage()),
    },
  ],
);
</script>

<template>
  <div class="blog-comments">
    <block-title>Обсуждение</block-title>

    <!-- Error -->
    <div v-if="commentsError" class="comments-error">
      {{ commentsError }}
    </div>

    <!-- Comments list -->
    <template v-else>
      <!-- Loading -->
      <CommentSkeleton v-if="commentsLoading && comments.length === 0" />

      <div v-else-if="comments.length === 0" class="comments-empty">
        <secondary-text>Комментариев пока нет</secondary-text>
      </div>

      <div v-else ref="commentsSectionRef" class="comments-section">
        <div class="comments-list">
          <CommentItem
            v-for="(comment, index) in comments"
            :key="comment.id"
            :comment="comment"
            :compact="isCompactLayout"
            :number="getCommentNumber(index)"
            :data-id="comment.id"
            :fetch-edit-source="fetchEditSource"
            @edit="handleEdit"
            @delete="handleDelete"
            @like="handleLike"
            @unlike="handleUnlike"
            @warn="handleWarn"
          />
        </div>
      </div>

      <!-- Paging -->
      <Paging
        v-if="commentsPaging"
        :paging="commentsPaging"
        :to="{
          name: 'blog-comments',
          params: { id: blogId },
        }"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />

      <!-- Comment input -->
      <div class="comment-input-wrapper">
        <div class="comment-input-container">
          <template v-if="canComment">
            <BBCodeEditor
              ref="editorRef"
              v-model="newComment"
              context="common"
              placeholder="Написать комментарий..."
              :draft-key="`blog_${blog?.id}_comment`"
              :disabled="sending"
              :min-height="100"
              :max-height="300"
              :resizable="true"
              :is-moderator="isModerator"
              @submit="handleSend"
            />
            <Button
              :loading="sending"
              :disabled="!newComment.trim()"
              @click="handleSend"
            >
              Отправить
            </Button>
          </template>

          <secondary-text v-else-if="!commentsEnabled" class="comment-hint">
            Комментарии в этом блоге отключены
          </secondary-text>
          <LoginPrompt v-else-if="!user" action="оставить комментарий" />
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
.blog-comments
  min-height: $grid-step * 50

.comments-error,
.comments-empty
  padding: $big
  text-align: center

.comments-error
  color: $accent-red

.comments-section
  display: flex
  flex-direction: column
  gap: $small

.comments-list
  display: flex
  flex-direction: column

.comment-input-wrapper
  margin-top: $medium

.comment-input-container
  display: flex
  flex-direction: column
  gap: $small

  :deep(.bbcode-editor-wrapper)
    width: 100%

.comment-hint
  text-align: center
  padding: $small
</style>

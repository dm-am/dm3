<script setup lang="ts">
// Left-sidebar panel for a blog page (mirrors GamePanel). Mounted by
// LeftSidebar on any /blogs/:id route (route.meta.blogZone). It is the
// single home for per-blog navigation and management, driven entirely by
// the shared useBlogDetailsStore (the blog page loads the blog; this panel
// surfaces the mutation actions).
//
// Section order follows the product doc (4.2.1.4 "Панель блога"):
//   {title} / "Статус" / "Рубрики" / "Обсуждение" (N) /
//   "Управление блогом" / "Действия с блогом" / "Модерация блога"
//
// Role gates (never a single blanket "moderator" gate):
//   "Управление блогом" — owner/assistant get the edit items (settings,
//     create publication, status buttons); the blog mentor ("наставник")
//     gets the management header and the notepad for oversight, but not the
//     edit items (mirrors GamePanel, and the backend BlogNotepadService gate).
//   "Действия с блогом" — any authenticated user except the owner
//     (subscribe toggle, via features/blog-actions).
//   "Модерация блога" — premoderation is Mentor+ (global),
//     delete-others-blog is SeniorModerator+ (mirrors the game panel).
//
// Four data-states per UI_STANDARDS: skeleton (initial load), error,
// empty (blog not found), content.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import {
  useBlogDetailsStore,
  BlogPremoderationTransition,
  BlogStatusBadge,
} from "@/entities/blog";
import { BlogStatusButtons, BlogJoinActions } from "@/features/blog-actions";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import BlogRubricLink from "./BlogRubricLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";

const props = defineProps<{ blogId: string }>();

const store = useBlogDetailsStore();
const {
  blog,
  blogLoading,
  blogError,
  rubrics,
  isOwner,
  canManage,
  canUseNotepad,
} = storeToRefs(store);

// Owner/assistant may edit the blog (settings, publications, status).
const canEdit = computed(() => canManage.value);

const { user } = storeToRefs(useAuthStore());
const router = useRouter();
const toast = useToast();

// Blog links use the short public id; fall back to the raw route param until
// the blog resolves (the blog endpoints are publicId-tolerant: 5-letter
// public id or GUID).
const routeId = computed(() => blog.value?.publicId ?? props.blogId);

// --- Actions with the blog ---
// Any authenticated user except the owner may act on the blog (subscribe).
// The owner manages via the sections above instead.
const canActOnBlog = computed(() => !!user.value && !isOwner.value);

// --- Moderation (global roles) ---

const globalRoles = computed<UserRole[]>(() => {
  const u = user.value;
  if (!u) return [];
  return u.role ? [u.role] : [];
});
const isGlobalMentor = computed(() =>
  globalRoles.value.some((r) =>
    [
      UserRole.Mentor,
      UserRole.Moderator,
      UserRole.SeniorModerator,
      UserRole.Admin,
    ].includes(r),
  ),
);
const isGlobalSeniorModerator = computed(() =>
  globalRoles.value.some((r) =>
    [UserRole.SeniorModerator, UserRole.Admin].includes(r),
  ),
);
const showModeration = computed(
  () => isGlobalMentor.value || isGlobalSeniorModerator.value,
);

// Confirmed moderation actions share a single dialog (mirrors GamePanel).
interface ModAction {
  title: string;
  message: string;
  confirmLabel: string;
  danger?: boolean;
  run: () => Promise<void>;
}
const pendingMod = ref<ModAction | null>(null);

function askPremod(t: BlogPremoderationTransition) {
  const send = t === BlogPremoderationTransition.SendToPremoderation;
  pendingMod.value = {
    title: send ? "Премодерация" : "Снятие с премодерации",
    message: send
      ? "Отправить блог на премодерацию?"
      : "Снять блог с премодерации?",
    confirmLabel: send ? "Отправить" : "Снять",
    run: async () => {
      const ok = await store.changePremoderation(t);
      if (!ok) toast.error("Не удалось изменить премодерацию");
    },
  };
}

function askDelete() {
  pendingMod.value = {
    title: "Удаление блога",
    message: "Удалить этот блог? Действие необратимо.",
    confirmLabel: "Удалить блог",
    danger: true,
    run: async () => {
      const ok = await store.deleteBlog();
      if (ok) router.push({ name: "blogs" });
      else toast.error("Не удалось удалить блог");
    },
  };
}

async function confirmMod() {
  const action = pendingMod.value;
  pendingMod.value = null;
  if (action) await action.run();
}
</script>

<template>
  <SidebarBlock token="BlogPanel">
    <template #title>
      <router-link
        v-if="blog"
        class="title-link"
        :to="{ name: 'blog', params: { id: routeId } }"
      >
        {{ blog.title }}
      </router-link>
      <template v-else>Блог</template>
    </template>

    <!-- 1. loading -->
    <SidebarSkeleton v-if="blogLoading && !blog" :lines="8" />

    <!-- 2. error -->
    <SecondaryText v-else-if="blogError" class="error">
      {{ blogError }}
    </SecondaryText>

    <!-- 3. content -->
    <template v-else-if="blog">
      <!-- Status: duplicated in the panel so the owner sees the blog's
           state next to the status-transition buttons. The premoderation
           status is not exposed by the blog API — that line will appear
           together with the field. -->
      <SecondaryText class="status-line">
        Статус: <BlogStatusBadge :status="blog.status" />
      </SecondaryText>

      <!-- "Рубрики" section -->
      <div class="section-title">Рубрики</div>
      <template v-if="rubrics.length">
        <BlogRubricLink
          v-for="rubric in rubrics"
          :key="rubric.id"
          :rubric="rubric"
          :blog-id="routeId"
        />
      </template>
      <SecondaryText v-else>Рубрик пока нет</SecondaryText>

      <!-- "Обсуждение" — blog discussion. The heading links to the comments
           page; N is the unread comment count (mirrors GamePanel). -->
      <div class="section-title">
        <router-link
          class="section-link"
          :to="{ name: 'blog-comments', params: { id: routeId } }"
        >
          Обсуждение<span v-if="blog.unreadCommentsCount">
            ({{ blog.unreadCommentsCount }})</span
          >
        </router-link>
      </div>

      <!-- "Управление блогом" section: edit items for owner/assistant, plus
           the notepad for the mentor's oversight (mirrors GamePanel). -->
      <template v-if="canEdit || canUseNotepad">
        <div class="section-title">Управление блогом</div>
        <template v-if="canEdit">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{ name: 'blog-settings', params: { id: routeId } }"
            >
              Настройки блога
            </router-link>
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{ name: 'blog-publication-create', params: { id: routeId } }"
            >
              Создать публикацию
            </router-link>
          </li>
          <!-- Status transition buttons (owner/assistant); placed between the
               edit items and the notepad link to mirror GamePanel's order. -->
          <BlogStatusButtons variant="strip" />
        </template>
        <li v-if="canUseNotepad" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'blog-notepad', params: { id: routeId } }">
            Заметки блога
          </router-link>
        </li>
      </template>

      <!-- "Действия с блогом" section (any authed except owner) -->
      <template v-if="canActOnBlog">
        <div class="section-title">Действия с блогом</div>
        <BlogJoinActions variant="strip" />
      </template>

      <!-- "Модерация блога" section (global roles) -->
      <template v-if="showModeration">
        <div class="section-title">Модерация блога</div>
        <template v-if="isGlobalMentor">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="
                askPremod(BlogPremoderationTransition.SendToPremoderation)
              "
            >
              Отправить на премодерацию
            </button>
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="
                askPremod(BlogPremoderationTransition.RemoveFromPremoderation)
              "
            >
              Снять с премодерации
            </button>
          </li>
        </template>
        <li v-if="isGlobalSeniorModerator" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button type="button" class="strip-action danger" @click="askDelete">
            Удалить блог
          </button>
        </li>
      </template>
    </template>

    <!-- 4. empty / not found -->
    <SecondaryText v-else>Блог не найден</SecondaryText>

    <ConfirmDialog
      :show="!!pendingMod"
      :title="pendingMod?.title ?? ''"
      :message="pendingMod?.message ?? ''"
      :confirm-label="pendingMod?.confirmLabel"
      :danger="pendingMod?.danger"
      @confirm="confirmMod"
      @cancel="pendingMod = null"
    />
  </SidebarBlock>
</template>

<style scoped lang="sass">
// The panel heading is itself a navigation link to the blog info page
// (product doc 4.2.1.4). Inherits the block heading look; underlines on
// hover like other strip links.
.title-link
  color: $heading-alt

  &:hover
    color: $link-hover
    text-decoration: underline

.status-line
  display: block
  margin-bottom: $tiny

.section-title
  margin: $small 0 $tiny
  font-size: $secondary-font-size
  font-weight: bold
  color: $text-muted
  text-transform: uppercase
  letter-spacing: 0.3px

// A section header that is itself a navigation link ("Обсуждение").
.section-link
  color: $text-muted

  &:hover
    color: $link-hover
    text-decoration: underline

.link
  display: block

.muted
  color: $text-muted

// Only the decorative "- " prefix (aria-hidden) is excluded from selection;
// informative muted text must stay selectable.
.muted[aria-hidden="true"]
  user-select: none

.error
  color: $accent-red

// Strip actions mimic a plain sidebar link but trigger a store mutation.
.strip-action
  padding: 0
  border: none
  background: none
  font: inherit
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover
    text-decoration: underline

  &.danger
    color: $accent-red
</style>

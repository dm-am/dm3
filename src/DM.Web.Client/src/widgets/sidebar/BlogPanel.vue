<script setup lang="ts">
// Left-sidebar "Меню блога" panel for a blog page, built to the same plan as
// GamePanel. Mounted by LeftSidebar on any /blogs/:id route
// (route.meta.blogZone). It is the single home for per-blog navigation and
// management, driven entirely by the shared useBlogDetailsStore (the blog
// page loads the blog, the rubrics ride inside it; this panel surfaces the
// mutation actions).
//
// Menu order:
//   "Рубрики" / "Информация" / "Лента публикаций" / "Обсуждение" /
//   "Управление блогом" / "Действия с блогом" / "Модерация блога"
//
// Rubrics nest under their group row and drop their own "- " prefix; the
// indent is the nesting. Every navigation row carries a counter, zero
// included, and a counter is grey down to its brackets (SidebarCounter for a
// single number, CounterPair for the rubric's "(N/A)"). Row labels are the
// names their routes carry, so the menu and the page it opens agree.
//
// Role gates (never a single blanket "moderator" gate):
//   "Управление блогом" — owner/assistant get the edit items (settings,
//     create publication, status buttons); the blog mentor ("наставник")
//     gets the management header and the notepad for oversight, but not the
//     edit items (the backend BlogNotepadService gate).
//     The settings row deliberately does NOT widen the way GamePanel's did:
//     BlogIntention.EditSettings is owner + assistants + senior moderation and
//     leaves the blog mentor out, because unlike the game curator he only
//     approves publications. Senior moderation is admitted by the intention
//     and still gets no row — staff powers are not interface copy.
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
} from "@/entities/blog";
import { BlogStatusButtons, BlogJoinActions } from "@/features/blog-actions";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarCounter from "./SidebarCounter.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import BlogRubricLink from "./BlogRubricLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { notifyFailure } from "@/shared/lib/errors";

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
      const error = await store.changePremoderation(t);
      if (error) notifyFailure(error, "Не удалось изменить премодерацию");
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
      const error = await store.deleteBlog();
      if (error) notifyFailure(error, "Не удалось удалить блог");
      else router.push({ name: "blogs" });
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
  <SidebarBlock token="BlogPanel" title="Меню блога">
    <!-- 1. loading -->
    <SidebarSkeleton v-if="blogLoading && !blog" :lines="8" />

    <!-- 2. error -->
    <li v-else-if="blogError">
      <SecondaryText class="error">{{ blogError }}</SecondaryText>
    </li>

    <!-- 3. content -->
    <template v-else-if="blog">
      <!-- "Рубрики" — the group row; the blog's rubrics follow it indented.
           They ride inside the blog itself, so this list has no loading or
           error state of its own (a game fetches its rooms separately). -->
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>Рубрики
      </li>
      <li v-if="rubrics.length">
        <ul class="rubric-list">
          <BlogRubricLink
            v-for="rubric in rubrics"
            :key="rubric.id"
            :rubric="rubric"
            :blog-id="routeId"
            prefix=""
          />
        </ul>
      </li>
      <li v-else><SecondaryText>Рубрик пока нет</SecondaryText></li>

      <!-- Blog navigation — a flat list of links, each carrying its counter. -->
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'blog', params: { id: routeId } }"
          >Информация</router-link
        >
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'blog-feed', params: { id: routeId } }"
          >Лента публикаций</router-link
        ><SidebarCounter :value="blog.unreadPublicationsCount ?? 0" />
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'blog-comments', params: { id: routeId } }"
          >Обсуждение</router-link
        ><SidebarCounter :value="blog.unreadCommentsCount ?? 0" />
      </li>

      <!-- "Управление блогом" section: edit items for owner/assistant, plus
           the notepad for the mentor's oversight (mirrors GamePanel). -->
      <template v-if="canEdit || canUseNotepad">
        <SidebarSectionTitle>Управление блогом</SidebarSectionTitle>
        <template v-if="canEdit">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{ name: 'blog-settings', params: { id: routeId } }"
              >Настройки блога</router-link
            >
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{ name: 'blog-publication-create', params: { id: routeId } }"
              >Создать публикацию</router-link
            >
          </li>
          <!-- Status transition buttons (owner/assistant); placed between the
               edit items and the notepad link to mirror GamePanel's order. -->
          <BlogStatusButtons variant="strip" />
        </template>
        <li v-if="canUseNotepad" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'blog-notepad', params: { id: routeId } }"
            >Заметки блога</router-link
          >
        </li>
      </template>

      <!-- "Действия с блогом" section (any authed except owner) -->
      <template v-if="canActOnBlog">
        <SidebarSectionTitle>Действия с блогом</SidebarSectionTitle>
        <BlogJoinActions variant="strip" />
      </template>

      <!-- "Модерация блога" section (global roles) -->
      <template v-if="showModeration">
        <SidebarSectionTitle>Модерация блога</SidebarSectionTitle>
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
    <li v-else><SecondaryText>Блог не найден</SecondaryText></li>

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
// Rubrics nest under their group row: the indent is the nesting, which is why
// the rubric rows carry no "- " prefix of their own.
.rubric-list
  list-style: none
  margin: 0
  padding: 0 0 0 $medium

.link
  display: block

.muted
  color: $text-muted

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

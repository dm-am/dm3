<script setup lang="ts">
// "Меню блога", to GamePanel's plan; the blog mentor gets the notepad, not the settings.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import {
  useBlogDetailsStore,
  BlogPremoderationTransition,
} from "@/entities/blog";
import { BlogStatusButtons, BlogJoinActions } from "@/features/blog-actions";
import {
  useAuthStore,
  userIsMentor,
  userIsSeniorModerator,
} from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarCounter from "./SidebarCounter.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import BlogRubricLink from "./BlogRubricLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

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

// The owner's move alone, and only while the blog awaits edits, as the server rules.
const canSubmitForApproval = computed(
  () => isOwner.value && blog.value?.premoderationStatus === "AwaitingEdits",
);

const { user } = storeToRefs(useAuthStore());
const router = useRouter();

// The public id, or the route's own param until the blog resolves.
const routeId = computed(() => blog.value?.publicId ?? props.blogId);

// Anybody signed in but the owner may subscribe.
const canActOnBlog = computed(() => !!user.value && !isOwner.value);

const isGlobalMentor = computed(() => userIsMentor(user.value));
const isGlobalSeniorModerator = computed(() =>
  userIsSeniorModerator(user.value),
);

// One dialog for the moderation moves; the request is its action.
interface ModAction {
  title: string;
  message: string;
  confirmLabel: string;
  /** What the window says when the server named nothing. */
  failure: string;
  run: () => Promise<{ error: GeneralError | null }>;
}
const pendingMod = ref<ModAction | null>(null);

/** The window prints the refusal, so the interceptor keeps quiet about it. */

const PREMOD_PROMPTS: Record<
  BlogPremoderationTransition,
  { title: string; message: string; confirmLabel: string }
> = {
  [BlogPremoderationTransition.SetApproved]: {
    title: "Одобрить блог?",
    message: "Блог станет виден всем",
    confirmLabel: "Одобрить",
  },
  [BlogPremoderationTransition.SetAwaitingEdits]: {
    title: "Вернуть блог на доработку?",
    message: "Автор сможет доработать блог и отправить его на проверку заново",
    confirmLabel: "Вернуть",
  },
  [BlogPremoderationTransition.SubmitForApproval]: {
    title: "Отправить блог на проверку?",
    message: "Блог уйдет наставнику на проверку",
    confirmLabel: "Отправить",
  },
};

function askPremod(t: BlogPremoderationTransition) {
  pendingMod.value = {
    ...PREMOD_PROMPTS[t],
    failure: "Не удалось изменить премодерацию",
    run: async () => ({
      error: await store.changePremoderation(t, OWNS_REFUSAL),
    }),
  };
}

function askDelete() {
  pendingMod.value = {
    title: "Удалить блог?",
    message: "Блог будет удален без возможности восстановления",
    confirmLabel: "Удалить",
    failure: "Не удалось удалить блог",
    run: async () => {
      const error = await store.deleteBlog(OWNS_REFUSAL);
      if (!error) router.push({ name: "blogs" });
      return { error };
    },
  };
}

const runMod = () => pendingMod.value?.run();
</script>

<template>
  <SidebarBlock token="BlogPanel" title="Меню блога">
    <SidebarSkeleton v-if="blogLoading && !blog" :lines="8" />

    <li v-else-if="blogError">
      <SecondaryText class="error">{{ blogError }}</SecondaryText>
    </li>

    <template v-else-if="blog">
      <!-- The rubrics ride inside the blog: no loading or error state of their own -->
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

      <template v-if="canManage || canUseNotepad">
        <SidebarSectionTitle>Управление блогом</SidebarSectionTitle>
        <template v-if="canManage">
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
          <BlogStatusButtons />
        </template>
        <!-- Not inside canManage: an assistant writes in the blog but does not declare it ready -->
        <li v-if="canSubmitForApproval" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button
            type="button"
            class="strip-action"
            @click="askPremod(BlogPremoderationTransition.SubmitForApproval)"
          >
            Отправить на проверку
          </button>
        </li>
        <li v-if="canUseNotepad" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'blog-notepad', params: { id: routeId } }"
            >Заметки блога</router-link
          >
        </li>
      </template>

      <template v-if="canActOnBlog">
        <SidebarSectionTitle>Действия с блогом</SidebarSectionTitle>
        <BlogJoinActions />
      </template>

      <template v-if="isGlobalMentor">
        <SidebarSectionTitle>Модерация блога</SidebarSectionTitle>
        <!-- The verdict both ways, legal from every status -->
        <li class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button
            type="button"
            class="strip-action"
            @click="askPremod(BlogPremoderationTransition.SetApproved)"
          >
            Одобрить блог
          </button>
        </li>
        <li class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button
            type="button"
            class="strip-action"
            @click="askPremod(BlogPremoderationTransition.SetAwaitingEdits)"
          >
            Вернуть на доработку
          </button>
        </li>
        <li v-if="isGlobalSeniorModerator" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button type="button" class="strip-action" @click="askDelete">
            Удалить блог
          </button>
        </li>
      </template>
    </template>

    <li v-else><SecondaryText>Блог не найден</SecondaryText></li>

    <ConfirmDialog
      :show="!!pendingMod"
      :title="pendingMod?.title ?? ''"
      :message="pendingMod?.message ?? ''"
      :confirm-label="pendingMod?.confirmLabel"
      :failure="pendingMod?.failure"
      :action="runMod"
      @update:show="(open: boolean) => !open && (pendingMod = null)"
    />
  </SidebarBlock>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// Rubrics nest under their group row: the indent is the nesting.
.rubric-list
  list-style: none
  margin: 0
  padding: 0 0 0 $medium

.link
  display: block

.error
  color: $accent-red

// Words of the menu like the links; deleting is not coloured, its question carries the weight.
.strip-action
  +inline-link-button
</style>

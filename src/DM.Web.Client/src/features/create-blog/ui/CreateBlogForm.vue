<script setup lang="ts">
/**
 * CreateBlogForm — "Создание блога" (doc 4.2.3.6.5).
 *
 * Mirrors the CreateGameForm pattern: typed payload (CreateBlogInput ->
 * backend CreateBlogRequest), field-level FluentValidation errors mapped
 * back onto the form, toast for non-field failures, redirect to the created
 * blog. The description is raw BBCode source on this endpoint (unlike the
 * read side, which returns server-rendered HTML), so it gets the BBCode
 * editor.
 */
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/entities/user";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import {
  blogApi,
  useBlogsStore,
  DraftVisibility,
  type CreateBlogInput,
} from "@/entities/blog";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type { BadRequestError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { describeFailure } from "@/shared/lib/errors";

const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const toast = useToast();
const blogsStore = useBlogsStore();

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Превью видно всем" },
  { value: DraftVisibility.Private, label: "Только участникам" },
];

// Form state (mirrors backend CreateBlogRequest defaults)
const title = ref("");
const description = ref("");
const draftVisibility = ref(DraftVisibility.Public);
const commentsEnabled = ref(true);
const copyBlacklist = ref(false);

// See CreateGameForm: released once the blog exists, so the form's own
// navigation is not questioned.
const saved = ref(false);
const dirty = computed(
  () => !saved.value && !!(title.value.trim() || description.value.trim()),
);

const isSubmitting = ref(false);
const titleError = ref("");
const descriptionError = ref("");

const canCreate = computed(() => {
  return user.value && title.value.trim().length > 0;
});

function clearErrors() {
  titleError.value = "";
  descriptionError.value = "";
}

async function handleSubmit() {
  if (!canCreate.value || isSubmitting.value) return;

  isSubmitting.value = true;
  clearErrors();

  try {
    const blogData: CreateBlogInput = {
      title: title.value.trim(),
      description: description.value,
      draftVisibility: draftVisibility.value,
      commentsEnabled: commentsEnabled.value,
      copyBlacklist: copyBlacklist.value,
    };

    const { data, error: apiError } = await blogApi.createBlog(blogData);

    if (apiError) {
      const errors = parseApiErrors(apiError as BadRequestError);
      titleError.value = getFieldError(errors, "title") || "";
      descriptionError.value = getFieldError(errors, "description") || "";

      if (!titleError.value && !descriptionError.value) {
        toast.error(describeFailure(apiError, "Не удалось создать блог"));
      }
      return;
    }

    if (data) {
      // Same as game creation: the lists are cached, so without refreshing
      // them the new blog is missing from /blogs and from the sidebar.
      saved.value = true;
      await blogsStore.invalidateBlogLists();
      router.push({
        name: "blog",
        params: { id: data.resource.publicId ?? data.resource.id },
      });
    }
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <form class="create-blog-form" @submit.prevent="handleSubmit">
    <!-- Basic info -->
    <section class="form-section">
      <block-title>Основная информация</block-title>

      <form-field
        label="Название блога *"
        name="title"
        :errors="titleError ? [titleError] : []"
      >
        <input
          v-model="title"
          type="text"
          id="title"
          placeholder="Введите название блога"
          maxlength="200"
          @input="titleError = ''"
        />
      </form-field>
    </section>

    <!-- Description -->
    <section class="form-section">
      <block-title>Описание</block-title>
      <form-field
        name="description"
        :errors="descriptionError ? [descriptionError] : []"
      >
        <BBCodeEditor
          v-model="description"
          context="common"
          placeholder="Расскажите, о чем ваш блог..."
          :min-height="200"
          :max-height="500"
          :resizable="true"
          @update:model-value="descriptionError = ''"
        />
      </form-field>
    </section>

    <!-- Access settings -->
    <section class="form-section">
      <block-title>Настройки доступа</block-title>

      <form-field label="Видимость черновика">
        <Select
          :model-value="draftVisibility"
          :options="visibilityOptions"
          @update:model-value="(v) => (draftVisibility = v as DraftVisibility)"
        />
      </form-field>

      <div class="checkbox-group">
        <div class="access-option">
          <label class="checkbox-label">
            <input v-model="commentsEnabled" type="checkbox" />
            Комментарии включены
          </label>
          <p class="option-hint">Читатели смогут обсуждать блог</p>
        </div>

        <div class="access-option">
          <label class="checkbox-label">
            <input v-model="copyBlacklist" type="checkbox" />
            Скопировать личный черный список
          </label>
          <p class="option-hint">
            Пользователи из вашего черного списка не получат доступ к блогу
          </p>
        </div>
      </div>
    </section>

    <!-- Submit -->
    <div class="form-actions">
      <Button type="submit" :loading="isSubmitting" :disabled="!canCreate">
        Создать блог
      </Button>
    </div>

    <UnsavedChangesGuard :dirty="dirty" />
  </form>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.create-blog-form
  max-width: $grid-step * 150

.form-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.checkbox-group
  display: flex
  flex-direction: column
  gap: $medium

.access-option
  display: flex
  flex-direction: column

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.option-hint
  margin: $minor 0 0
  padding-left: 16px + $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

.form-actions
  display: flex
  gap: $small
</style>

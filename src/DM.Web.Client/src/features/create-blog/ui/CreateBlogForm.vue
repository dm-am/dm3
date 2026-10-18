<script setup lang="ts">
/**
 * CreateBlogForm — "Создание блога" (doc 4.2.3.6.5).
 *
 * Laid out as the creation of a game is, DM2's one form of captioned rows
 * (_FormRows.sass): the title and who sees the blog in the making beside their
 * captions, the box under them, the description over the whole width.
 *
 * Mirrors the CreateGameForm pattern: typed payload (CreateBlogInput ->
 * backend CreateBlogRequest), field-level FluentValidation errors mapped
 * back onto the form, the non-field failures in a line under the button (the
 * plate the response interceptor held for them taken down), redirect to the
 * created blog. The description is raw BBCode source on this endpoint (unlike
 * the read side, which returns server-rendered HTML), so it gets the BBCode
 * editor.
 *
 * "Перенести мой черный список" is CopyBlacklistOption: drawn only when the
 * author's own list has somebody on it, starting where the account setting
 * stands. The form sends what the box shows, and leaves the field out where
 * there is no box, so the server applies the setting itself.
 */
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/entities/user";
import { CopyBlacklistOption } from "@/features/roster/@x/create-blog";
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
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const blogsStore = useBlogsStore();

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Все" },
  { value: DraftVisibility.Private, label: "Только участники" },
];

// Form state (mirrors backend CreateBlogRequest defaults)
const title = ref("");
const description = ref("");
const draftVisibility = ref(DraftVisibility.Public);
/** Undefined while there is no box: the server then follows the setting. */
const copyBlacklist = ref<boolean | undefined>(undefined);

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

/** What the server said that no field of the form shows, under the button. */
const createResult = useActionResult();

function clearErrors() {
  titleError.value = "";
  descriptionError.value = "";
  createResult.clear();
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
      copyBlacklist: copyBlacklist.value,
    };

    const { data, error: apiError } = await blogApi.createBlog(blogData);

    if (apiError) {
      const errors = parseApiErrors(apiError as BadRequestError);
      titleError.value = getFieldError(errors, "title") || "";
      descriptionError.value = getFieldError(errors, "description") || "";

      if (!titleError.value && !descriptionError.value) {
        // What a field can show it has shown; the rest - a refusal, a rate
        // limit, a broken server, a 400 that named no field of this form -
        // stands under the button that was pressed.
        createResult.refuse(apiError, "Не удалось создать блог");
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
    <form-field
      class="row"
      label="Название:"
      name="title"
      :errors="titleError ? [titleError] : []"
    >
      <input
        v-model="title"
        type="text"
        id="title"
        maxlength="200"
        @input="titleError = ''"
      />
    </form-field>

    <form-field
      class="row"
      label="Кто видит оформляющийся блог:"
      name="draft-visibility"
    >
      <Select
        id="blog-draft-visibility"
        :model-value="draftVisibility"
        :options="visibilityOptions"
        @update:model-value="(v) => (draftVisibility = v as DraftVisibility)"
      />
    </form-field>

    <CopyBlacklistOption
      v-if="user"
      v-model="copyBlacklist"
      class="copy-option"
      does="Внесет в черный список блога"
    />

    <form-field
      label="Описание:"
      name="description"
      :errors="descriptionError ? [descriptionError] : []"
    >
      <BBCodeEditor
        v-model="description"
        context="common"
        :min-height="200"
        :max-height="500"
        :resizable="true"
        @update:model-value="descriptionError = ''"
      />
    </form-field>

    <div class="form-actions">
      <Button
        type="submit"
        variant="primary"
        :loading="isSubmitting"
        :disabled="!canCreate || createResult.waiting.value"
      >
        Создать
      </Button>
    </div>
    <ActionResult :result="createResult" />

    <UnsavedChangesGuard :dirty="dirty" />
  </form>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

.create-blog-form
  max-width: $grid-step * 150

.create-blog-form .row
  +form-row

// The box stands where the boxes of the game's form do: at the left edge of
// the form, a step from the rows over it and the description under it.
.copy-option
  margin: $medium 0

.form-actions
  display: flex
  gap: $small
  margin-top: $medium
</style>

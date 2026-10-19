<script setup lang="ts">
// The description is raw BBCode on this endpoint, so it gets the editor.
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
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { BadRequestError } from "@/shared/api/models/common";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { BLOG_TITLE_MAX_LENGTH } from "@/shared/lib/constants/blog";

const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const blogsStore = useBlogsStore();

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Все" },
  { value: DraftVisibility.Private, label: "Только участники" },
];

const title = ref("");
const description = ref("");
const draftVisibility = ref(DraftVisibility.Public);
/** Undefined while there is no box: the server then follows the setting. */
const copyBlacklist = ref<boolean | undefined>(undefined);

// Released once the blog exists, so the form's own navigation is not questioned.
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
      title: title.value,
      description: description.value,
      draftVisibility: draftVisibility.value,
      copyBlacklist: copyBlacklist.value,
    };

    const { data, error: apiError } = await blogApi.createBlog(blogData);

    if (apiError) {
      const errors = parseApiErrors(apiError as BadRequestError);
      titleError.value = readFieldError(errors, "title") || "";
      descriptionError.value = readFieldError(errors, "description") || "";

      if (!titleError.value && !descriptionError.value) {
        createResult.refuse(apiError, "Не удалось создать блог");
      }
      return;
    }

    if (data) {
      // The lists are cached: without a refresh the new blog is missing until they expire.
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
        :maxlength="BLOG_TITLE_MAX_LENGTH"
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

.copy-option
  margin: $medium 0

.form-actions
  display: flex
  gap: $small
  margin-top: $medium
</style>

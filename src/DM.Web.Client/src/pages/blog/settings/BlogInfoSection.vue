<script setup lang="ts">
/**
 * The title, the description and who sees the blog while it is in the making,
 * as GameInfoSection. The description is the owner's alone: the server gives
 * its BBCode as typed to its author, and a lead would be given it drawn.
 */
import { computed, nextTick, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi, DraftVisibility } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { BLOG_TITLE_MAX_LENGTH } from "@/shared/lib/constants/blog";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { BadRequestError } from "@/shared/api/models/common";

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// By id, as the server compares: a username can change.
const isOwner = computed(
  () =>
    !!user.value &&
    !!blog.value?.author &&
    blog.value.author.id === user.value.id,
);

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Все" },
  { value: DraftVisibility.Private, label: "Только участники" },
];

const title = ref("");
const draftVisibility = ref(DraftVisibility.Public);
/** The description as typed, and as the server last gave it; null until it is read. */
const description = ref("");
const storedDescription = ref<string | null>(null);
/** The server's refusal of the description, under the field it is about. */
const descriptionRefusal = ref("");

const saving = ref(false);

/**
 * The patch the form sends, and what the saved state compares: one function, or the two drift.
 * The description goes only changed, an erased one as an empty string.
 */
const patch = () => ({
  title: title.value,
  draftVisibility: draftVisibility.value,
  ...(storedDescription.value !== null &&
  description.value !== storedDescription.value
    ? { description: description.value }
    : {}),
});

// The screen after a save holds the typed values, so the button carries the answer.
const { saved, label, remember } = useSavedState(patch);

// A form that matches the server's copy has nothing to submit.
const canSubmit = computed(() => title.value.trim().length > 0 && !saved.value);

watch(
  blog,
  (b) => {
    if (!b) return;
    title.value = b.title ?? "";
    draftVisibility.value = b.draftVisibility ?? DraftVisibility.Public;
  },
  { immediate: true },
);

const saveResult = useActionResult();

async function loadDescription(id: string) {
  const { data, error } = await blogApi.getBlogForEdit(id);
  if (blog.value?.id !== id) return;
  if (error) {
    saveResult.refuse(error, "Не удалось загрузить описание блога");
    return;
  }
  storedDescription.value = data?.resource.description ?? "";
  description.value = storedDescription.value;
}

watch(
  () => (isOwner.value ? blog.value?.id : undefined),
  (id) => {
    storedDescription.value = null;
    if (id) void loadDescription(id);
  },
  { immediate: true },
);

async function save() {
  if (!blog.value) return;
  const id = blog.value.id;
  saving.value = true;
  saveResult.clear();
  descriptionRefusal.value = "";

  const { error } = await blogApi.updateBlog(id, patch());
  saving.value = false;
  if (error) {
    const said = readFieldError(
      parseApiErrors(error as BadRequestError),
      "description",
    );
    if (said) descriptionRefusal.value = said;
    else saveResult.refuse(error, "Не удалось сохранить информацию блога");
    return;
  }
  await store.loadBlog(id);
  // The field shows what the server stored, not what was sent.
  if (isOwner.value) await loadDescription(id);
  // After the reload and the tick that re-seeds the fields, so the button
  // compares against the stored copy.
  await nextTick();
  remember();
}
</script>

<template>
  <SettingsSection title="Общие настройки">
    <Form
      class="info-form"
      :valid="canSubmit"
      :loading="saving"
      :action="label"
      :refusal="saveResult.refusal.value"
      @submit="save"
    >
      <FormField class="row" label="Название:" name="blog-title">
        <input
          id="blog-title"
          v-model="title"
          type="text"
          :maxlength="BLOG_TITLE_MAX_LENGTH"
        />
      </FormField>

      <FormField
        class="row"
        label="Кто видит оформляющийся блог:"
        name="draft-visibility"
      >
        <Select
          id="blog-settings-draft-visibility"
          :model-value="draftVisibility"
          :options="visibilityOptions"
          @update:model-value="(v) => (draftVisibility = v as DraftVisibility)"
        />
      </FormField>

      <FormField
        v-if="storedDescription !== null"
        label="Описание:"
        name="blog-description"
        :errors="descriptionRefusal ? [descriptionRefusal] : []"
      >
        <BBCodeEditor
          v-model="description"
          context="common"
          :min-height="200"
          :max-height="500"
          :resizable="true"
          @update:model-value="descriptionRefusal = ''"
        />
      </FormField>
    </Form>
  </SettingsSection>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

.info-form .row
  +form-row
</style>

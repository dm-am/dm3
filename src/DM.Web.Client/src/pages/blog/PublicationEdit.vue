<script setup lang="ts">
/**
 * Editing a publication, for the owner and the assistants; anybody else gets
 * the site's "not found", decided after restoreSession. The content is read
 * as its author typed it (the author_edit audience) and sent only when it was
 * changed: an untouched text is not written again.
 */
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { Publication, UpdatePublicationInput } from "@/entities/blog";
import { restoreSession } from "@/entities/user";
import {
  PUBLICATION_EDITOR_HEIGHT,
  PublicationForm,
} from "@/features/publication";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import Button from "@/shared/ui/Button/Button.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import FormSkeleton from "@/shared/ui/Skeleton/FormSkeleton.vue";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

const route = useRoute();
const router = useRouter();
const blogStore = useBlogDetailsStore();
const { rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);
const pubId = computed(() => route.params.pubId as string);

const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

useNotFoundWhen(() => sessionSettled.value && !canManage.value);

const NO_RUBRIC = "";
const rubricOptions = computed(() => [
  { value: NO_RUBRIC, label: "Без рубрики" },
  ...rubrics.value.map((r) => ({ value: r.id, label: r.title })),
]);

// The shared store holds the feed, not one publication.
const publication = ref<Publication | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
/** The publication has answered: "not found" is said only after it. */
const loaded = ref(false);

const title = ref("");
const rubricId = ref<string>(NO_RUBRIC);
const content = ref("");
// Content is sent only when it diverges from this.
const initialContent = ref("");

async function load() {
  loading.value = true;
  loadError.value = null;
  const { data, error } = await blogApi.getPublicationForEdit(pubId.value);
  if (error || !data?.resource) {
    loadError.value = "Не удалось загрузить публикацию";
    publication.value = null;
  } else {
    publication.value = data.resource;
    title.value = data.resource.title;
    rubricId.value = data.resource.rubric?.id ?? NO_RUBRIC;
    initialContent.value = data.resource.content;
    content.value = initialContent.value;
  }
  loading.value = false;
  loaded.value = true;
}

useFetchData(load, [{ param: (p) => p.pubId, callback: () => load() }]);

const valid = computed(
  () => title.value.trim().length > 0 && content.value.trim().length > 0,
);

const saving = ref(false);
// Released on a successful save or delete: both navigate away.
const saved = ref(false);

// No draft key on purpose (a draft of an edit would resurrect itself over the
// published text), so leaving the page is guarded instead.
const dirty = computed(
  () =>
    !saved.value &&
    !!publication.value &&
    (title.value !== publication.value.title ||
      rubricId.value !== (publication.value.rubric?.id ?? NO_RUBRIC) ||
      content.value !== initialContent.value),
);

const saveResult = useActionResult();

async function save() {
  if (!publication.value || !valid.value || saving.value) return;
  saving.value = true;
  saveResult.clear();

  const patch: UpdatePublicationInput = {
    title: title.value,
  };
  if (rubricId.value) {
    patch.rubricId = rubricId.value;
  } else if (publication.value.rubric) {
    patch.clearRubric = true;
  }
  if (content.value !== initialContent.value) {
    patch.content = content.value;
  }

  const { error } = await blogApi.updatePublication(pubId.value, patch);
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить публикацию");
    return;
  }
  saved.value = true;
  // No success toast: the publication it opens shows what was saved.
  router.push({
    name: "blog-publication",
    params: { id: blogId.value, pubId: pubId.value },
  });
}

const confirmingDelete = ref(false);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmDelete() {
  if (!publication.value) return;
  const result = await blogApi.deletePublication(pubId.value, OWNS_REFUSAL);
  if (result.error) return result;
  saved.value = true;
  // From the loaded publication, not the form: the select may hold an unsaved rubric.
  const deletedFrom = publication.value.rubric?.id;
  blogStore.loadBlog(blogId.value);
  // No success toast: the feed it lands on reloads without the publication.
  router.push({
    name: "blog-feed",
    params: { id: blogId.value },
    query: deletedFrom ? { rubric: deletedFrom } : {},
  });
  return result;
}

function cancel() {
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}
</script>

<template>
  <div class="publication-edit">
    <!-- Anybody but the owner and the assistants is sent to "not found". -->
    <template v-if="canManage">
      <!-- The form's skeleton: the title, the rubric, the text and the two buttons. -->
      <FormSkeleton
        v-if="!publication && (loading || !loaded)"
        :fields="3"
        :editor-height="PUBLICATION_EDITOR_HEIGHT"
      />

      <ErrorState v-else-if="loadError" :message="loadError" :retry="load" />

      <template v-else-if="publication">
        <PublicationForm
          v-model:title="title"
          v-model:rubric-id="rubricId"
          v-model:content="content"
          :rubric-options="rubricOptions"
          :valid="valid"
          :loading="saving"
          :refusal="saveResult.refusal.value"
          action="Сохранить"
          cancel-label="Отмена"
          @submit="save"
          @cancel="cancel"
        />

        <div class="danger-row">
          <Button
            type="button"
            class="delete-publication"
            @click="confirmingDelete = true"
          >
            Удалить публикацию
          </Button>
        </div>

        <UnsavedChangesGuard :dirty="dirty" />
      </template>

      <secondary-text v-else>Публикация не найдена</secondary-text>
    </template>

    <ConfirmDialog
      v-model:show="confirmingDelete"
      title="Удалить публикацию?"
      message="Публикация будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить публикацию"
      :action="confirmDelete"
    />
  </div>
</template>

<style scoped lang="sass">
.publication-edit
  max-width: $grid-step * 200

.danger-row
  margin-top: $big
</style>

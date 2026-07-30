<script setup lang="ts">
/**
 * BlogInfoSection — edit the blog's core details (title, draft visibility,
 * comments switch). Persists via blogApi.updateBlog (PATCH blogs/{id}) then
 * reloads the blog. Mirrors GameInfoSection.
 *
 * The description is intentionally NOT edited here: the blog endpoint returns
 * it as server-rendered HTML (CommonBbText), so there is no raw BBCode source
 * to round-trip through an editor without corrupting it — the same limitation
 * as the game description (see GameInfoSection).
 */
import { ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi, DraftVisibility } from "@/entities/blog";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);
const toast = useToast();

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Превью видно всем" },
  { value: DraftVisibility.Private, label: "Только участникам" },
];

const title = ref("");
const draftVisibility = ref(DraftVisibility.Public);
const commentsEnabled = ref(true);

const saving = ref(false);

// Seed the local draft from the loaded blog.
watch(
  blog,
  (b) => {
    if (!b) return;
    title.value = b.title ?? "";
    draftVisibility.value = b.draftVisibility ?? DraftVisibility.Public;
    commentsEnabled.value = b.commentsEnabled ?? true;
  },
  { immediate: true },
);

async function save() {
  if (!blog.value) return;
  saving.value = true;

  const { error } = await blogApi.updateBlog(blog.value.id, {
    title: title.value.trim(),
    draftVisibility: draftVisibility.value,
    commentsEnabled: commentsEnabled.value,
  });
  saving.value = false;
  if (error) {
    notifyFailure(error, "Не удалось сохранить информацию блога");
    return;
  }
  toast.success("Информация блога сохранена");
  await store.loadBlog(blog.value.id);
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Информация блога</BlockTitle>
    <Form
      :valid="title.trim().length > 0"
      :loading="saving"
      action="Сохранить"
      @submit="save"
    >
      <FormField label="Название" name="blog-title">
        <input id="blog-title" v-model="title" type="text" maxlength="200" />
      </FormField>

      <FormField label="Видимость черновика">
        <Select
          :model-value="draftVisibility"
          :options="visibilityOptions"
          @update:model-value="(v) => (draftVisibility = v as DraftVisibility)"
        />
      </FormField>

      <FormField>
        <label class="checkbox-label">
          <input v-model="commentsEnabled" type="checkbox" />
          Комментарии включены
        </label>
      </FormField>
    </Form>
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
</style>

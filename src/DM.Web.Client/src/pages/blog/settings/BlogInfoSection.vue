<script setup lang="ts">
/**
 * BlogInfoSection — "Общие настройки" of a blog: the title and who sees the
 * blog while it is in the making. Persists via blogApi.updateBlog (PATCH
 * blogs/{id}) then reloads the blog. Mirrors GameInfoSection, DM2's one form of
 * captioned rows (_FormRows.sass).
 *
 * The description is intentionally NOT edited here: the blog endpoint returns
 * it as server-rendered HTML (CommonBbText), so there is no raw BBCode source
 * to round-trip through an editor without corrupting it — the same limitation
 * as the game description (see GameInfoSection).
 */
import { computed, nextTick, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi, DraftVisibility } from "@/entities/blog";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const visibilityOptions = [
  { value: DraftVisibility.Public, label: "Все" },
  { value: DraftVisibility.Private, label: "Только участники" },
];

const title = ref("");
const draftVisibility = ref(DraftVisibility.Public);

const saving = ref(false);

/**
 * The patch this form sends, and the only thing the saved state compares.
 *
 * One function for both, because two would drift: a field the button watches
 * but the request leaves out would take the form back to "Сохранить" over work
 * that was never sent, and a field the request carries but the button ignores
 * would leave it reading "Сохранено" over an edit still in the field.
 */
const patch = () => ({
  title: title.value.trim(),
  draftVisibility: draftVisibility.value,
});

// The form is the only thing that can report a save of its own: the screen
// after one holds the very values the reader typed into it. So the submit
// button carries the answer, and there is no toast — see UI_STANDARDS,
// "Всплывающие сообщения".
const { saved, label, remember } = useSavedState(patch);

// Form draws the submit disabled while `valid` is false, and a form that
// matches the server's copy has nothing to submit. The button therefore reads
// "Сохранено" and stays inert until the reader changes a field.
const canSubmit = computed(() => title.value.trim().length > 0 && !saved.value);

// Seed the local draft from the loaded blog.
watch(
  blog,
  (b) => {
    if (!b) return;
    title.value = b.title ?? "";
    draftVisibility.value = b.draftVisibility ?? DraftVisibility.Public;
  },
  { immediate: true },
);

/** A refused save, said over the footer of the form. */
const saveResult = useActionResult();

async function save() {
  if (!blog.value) return;
  const id = blog.value.id;
  saving.value = true;
  saveResult.clear();

  const { error } = await blogApi.updateBlog(id, patch());
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить информацию блога");
    return;
  }
  await store.loadBlog(id);
  // Remembered after the reload and the tick that lets the watch above re-seed
  // the fields: the snapshot has to be what now stands on screen. Taken before
  // it, a value the server normalised on its way in would come back into the
  // field and read as an edit the reader never made.
  await nextTick();
  remember();
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Общие настройки</BlockTitle>
    <Form
      class="info-form"
      :valid="canSubmit"
      :loading="saving"
      :action="label"
      :refusal="saveResult.refusal.value"
      @submit="save"
    >
      <FormField class="row" label="Название:" name="blog-title">
        <input id="blog-title" v-model="title" type="text" maxlength="200" />
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
    </Form>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

.info-form .row
  +form-row
</style>

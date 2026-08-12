<script setup lang="ts">
/**
 * RubricsSection — manage the blog's rubrics (categories of publications;
 * the blog-zone counterpart of game rooms). This section wires create and
 * delete (POST blogs/{id}/rubrics, DELETE blogs/{id}/rubrics/{rubricId});
 * the rename and reorder endpoints exist on the server but nothing here
 * calls them, so rows are not editable in place. Backed by the shared
 * blog-details store (createRubric / deleteRubric re-sync the blog, so the
 * sidebar panel updates too).
 */
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import type { Rubric } from "@/entities/blog";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useBlogDetailsStore();
const { rubrics } = storeToRefs(store);
const toast = useToast();

const newTitle = ref("");
const creating = ref(false);
const pendingDelete = ref<Rubric | null>(null);

async function create() {
  if (!newTitle.value.trim()) return;
  creating.value = true;
  const error = await store.createRubric({ title: newTitle.value.trim() });
  creating.value = false;
  if (error) {
    notifyFailure(error, "Не удалось создать рубрику");
    return;
  }
  toast.success("Рубрика создана");
  newTitle.value = "";
}

async function confirmDelete() {
  const rubric = pendingDelete.value;
  pendingDelete.value = null;
  if (!rubric) return;
  const error = await store.deleteRubric(rubric.id);
  if (error) {
    notifyFailure(error, "Не удалось удалить рубрику");
    return;
  }
  toast.success("Рубрика удалена");
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Управление рубриками</BlockTitle>

    <ul v-if="rubrics.length" class="rubric-list">
      <li v-for="rubric in rubrics" :key="rubric.id" class="rubric-item">
        <span class="rubric-title">{{ rubric.title }}</span>
        <button
          type="button"
          class="remove-btn"
          @click="pendingDelete = rubric"
        >
          Удалить
        </button>
      </li>
    </ul>
    <SecondaryText v-else>Рубрик пока нет</SecondaryText>

    <div class="add-row">
      <input
        v-model="newTitle"
        type="text"
        placeholder="Название рубрики"
        aria-label="Название рубрики"
        maxlength="100"
        @keydown.enter.prevent="create"
      />
      <Button
        type="button"
        :loading="creating"
        :disabled="!newTitle.trim()"
        @click="create"
      >
        Создать
      </Button>
    </div>

    <ConfirmDialog
      :show="!!pendingDelete"
      title="Удаление рубрики"
      :message="`Удалить рубрику &quot;${pendingDelete?.title ?? ''}&quot;? Публикации из нее останутся в блоге без рубрики.`"
      confirm-label="Удалить"
      danger
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.rubric-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.rubric-item
  display: flex
  align-items: center
  gap: $small

.rubric-title
  color: $text

.remove-btn
  padding: 0
  border: none
  background: none
  font: inherit
  font-size: $secondary-font-size
  color: $accent-red
  cursor: pointer

  &:hover
    text-decoration: underline

.add-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small

  input
    flex: 1
    max-width: $grid-step * 75
</style>

<script setup lang="ts">
/**
 * Creating and deleting the blog's rubrics; the store re-syncs the blog, so
 * the sidebar follows. Neither says anything in the corner: both move the list.
 */
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import type { Rubric } from "@/entities/blog";
import Button from "@/shared/ui/Button/Button.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { RUBRIC_TITLE_MAX_LENGTH } from "@/shared/lib/constants/blog";

const store = useBlogDetailsStore();
const { rubrics } = storeToRefs(store);

const newTitle = ref("");
const creating = ref(false);
const pendingDelete = ref<Rubric | null>(null);

const createResult = useActionResult();

async function create() {
  const title = newTitle.value;
  if (!title.trim()) return;
  creating.value = true;
  createResult.clear();
  // The endpoint keeps the position it is handed; without one a rubric sorted
  // among the zeros by title. One past the highest is the row above the field.
  const sortOrder =
    rubrics.value.reduce((max, rubric) => Math.max(max, rubric.sortOrder), 0) +
    1;
  const error = await store.createRubric({ title, sortOrder });
  creating.value = false;
  if (error) {
    createResult.refuse(error, "Не удалось создать рубрику");
    return;
  }
  // The new row and the cleared field are the confirmation.
  newTitle.value = "";
}

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmDelete() {
  const rubric = pendingDelete.value;
  if (!rubric) return;
  const error = await store.deleteRubric(rubric.id, OWNS_REFUSAL);
  if (error) return { error };
  // Nothing to announce: the row is gone. The delete is soft, but every read goes
  // through the IsRemoved filter, so the rubric is gone everywhere.
  return { error: null };
}
</script>

<template>
  <SettingsSection title="Управление рубриками">
    <ul v-if="rubrics.length" class="rubric-list">
      <li v-for="rubric in rubrics" :key="rubric.id" class="rubric-item">
        <span class="rubric-title">{{ rubric.title }}</span>
        <RemoveButton @click="pendingDelete = rubric"> Удалить </RemoveButton>
      </li>
    </ul>
    <SecondaryText v-else>Рубрик пока нет</SecondaryText>

    <div class="add-row">
      <input
        v-model="newTitle"
        type="text"
        placeholder="Название рубрики"
        aria-label="Название рубрики"
        :maxlength="RUBRIC_TITLE_MAX_LENGTH"
        @keydown.enter.prevent="create"
      />
      <Button
        type="button"
        variant="primary"
        :loading="creating"
        :disabled="!newTitle.trim()"
        @click="create"
      >
        Создать
      </Button>
    </div>
    <ActionResult :result="createResult" />

    <ConfirmDialog
      :show="!!pendingDelete"
      title="Удалить рубрику?"
      :message="`Публикации из рубрики &quot;${pendingDelete?.title ?? ''}&quot; останутся в блоге без рубрики`"
      confirm-label="Удалить"
      failure="Не удалось удалить рубрику"
      :action="confirmDelete"
      @update:show="(shown) => !shown && (pendingDelete = null)"
    />
  </SettingsSection>
</template>

<style scoped lang="sass">
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

.add-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small

  input
    flex: 1
    max-width: $grid-step * 75
</style>

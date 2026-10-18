<script setup lang="ts">
/**
 * RubricsSection — manage the blog's rubrics (categories of publications;
 * the blog-zone counterpart of game rooms). This section wires create and
 * delete (POST blogs/{id}/rubrics, DELETE blogs/{id}/rubrics/{rubricId});
 * the rename and reorder endpoints exist on the server but nothing here
 * calls them, so rows are not editable in place. Backed by the shared
 * blog-details store (createRubric / deleteRubric re-sync the blog, so the
 * sidebar panel updates too).
 *
 * Neither operation says anything in the corner: both move the list that is
 * on screen. A created rubric is the row that appears directly above the
 * field it was typed into (see create), a deleted one is the row that leaves
 * the list under the button that asked for it (see confirmDelete).
 */
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import type { Rubric } from "@/entities/blog";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const store = useBlogDetailsStore();
const { rubrics } = storeToRefs(store);

const newTitle = ref("");
const creating = ref(false);
const pendingDelete = ref<Rubric | null>(null);

/** A refused "Создать", under the field the title was typed in, the title kept. */
const createResult = useActionResult();

async function create() {
  const title = newTitle.value.trim();
  if (!title) return;
  creating.value = true;
  createResult.clear();
  // Where the new row lands is this section's to decide, and the endpoint
  // takes the position it is handed. Sent without one, every rubric created
  // here carried SortOrder 0 and took its place among the other zeros by
  // title (BlogRepository.GetRubrics orders by SortOrder, then Title), which
  // in a long list is somewhere in the middle and as likely as not off the
  // screen. One past the highest puts it last, and last is the row directly
  // above the field the title was typed into.
  const sortOrder =
    rubrics.value.reduce((max, rubric) => Math.max(max, rubric.sortOrder), 0) +
    1;
  const error = await store.createRubric({ title, sortOrder });
  creating.value = false;
  if (error) {
    createResult.refuse(error, "Не удалось создать рубрику");
    return;
  }
  // That new row and the cleared field are the confirmation, the same answer
  // the rooms section gives to the same action (RoomsSection.createRoom).
  newTitle.value = "";
}

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDelete() {
  const rubric = pendingDelete.value;
  if (!rubric) return;
  const error = await store.deleteRubric(rubric.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (error) return { error };
  // Nothing to announce: the row is gone from the list the dialog was opened
  // over. The delete is soft, and publications keep the identifier of the
  // rubric they were in, but every read goes through the global IsRemoved
  // filter (DmDbContext), so the rubric is gone from this list, from the
  // blog's rubric panel and from its own publications, which from here on
  // read as having no rubric - exactly what the dialog promised.
  return { error: null };
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
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

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

// Removing is a link like every other: the confirmation that follows carries
// the weight of the act, and nothing on the page is red.
.remove-btn
  +inline-link-button

  &
    font-size: $secondary-font-size

.add-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small

  input
    flex: 1
    max-width: $grid-step * 75
</style>

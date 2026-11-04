<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, reactive } from "vue";
import { useModal } from "vue-final-modal";
import {
  gameTagApi,
  useGamesStore,
  type ModerationTagGroup,
  type ModerationTag,
} from "@/entities/game";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { describeFailureOrWait } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { pluralize } from "@/shared/lib/utils/pluralize";
import TagGroupDialog from "./dialogs/TagGroupDialog.vue";
import TagDialog from "./dialogs/TagDialog.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const gamesStore = useGamesStore();

const groups = ref<ModerationTagGroup[]>([]);
const tags = ref<ModerationTag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

const selectedGroupId = ref<string | null>(null);

const filteredTags = computed(() => {
  if (!selectedGroupId.value) return tags.value;
  return tags.value.filter((t) => t.groupId === selectedGroupId.value);
});

/** "1 игра" / "2 игры" / "5 игр". */
function gamesLabel(count: number): string {
  return `${count} ${pluralize(count, "игра", "игры", "игр")}`;
}

const selectedGroupTitle = computed(() => {
  if (!selectedGroupId.value) return "Все теги";
  const group = groups.value.find((g) => g.id === selectedGroupId.value);
  return group?.title ?? "Группа";
});

/**
 * Both reads own their refusal: it stands where the two panels would be. An
 * expired session and a dead connection stay with the interceptor.
 */

async function loadData() {
  loading.value = true;
  error.value = null;
  try {
    const [groupsRes, tagsRes] = await Promise.all([
      gameTagApi.getTagGroups(OWNS_REFUSAL),
      gameTagApi.getTags(OWNS_REFUSAL),
    ]);
    // A failed load is the page's own state: the message stands where the table would be.
    const failure = groupsRes.error ?? tagsRes.error;
    if (failure) {
      // The shared wording names the seconds; the 429 title carries no number.
      error.value = describeFailureOrWait(
        failure,
        "Не удалось загрузить данные",
      );
      return;
    }
    groups.value = groupsRes.data?.resources ?? [];
    tags.value = tagsRes.data?.resources ?? [];
  } finally {
    loading.value = false;
  }
}

/**
 * The write goes to a third address, so nothing invalidates the browser's copy
 * of the public `games/tags` (max-age=300): after a write the public catalogue
 * is read again from the origin, or the picker would show the old list.
 */
async function reloadAfterWrite() {
  await Promise.all([loadData(), gamesStore.fetchTags(true)]);
}

function selectGroup(groupId: string | null) {
  selectedGroupId.value = groupId;
}

const editingGroup = ref<ModerationTagGroup | null>(null);

const { open: openGroupDialog, close: closeGroupDialog } = useModal({
  component: TagGroupDialog,
  attrs: reactive({
    group: editingGroup,
    defaultSortOrder: computed(() => groups.value.length),
    onSuccess: async () => {
      closeGroupDialog();
      await reloadAfterWrite();
    },
    onCancel: () => closeGroupDialog(),
  }),
});

function openCreateGroupModal() {
  editingGroup.value = null;
  openGroupDialog();
}

function openEditGroupModal(group: ModerationTagGroup) {
  editingGroup.value = group;
  openGroupDialog();
}

const deleteGroupTarget = ref<ModerationTagGroup | null>(null);

// The window waits for the request, prints a refusal in itself and closes on success alone.
async function confirmDeleteGroup() {
  const target = deleteGroupTarget.value;
  if (!target) return;
  const result = await gameTagApi.deleteTagGroup(target.id, OWNS_REFUSAL);
  if (result.error) return result;
  if (selectedGroupId.value === target.id) {
    selectedGroupId.value = null;
  }
  void reloadAfterWrite();
  return result;
}

const editingTag = ref<ModerationTag | null>(null);

const { open: openTagDialog, close: closeTagDialog } = useModal({
  component: TagDialog,
  attrs: reactive({
    tag: editingTag,
    groups,
    defaultGroupId: computed(
      () => selectedGroupId.value ?? groups.value[0]?.id ?? "",
    ),
    defaultSortOrder: computed(() => filteredTags.value.length),
    onSuccess: async () => {
      closeTagDialog();
      await reloadAfterWrite();
    },
    onCancel: () => closeTagDialog(),
  }),
});

function openCreateTagModal() {
  editingTag.value = null;
  openTagDialog();
}

// A tag that did not come is said in its row, where the pencil was pressed.
const openFailedTagId = ref<string | null>(null);
const openResult = useActionResult();

// The page is gone: a tag still on its way opens no window over the next one.
let gone = false;
onUnmounted(() => (gone = true));

// The list carries the description rendered; the window opens on it as typed.
async function openEditTagModal(tag: ModerationTag) {
  openFailedTagId.value = null;
  openResult.clear();
  const { data, error } = await gameTagApi.getTagForEdit(tag.id);
  if (gone) return;
  if (error) {
    openFailedTagId.value = tag.id;
    openResult.refuse(error, "Не удалось загрузить тег");
    return;
  }
  editingTag.value = data;
  openTagDialog();
}

const deleteTagTarget = ref<ModerationTag | null>(null);

async function confirmDeleteTag() {
  const target = deleteTagTarget.value;
  if (!target) return;
  const result = await gameTagApi.deleteTag(target.id, OWNS_REFUSAL);
  if (!result.error) void reloadAfterWrite();
  return result;
}

onMounted(() => {
  loadData();
});
</script>

<template>
  <!-- The frame the two panels measure themselves against: the page, not the window. -->
  <div class="tags-page">
    <page-title>Игровые теги</page-title>

    <!-- A read again after a write keeps the panels standing under aria-busy. -->
    <div class="moderation-tags" :aria-busy="loading || undefined">
      <!-- Text, not a skeleton: the length of the two lists is anyone's guess. -->
      <div v-if="loading && !groups.length" class="loading">Загрузка...</div>
      <ErrorState
        v-else-if="error"
        class="load-refusal"
        :message="error"
        :retry="loadData"
      />
      <template v-else>
        <div class="groups-panel">
          <div class="panel-header">
            <h3>Группы тегов</h3>
            <button type="button" class="btn-add" @click="openCreateGroupModal">
              + Группа
            </button>
          </div>
          <div class="groups-list">
            <!-- The frame of a group row stretches its button, so the count stands at the end. -->
            <div class="group-item-wrapper">
              <button
                type="button"
                class="group-item group-item--all"
                :class="{ active: selectedGroupId === null }"
                @click="selectGroup(null)"
              >
                <span class="group-title">Все теги</span>
                <span class="group-count">{{ tags.length }}</span>
              </button>
            </div>
            <div
              v-for="group in groups"
              :key="group.id"
              class="group-item-wrapper"
            >
              <button
                type="button"
                class="group-item"
                :class="{ active: selectedGroupId === group.id }"
                @click="selectGroup(group.id)"
              >
                <span class="group-title">{{ group.title }}</span>
                <span class="group-count">{{ group.tagsCount }}</span>
              </button>
              <div class="group-actions">
                <Tooltip text="Редактировать">
                  <RowIconButton
                    icon="pencil"
                    label="Редактировать группу"
                    @click.stop="openEditGroupModal(group)"
                  />
                </Tooltip>
                <!-- The server refuses to delete a group that holds tags, so it has no trash. -->
                <Tooltip v-if="group.tagsCount === 0" text="Удалить">
                  <RowIconButton
                    icon="trash"
                    label="Удалить группу"
                    @click.stop="deleteGroupTarget = group"
                  />
                </Tooltip>
              </div>
            </div>
          </div>
        </div>

        <div class="tags-panel">
          <div class="panel-header">
            <h3>{{ selectedGroupTitle }}</h3>
            <button type="button" class="btn-add" @click="openCreateTagModal">
              + Тег
            </button>
          </div>
          <EmptyState
            v-if="filteredTags.length === 0"
            :title="
              selectedGroupId ? 'Нет тегов в этой группе' : 'Тегов пока нет'
            "
          />
          <div v-else class="tags-list">
            <div v-for="tag in filteredTags" :key="tag.id" class="tag-item">
              <div class="tag-info">
                <div class="tag-header">
                  <span class="tag-title">{{ tag.title }}</span>
                  <span class="tag-id">#{{ tag.shortId }}</span>
                </div>
                <!-- As in the tag's tooltip: the words of a [tip] under a dotted line. -->
                <div v-if="tag.description" class="tag-description">
                  <TooltipContent :html="tag.description" />
                </div>
                <div class="tag-meta">
                  <span class="tag-group">{{ tag.groupTitle }}</span>
                  <span class="tag-games">{{
                    gamesLabel(tag.gamesCount)
                  }}</span>
                  <span class="tag-sort">Порядок: {{ tag.sortOrder }}</span>
                </div>
                <ActionResult
                  v-if="openFailedTagId === tag.id"
                  :result="openResult"
                />
              </div>
              <div class="tag-actions">
                <Tooltip text="Редактировать">
                  <RowIconButton
                    icon="pencil"
                    label="Редактировать тег"
                    @click="openEditTagModal(tag)"
                  />
                </Tooltip>
                <!-- The same for a tag that stands in a game. -->
                <Tooltip v-if="tag.gamesCount === 0" text="Удалить">
                  <RowIconButton
                    icon="trash"
                    label="Удалить тег"
                    @click="deleteTagTarget = tag"
                  />
                </Tooltip>
              </div>
            </div>
          </div>
        </div>
      </template>

      <ConfirmDialog
        :show="deleteGroupTarget !== null"
        title="Удалить группу?"
        :message="`Группа &quot;${deleteGroupTarget?.title ?? ''}&quot; пропадет из каталога тегов`"
        confirm-label="Удалить"
        failure="Не удалось удалить группу тегов"
        :action="confirmDeleteGroup"
        @update:show="(v) => !v && (deleteGroupTarget = null)"
      />
      <ConfirmDialog
        :show="deleteTagTarget !== null"
        title="Удалить тег?"
        :message="`Тег &quot;${deleteTagTarget?.title ?? ''}&quot; пропадет из каталога тегов`"
        confirm-label="Удалить"
        failure="Не удалось удалить тег"
        :action="confirmDeleteTag"
        @update:show="(v) => !v && (deleteTagTarget = null)"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

// Inline-size containment keeps the width of the page from its ancestors.
.tags-page
  container: tags-page / inline-size

.moderation-tags
  display: grid
  grid-template-columns: 300px 1fr
  gap: $medium

.loading
  grid-column: 1 / -1
  padding: $large

.load-refusal
  grid-column: 1 / -1

// Panels: content surfaces, square as every card of the site.
.groups-panel,
.tags-panel
  background: $bg-element
  border: 1px solid $border
  overflow: hidden

.panel-header
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background: $bg-element-accent
  border-bottom: 1px solid $border

  h3
    margin: 0
    font-size: $font-size
    font-weight: 400

.btn-add
  +button

// One grid for every row, so the counts stand in one column and the buttons in two,
// whether a row has its trash or not: a row is a subgrid of it.
.groups-list
  display: grid
  grid-template-columns: 1fr auto auto
  column-gap: $tiny
  padding: $small

.group-item-wrapper
  grid-column: 1 / -1
  display: grid
  grid-template-columns: subgrid
  align-items: center

  &:hover .group-actions
    opacity: 1

.group-item
  grid-column: 1
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  margin-bottom: $tiny
  font-size: $secondary-font-size
  font-family: inherit
  text-align: left
  cursor: pointer
  border: none
  border-radius: $border-radius
  background: none
  color: $text

  &:hover
    background: $bg-element-hover

  // A choice, not a button: the active segment's fill, so the page's one fill of $link stays the main button's.
  &.active
    background: $bg-element-accent
    color: $text
    font-weight: 700

.group-title
  flex: 1

.group-count
  font-size: $tertiary-font-size
  color: inherit
  opacity: $opacity-soft

// The two columns of the buttons, the pencil in the first.
.group-actions
  grid-column: 2 / -1
  display: grid
  grid-template-columns: subgrid
  opacity: 0
  transition: opacity $transition-normal

// The same grid for the tags: the pencils stand in one column.
.tags-list
  display: grid
  grid-template-columns: 1fr auto auto
  column-gap: $tiny
  padding: $small

.tag-item
  grid-column: 1 / -1
  display: grid
  grid-template-columns: subgrid
  align-items: flex-start
  padding: $small $medium
  margin-bottom: $small
  border: 1px solid $border
  background: $bg-element-accent

  &:hover .tag-actions
    opacity: 1

.tag-info
  min-width: 0

.tag-header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $tiny

.tag-title
  font-weight: 400

.tag-id
  font-size: $tertiary-font-size
  color: $text-muted

.tag-description
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.tag-meta
  display: flex
  gap: $medium
  font-size: $tertiary-font-size
  color: $text-muted

.tag-actions
  grid-column: 2 / -1
  display: grid
  grid-template-columns: subgrid
  opacity: 0
  transition: opacity $transition-normal

// The two panels side by side need 600px of the column; below it the groups stand over the tags.
@container tags-page (max-width: 599px)
  .moderation-tags
    grid-template-columns: 1fr
</style>

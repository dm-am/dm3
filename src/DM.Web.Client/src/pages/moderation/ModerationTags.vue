<script setup lang="ts">
import { ref, computed, onMounted, reactive } from "vue";
import { useModal } from "vue-final-modal";
import {
  gameTagApi,
  useGamesStore,
  type ModerationTagGroup,
  type ModerationTag,
} from "@/entities/game";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure, notifyFailure } from "@/shared/lib/errors";
import { pluralize } from "@/shared/lib/utils/pluralize";
import TagGroupDialog from "./dialogs/TagGroupDialog.vue";
import TagDialog from "./dialogs/TagDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("SeniorModerator");
const toast = useToast();
const gamesStore = useGamesStore();

// State
const groups = ref<ModerationTagGroup[]>([]);
const tags = ref<ModerationTag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

// Selected group for filtering tags
const selectedGroupId = ref<string | null>(null);

// Computed
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

// Actions
async function loadData() {
  loading.value = true;
  error.value = null;
  try {
    const [groupsRes, tagsRes] = await Promise.all([
      gameTagApi.getTagGroups(),
      gameTagApi.getTags(),
    ]);
    // A failed load is the page's own state, not a toast: there is nothing on
    // screen to go back to, so the message belongs where the table would be.
    const failure = groupsRes.error ?? tagsRes.error;
    if (failure) {
      error.value = describeFailure(failure, "Не удалось загрузить данные");
      return;
    }
    groups.value = groupsRes.data?.resources ?? [];
    tags.value = tagsRes.data?.resources ?? [];
  } finally {
    loading.value = false;
  }
}

/**
 * This page reads `moderation/tags`, which carries no cache policy; the rest of
 * the site reads the same catalogue from `games/tags`, which answers
 * `public, max-age=300`. The write goes to a third address, so nothing
 * invalidates the copy the browser holds for the public one — the tag picker on
 * "Создать игру" would show the pre-edit list for the next five minutes, and
 * survive a reload doing it. So after a write here the public catalogue is
 * re-read from the origin.
 */
async function reloadAfterWrite() {
  await Promise.all([loadData(), gamesStore.fetchTags(true)]);
}

function selectGroup(groupId: string | null) {
  selectedGroupId.value = groupId;
}

// --- Group dialog (shared Dialog idiom) ---
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

// --- Group delete (ConfirmDialog) ---
const deleteGroupTarget = ref<ModerationTagGroup | null>(null);
const deletingGroup = ref(false);

function deleteGroup(group: ModerationTagGroup) {
  if (group.tagsCount > 0) {
    toast.error("Нельзя удалить группу, в которой есть теги");
    return;
  }
  deleteGroupTarget.value = group;
}

async function confirmDeleteGroup() {
  if (!deleteGroupTarget.value || deletingGroup.value) return;
  deletingGroup.value = true;
  try {
    const { error } = await gameTagApi.deleteTagGroup(
      deleteGroupTarget.value.id,
    );
    if (error) {
      notifyFailure(error, "Не удалось удалить группу тегов");
      return;
    }
    if (selectedGroupId.value === deleteGroupTarget.value.id) {
      selectedGroupId.value = null;
    }
    deleteGroupTarget.value = null;
    await reloadAfterWrite();
  } finally {
    deletingGroup.value = false;
  }
}

// --- Tag dialog (shared Dialog idiom) ---
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

function openEditTagModal(tag: ModerationTag) {
  editingTag.value = tag;
  openTagDialog();
}

// --- Tag delete (ConfirmDialog) ---
const deleteTagTarget = ref<ModerationTag | null>(null);
const deletingTag = ref(false);

function deleteTag(tag: ModerationTag) {
  if (tag.gamesCount > 0) {
    const games = pluralize(tag.gamesCount, "игре", "играх", "играх");
    toast.error(
      `Нельзя удалить тег, который используется в ${tag.gamesCount} ${games}`,
    );
    return;
  }
  deleteTagTarget.value = tag;
}

async function confirmDeleteTag() {
  if (!deleteTagTarget.value || deletingTag.value) return;
  deletingTag.value = true;
  try {
    const { error } = await gameTagApi.deleteTag(deleteTagTarget.value.id);
    if (error) {
      notifyFailure(error, "Не удалось удалить тег");
      return;
    }
    deleteTagTarget.value = null;
    await reloadAfterWrite();
  } finally {
    deletingTag.value = false;
  }
}

onMounted(() => {
  loadData();
});
</script>

<template>
  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <div v-else class="moderation-tags">
    <div v-if="loading" class="loading">Загрузка...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <template v-else>
      <!-- Groups panel -->
      <div class="groups-panel">
        <div class="panel-header">
          <h3>Группы тегов</h3>
          <button type="button" class="btn-add" @click="openCreateGroupModal">
            + Группа
          </button>
        </div>
        <div class="groups-list">
          <button
            type="button"
            class="group-item"
            :class="{ active: selectedGroupId === null }"
            @click="selectGroup(null)"
          >
            <span class="group-title">Все теги</span>
            <span class="group-count">{{ tags.length }}</span>
          </button>
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
                <button
                  type="button"
                  class="btn-icon"
                  aria-label="Редактировать группу"
                  @click.stop="openEditGroupModal(group)"
                >
                  <SvgIcon name="pencil" />
                </button>
              </Tooltip>
              <Tooltip text="Удалить">
                <button
                  type="button"
                  class="btn-icon btn-danger"
                  aria-label="Удалить группу"
                  :disabled="group.tagsCount > 0"
                  @click.stop="deleteGroup(group)"
                >
                  <SvgIcon name="trash" />
                </button>
              </Tooltip>
            </div>
          </div>
        </div>
      </div>

      <!-- Tags panel -->
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
              <div v-if="tag.description" class="tag-description">
                {{ tag.description }}
              </div>
              <div class="tag-meta">
                <span class="tag-group">{{ tag.groupTitle }}</span>
                <span class="tag-games">{{ gamesLabel(tag.gamesCount) }}</span>
                <span class="tag-sort">Порядок: {{ tag.sortOrder }}</span>
              </div>
            </div>
            <div class="tag-actions">
              <Tooltip text="Редактировать">
                <button
                  type="button"
                  class="btn-icon"
                  aria-label="Редактировать тег"
                  @click="openEditTagModal(tag)"
                >
                  <SvgIcon name="pencil" />
                </button>
              </Tooltip>
              <Tooltip text="Удалить">
                <button
                  type="button"
                  class="btn-icon btn-danger"
                  aria-label="Удалить тег"
                  :disabled="tag.gamesCount > 0"
                  @click="deleteTag(tag)"
                >
                  <SvgIcon name="trash" />
                </button>
              </Tooltip>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- Delete confirmations -->
    <ConfirmDialog
      :show="deleteGroupTarget !== null"
      title="Удаление группы"
      :message="`Удалить группу &quot;${deleteGroupTarget?.title ?? ''}&quot;?`"
      confirm-label="Удалить"
      danger
      :loading="deletingGroup"
      @update:show="(v) => !v && (deleteGroupTarget = null)"
      @confirm="confirmDeleteGroup"
    />
    <ConfirmDialog
      :show="deleteTagTarget !== null"
      title="Удаление тега"
      :message="`Удалить тег &quot;${deleteTagTarget?.title ?? ''}&quot;?`"
      confirm-label="Удалить"
      danger
      :loading="deletingTag"
      @update:show="(v) => !v && (deleteTagTarget = null)"
      @confirm="confirmDeleteTag"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.moderation-tags
  display: grid
  grid-template-columns: 300px 1fr
  gap: $medium

.loading,
.error
  grid-column: 1 / -1
  padding: $large

.error
  color: $accent-red

// Panels
.groups-panel,
.tags-panel
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
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
    font-weight: 500

.btn-add
  +button

// Groups list
.groups-list
  padding: $small

.group-item-wrapper
  display: flex
  align-items: center
  gap: $tiny

  &:hover .group-actions
    opacity: 1

.group-item
  flex: 1
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
    background: $bg-element-accent

  &.active
    background: $link
    color: $text-on-fill

.group-title
  flex: 1

.group-count
  font-size: $tertiary-font-size
  color: inherit
  opacity: 0.7

.group-actions
  display: flex
  gap: $tiny
  opacity: 0
  transition: opacity 0.15s

// Tags list
.tags-list
  padding: $small

.tag-item
  display: flex
  justify-content: space-between
  align-items: flex-start
  padding: $small $medium
  margin-bottom: $small
  border: 1px solid $border
  border-radius: $border-radius
  background: $bg-element-accent

  &:hover .tag-actions
    opacity: 1

.tag-info
  flex: 1

.tag-header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $tiny

.tag-title
  font-weight: 500

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
  display: flex
  gap: $tiny
  opacity: 0
  transition: opacity 0.15s

// Icon buttons
.btn-icon
  display: flex
  align-items: center
  justify-content: center
  width: 28px
  height: 28px
  padding: 0
  cursor: pointer
  border: none
  border-radius: $border-radius
  background: none
  color: $text-muted

  &:hover
    background: $bg-element-accent
    color: $text

  &:disabled
    opacity: 0.3
    cursor: default

  &.btn-danger:hover:not(:disabled)
    +tint($accent-red, 10%)
    color: $accent-red

  svg
    width: 18px
    height: 18px

// Responsive
@media (max-width: $bp-tablet)
  .moderation-tags
    grid-template-columns: 1fr
</style>

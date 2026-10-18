<script setup lang="ts">
import { ref, computed, onMounted, reactive } from "vue";
import { useModal } from "vue-final-modal";
import {
  gameTagApi,
  useGamesStore,
  type ModerationTagGroup,
  type ModerationTag,
} from "@/entities/game";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";
import { pluralize } from "@/shared/lib/utils/pluralize";
import TagGroupDialog from "./dialogs/TagGroupDialog.vue";
import TagDialog from "./dialogs/TagDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(sectionRole("moderation-tags"));
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

/**
 * Both reads answer for themselves, because this page does.
 *
 * The failure stands where the two panels would be, so the interceptor's plate
 * over it said the same sentence a second time — word for word on a 403 and a
 * 5xx, since both read it out of the same problem document. Without the strip
 * the plate alone would be left explaining an empty section from the corner.
 *
 * An expired session and a dead connection are not in here: neither is about
 * the tag catalogue, and only the interceptor can offer the way back in.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

// Actions
async function loadData() {
  loading.value = true;
  error.value = null;
  try {
    const [groupsRes, tagsRes] = await Promise.all([
      gameTagApi.getTagGroups(OWNS_REFUSAL),
      gameTagApi.getTags(OWNS_REFUSAL),
    ]);
    // A failed load is the page's own state, not a toast: there is nothing on
    // screen to go back to, so the message belongs where the table would be.
    const failure = groupsRes.error ?? tagsRes.error;
    if (failure) {
      // A wait is said in the shared wording, the one the interceptor would
      // have raised: it is the only one that names the seconds the server asked
      // for, and the title sent with a 429 carries no number.
      error.value =
        failure.status === 429
          ? describeRateLimit(failure.retryAfterSeconds)
          : describeFailure(failure, "Не удалось загрузить данные");
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

function deleteGroup(group: ModerationTagGroup) {
  // The trash control is drawn only for a group without tags, and the count
  // stands next to its name, so this guard only repeats what the row shows.
  if (group.tagsCount > 0) return;
  deleteGroupTarget.value = group;
}

// The requests of the two confirmations. The window waits for one, prints a
// refusal in itself and closes on success alone, so the interceptor keeps quiet
// about it the way it does about the reads.
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

function deleteTag(tag: ModerationTag) {
  // Same as deleteGroup: the trash control is drawn only for a tag no game
  // uses, and the row already says in how many games it stands.
  if (tag.gamesCount > 0) return;
  deleteTagTarget.value = tag;
}

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
  <!-- The frame the two panels measure themselves against: the page, not
       the window. -->
  <div class="tags-page">
    <page-title>Игровые теги</page-title>

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
            <!-- In the frame of a group row, which stretches its button: a
                 button outside it is as wide as its words, and the count stood
                 against the title. -->
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
                  <button
                    type="button"
                    class="btn-icon"
                    aria-label="Редактировать группу"
                    @click.stop="openEditGroupModal(group)"
                  >
                    <SvgIcon name="pencil" />
                  </button>
                </Tooltip>
                <!-- The server refuses to delete a group that holds tags,
                     so a group with tags has no trash: a pale control that
                     does nothing was the refusal drawn in advance. -->
                <Tooltip v-if="group.tagsCount === 0" text="Удалить">
                  <button
                    type="button"
                    class="btn-icon"
                    aria-label="Удалить группу"
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
                <!-- As a reader sees it in the tag's tooltip: the words of a
                     [tip] under a dotted line, their small tooltip on hover. -->
                <div v-if="tag.description" class="tag-description">
                  <TooltipContent
                    :html="tag.descriptionHtml"
                    :text="tag.description"
                  />
                </div>
                <div class="tag-meta">
                  <span class="tag-group">{{ tag.groupTitle }}</span>
                  <span class="tag-games">{{
                    gamesLabel(tag.gamesCount)
                  }}</span>
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
                <!-- Same for a tag that stands in a game. -->
                <Tooltip v-if="tag.gamesCount === 0" text="Удалить">
                  <button
                    type="button"
                    class="btn-icon"
                    aria-label="Удалить тег"
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
@use "@/assets/styles/Inputs" as *

// The side of an icon button (.btn-icon), and the room a row keeps for two of
// them: the pencil, then the trash where there is something to delete. The
// actions of a group and of a tag take it, and .group-item--all leaves it.
$icon-button: 28px
$two-icon-buttons: calc(2 * #{$icon-button} + #{$tiny})

// Inline-size containment keeps the width of the page from its ancestors.
.tags-page
  container: tags-page / inline-size

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
    background: $bg-element-hover

  // A choice, not a button: the chosen group wears the active segment of the
  // site ($bg-element-accent and the bold), so the one fill of $link on a page
  // stays the main button's.
  &.active
    background: $bg-element-accent
    color: $text
    font-weight: 700

// The row of every tag has no buttons of its own. It stops where a group row
// stops before its two and the gaps around them, so the counts stand in one
// column.
.group-item--all
  margin-right: calc(#{$two-icon-buttons} + #{$tiny})

.group-title
  flex: 1

.group-count
  font-size: $tertiary-font-size
  color: inherit
  opacity: 0.7

// The room of both buttons whether the trash is drawn or not, so a group that
// holds tags keeps its count and its pencil in the column of the others.
.group-actions
  display: flex
  gap: $tiny
  flex: none
  width: $two-icon-buttons
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
  flex: none
  width: $two-icon-buttons
  opacity: 0
  transition: opacity 0.15s

// Icon buttons
.btn-icon
  display: flex
  align-items: center
  justify-content: center
  width: $icon-button
  height: $icon-button
  padding: 0
  cursor: pointer
  border: none
  border-radius: $border-radius
  background: none
  color: $text-muted

  // Deleting wears no colour of its own: the question it asks first spells
  // it out.
  &:hover
    background: $bg-element-accent
    color: $text

  svg
    width: 18px
    height: 18px

// The two panels side by side need 600px of the column: the list of groups
// keeps its 300px, and a narrower list of tags cut the right edge of its cards
// off. At 1280 the column is 556px, and there the groups stand over the tags.
@container tags-page (max-width: 599px)
  .moderation-tags
    grid-template-columns: 1fr
</style>

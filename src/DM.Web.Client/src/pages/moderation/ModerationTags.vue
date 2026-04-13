<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import ModerationApi, {
  type ModerationTagGroup,
  type ModerationTag,
} from "@/shared/api/moderationApi";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { EmptyState } from "@/shared/ui";

// State
const groups = ref<ModerationTagGroup[]>([]);
const tags = ref<ModerationTag[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

// Selected group for filtering tags
const selectedGroupId = ref<string | null>(null);

// Edit state
const editingGroup = ref<ModerationTagGroup | null>(null);
const editingTag = ref<ModerationTag | null>(null);
const showGroupModal = ref(false);
const showTagModal = ref(false);
const isCreatingGroup = ref(false);
const isCreatingTag = ref(false);

// Form fields
const groupForm = ref({
  title: "",
  description: "",
  sortOrder: 0,
});

const tagForm = ref({
  groupId: "",
  title: "",
  description: "",
  sortOrder: 0,
});

// Computed
const filteredTags = computed(() => {
  if (!selectedGroupId.value) return tags.value;
  return tags.value.filter((t) => t.groupId === selectedGroupId.value);
});

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
      ModerationApi.getTagGroups(),
      ModerationApi.getTags(),
    ]);
    groups.value = groupsRes.data?.resources ?? [];
    tags.value = tagsRes.data?.resources ?? [];
  } catch (e) {
    error.value = "Не удалось загрузить данные";
    console.error(e);
  } finally {
    loading.value = false;
  }
}

function selectGroup(groupId: string | null) {
  selectedGroupId.value = groupId;
}

// Group modal
function openCreateGroupModal() {
  isCreatingGroup.value = true;
  editingGroup.value = null;
  groupForm.value = {
    title: "",
    description: "",
    sortOrder: groups.value.length,
  };
  showGroupModal.value = true;
}

function openEditGroupModal(group: ModerationTagGroup) {
  isCreatingGroup.value = false;
  editingGroup.value = group;
  groupForm.value = {
    title: group.title,
    description: group.description ?? "",
    sortOrder: group.sortOrder,
  };
  showGroupModal.value = true;
}

function closeGroupModal() {
  showGroupModal.value = false;
  editingGroup.value = null;
}

async function saveGroup() {
  try {
    if (isCreatingGroup.value) {
      await ModerationApi.createTagGroup({
        title: groupForm.value.title,
        description: groupForm.value.description || undefined,
        sortOrder: groupForm.value.sortOrder,
      });
    } else if (editingGroup.value) {
      await ModerationApi.updateTagGroup(editingGroup.value.id, {
        title: groupForm.value.title,
        description: groupForm.value.description || undefined,
        sortOrder: groupForm.value.sortOrder,
      });
    }
    closeGroupModal();
    await loadData();
  } catch (e) {
    console.error(e);
    alert("Ошибка при сохранении группы");
  }
}

async function deleteGroup(group: ModerationTagGroup) {
  if (group.tagsCount > 0) {
    alert("Нельзя удалить группу, в которой есть теги");
    return;
  }
  if (!confirm(`Удалить группу "${group.title}"?`)) return;
  try {
    await ModerationApi.deleteTagGroup(group.id);
    if (selectedGroupId.value === group.id) {
      selectedGroupId.value = null;
    }
    await loadData();
  } catch (e) {
    console.error(e);
    alert("Ошибка при удалении группы");
  }
}

// Tag modal
function openCreateTagModal() {
  isCreatingTag.value = true;
  editingTag.value = null;
  tagForm.value = {
    groupId: selectedGroupId.value ?? groups.value[0]?.id ?? "",
    title: "",
    description: "",
    sortOrder: filteredTags.value.length,
  };
  showTagModal.value = true;
}

function openEditTagModal(tag: ModerationTag) {
  isCreatingTag.value = false;
  editingTag.value = tag;
  tagForm.value = {
    groupId: tag.groupId,
    title: tag.title,
    description: tag.description ?? "",
    sortOrder: tag.sortOrder,
  };
  showTagModal.value = true;
}

function closeTagModal() {
  showTagModal.value = false;
  editingTag.value = null;
}

async function saveTag() {
  try {
    if (isCreatingTag.value) {
      await ModerationApi.createTag({
        groupId: tagForm.value.groupId,
        title: tagForm.value.title,
        description: tagForm.value.description || undefined,
        sortOrder: tagForm.value.sortOrder,
      });
    } else if (editingTag.value) {
      await ModerationApi.updateTag(editingTag.value.id, {
        groupId: tagForm.value.groupId,
        title: tagForm.value.title,
        description: tagForm.value.description || undefined,
        sortOrder: tagForm.value.sortOrder,
      });
    }
    closeTagModal();
    await loadData();
  } catch (e) {
    console.error(e);
    alert("Ошибка при сохранении тега");
  }
}

async function deleteTag(tag: ModerationTag) {
  if (tag.gamesCount > 0) {
    alert(`Нельзя удалить тег, который используется в ${tag.gamesCount} играх`);
    return;
  }
  if (!confirm(`Удалить тег "${tag.title}"?`)) return;
  try {
    await ModerationApi.deleteTag(tag.id);
    await loadData();
  } catch (e) {
    console.error(e);
    alert("Ошибка при удалении тега");
  }
}

onMounted(() => {
  loadData();
});
</script>

<template>
  <div class="moderation-tags">
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
          :title="selectedGroupId ? 'Нет тегов в этой группе' : 'Тегов пока нет'"
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
                <span class="tag-games">{{ tag.gamesCount }} игр</span>
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

    <!-- Group modal -->
    <div
      v-if="showGroupModal"
      class="modal-overlay"
      @click.self="closeGroupModal"
    >
      <div class="modal">
        <h3>{{ isCreatingGroup ? "Новая группа" : "Редактировать группу" }}</h3>
        <form @submit.prevent="saveGroup">
          <div class="form-field">
            <label>Название</label>
            <input v-model="groupForm.title" type="text" required />
          </div>
          <div class="form-field">
            <label>Описание</label>
            <textarea v-model="groupForm.description" rows="3" />
          </div>
          <div class="form-field">
            <label>Порядок сортировки</label>
            <input v-model.number="groupForm.sortOrder" type="number" min="0" />
          </div>
          <div class="modal-actions">
            <button
              type="button"
              class="btn-secondary"
              @click="closeGroupModal"
            >
              Отмена
            </button>
            <button type="submit" class="btn-primary">Сохранить</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Tag modal -->
    <div v-if="showTagModal" class="modal-overlay" @click.self="closeTagModal">
      <div class="modal">
        <h3>{{ isCreatingTag ? "Новый тег" : "Редактировать тег" }}</h3>
        <form @submit.prevent="saveTag">
          <div class="form-field">
            <label>Группа</label>
            <select v-model="tagForm.groupId" required>
              <option v-for="group in groups" :key="group.id" :value="group.id">
                {{ group.title }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label>Название</label>
            <input v-model="tagForm.title" type="text" required />
          </div>
          <div class="form-field">
            <label>Описание</label>
            <textarea v-model="tagForm.description" rows="3" />
            <small class="field-hint"
              >Используйте [tipimg:URL]текст[/tipimg] для картинки в тултипе</small
            >
          </div>
          <div class="form-field">
            <label>Порядок сортировки</label>
            <input v-model.number="tagForm.sortOrder" type="number" min="0" />
          </div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" @click="closeTagModal">
              Отмена
            </button>
            <button type="submit" class="btn-primary">Сохранить</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Layout"
@import "src/assets/styles/Themes"
@import "src/assets/styles/ZIndex"

.moderation-tags
  display: grid
  grid-template-columns: 300px 1fr
  gap: $medium

.loading,
.error
  grid-column: 1 / -1
  padding: $large
  text-align: center

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
  padding: $tiny $small
  font-size: $secondary-font-size
  font-family: inherit
  cursor: pointer
  border: 1px solid $link
  border-radius: $border-radius
  background: $link
  color: white

  &:hover
    background: $link-hover

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
    color: white

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
    cursor: not-allowed

  &.btn-danger:hover:not(:disabled)
    background: rgba($accent-red, 0.1)
    color: $accent-red

  svg
    width: 18px
    height: 18px

// Modal
.modal-overlay
  position: fixed
  top: 0
  left: 0
  right: 0
  bottom: 0
  display: flex
  align-items: center
  justify-content: center
  background: rgba(0, 0, 0, 0.5)
  z-index: $z-modal

.modal
  width: 100%
  max-width: 450px
  padding: $large
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  box-shadow: 0 4px 20px $shadow-color

  h3
    margin: 0 0 $medium 0
    font-size: $font-size
    font-weight: 500

.form-field
  margin-bottom: $medium

  label
    display: block
    margin-bottom: $tiny
    font-size: $secondary-font-size
    color: $text-muted

  input,
  textarea,
  select
    width: 100%
    padding: $small
    font-size: $secondary-font-size
    font-family: inherit
    border: 1px solid $border
    border-radius: $border-radius
    background: $bg-element
    color: $text
    box-sizing: border-box

    &:focus
      outline: none
      border-color: $link

  textarea
    resize: vertical

.field-hint
  display: block
  margin-top: $tiny
  font-size: $tertiary-font-size
  color: $text-muted

.modal-actions
  display: flex
  justify-content: flex-end
  gap: $small
  margin-top: $large

.btn-primary,
.btn-secondary
  padding: $small $medium
  font-size: $secondary-font-size
  font-family: inherit
  cursor: pointer
  border-radius: $border-radius

.btn-primary
  border: 1px solid $link
  background: $link
  color: white

  &:hover
    background: $link-hover

.btn-secondary
  border: 1px solid $border
  background: $bg-element
  color: $text

  &:hover
    background: $bg-element-accent

// Responsive
@media (max-width: 768px)
  .moderation-tags
    grid-template-columns: 1fr
</style>

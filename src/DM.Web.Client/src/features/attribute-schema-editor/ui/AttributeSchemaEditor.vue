<script setup lang="ts">
/**
 * AttributeSchemaEditor — reusable editor for a game's attribute schema.
 * Embedded inline in game creation and in dedicated schema/settings pages.
 *
 * Composition (doc 4.2.2.10 / 4.2.3.5.12):
 *  - copy-existing-schema picker (Select of public + own schemas) + Скопировать
 *    (client-side copy of specs into the draft; persisted only on Save; copied
 *    specs receive fresh ids so a save never re-uses the source schema's ids)
 *  - the attribute table (row hover, empty-state) with per-row pencil/trash,
 *    Приватность / Обязательность checkboxes and a single-select Дескриптор
 *    radio (hidden for BbCode rows)
 *  - a full-width "+ Новый атрибут" row-styled add button
 *  - a region that shows either the inline attribute edit form OR the
 *    character-creation preview (the edit form replaces the preview)
 *  - Сохранить / Отмена / Превью actions, with a data-loss confirm modal
 *    when the game already has characters
 */
import { ref, computed, watch, nextTick, onMounted } from "vue";
import type { AttributeSpecification, AttributeSchema } from "@/entities/game";
import { AttributeSchemaType, gameApi } from "@/entities/game";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { SvgIcon } from "@/shared/ui/Icon";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import AttributeEditForm from "./AttributeEditForm.vue";
import CharacterFormPreview from "./CharacterFormPreview.vue";
import {
  SPEC_TYPE_LABELS,
  createEmptySchema,
  createEmptySpec,
  cloneSchema,
  cloneSpecsWithNewIds,
  isBbCode,
} from "@/entities/game";

const props = withDefaults(
  defineProps<{
    /** The schema draft (v-model). Null means "no schema yet". */
    modelValue: AttributeSchema | null;
    /**
     * Whether the game already has characters. When true, saving prompts a
     * data-loss confirmation because schema edits can invalidate sheets.
     */
    hasCharacters?: boolean;
    /** Show the Сохранить/Отмена/Превью action bar (hidden when the host
     * page drives persistence, e.g. inline in game creation). */
    showActions?: boolean;
    /** External save-in-flight indicator for the Сохранить button. */
    saving?: boolean;
  }>(),
  {
    hasCharacters: false,
    showActions: true,
    saving: false,
  },
);

const emit = defineEmits<{
  "update:modelValue": [AttributeSchema];
  save: [AttributeSchema];
  cancel: [];
}>();

const schemaTypeOptions = [
  { value: AttributeSchemaType.Private, label: "Приватная (только моя)" },
  { value: AttributeSchemaType.Public, label: "Публичная (доступна всем)" },
];

// --- Draft state, synced with the v-model ---------------------------------

const draft = ref<AttributeSchema>(
  props.modelValue ? cloneSchema(props.modelValue) : createEmptySchema(),
);

let syncingFromProp = false;
watch(
  () => props.modelValue,
  (incoming) => {
    const next = incoming ? cloneSchema(incoming) : createEmptySchema();
    if (JSON.stringify(next) === JSON.stringify(draft.value)) return;
    syncingFromProp = true;
    draft.value = next;
    nextTick(() => (syncingFromProp = false));
  },
);

watch(
  draft,
  (value) => {
    if (syncingFromProp) return;
    emit("update:modelValue", cloneSchema(value));
  },
  { deep: true },
);

// --- Copy-existing-schema picker ------------------------------------------

const schemas = ref<AttributeSchema[]>([]);
const selectedCopyId = ref("");

const copyOptions = computed(() =>
  schemas.value
    .filter((s) => s.id)
    .map((s) => ({ value: s.id as string, label: s.title })),
);

onMounted(async () => {
  const { data } = await gameApi.getSchemas();
  if (data) schemas.value = data.resources;
});

function copySchema() {
  const source = schemas.value.find((s) => s.id === selectedCopyId.value);
  if (!source) return;
  draft.value.specifications = cloneSpecsWithNewIds(source.specifications);
  editingId.value = null;
  showPreview.value = true;
}

// --- Attribute table + inline edit ----------------------------------------

const editingId = ref<string | null>(null);
const showPreview = ref(true);

const editingSpec = computed(
  () =>
    draft.value.specifications.find((s) => s.id === editingId.value) ?? null,
);

// Writable proxy for the two-way bound edit form. The setter swaps the element
// in place; the form only mutates nested fields so it is rarely invoked.
const editingModel = computed<AttributeSpecification>({
  get: () => editingSpec.value as AttributeSpecification,
  set: (value) => {
    const index = draft.value.specifications.findIndex(
      (s) => s.id === editingId.value,
    );
    if (index !== -1) draft.value.specifications[index] = value;
  },
});

function reindex() {
  draft.value.specifications.forEach((s, i) => (s.order = i));
}

function addAttribute() {
  const spec = createEmptySpec(draft.value.specifications.length);
  draft.value.specifications.push(spec);
  editingId.value = spec.id;
}

function editAttribute(id: string) {
  editingId.value = id;
}

function removeAttribute(id: string) {
  const index = draft.value.specifications.findIndex((s) => s.id === id);
  if (index !== -1) draft.value.specifications.splice(index, 1);
  if (editingId.value === id) editingId.value = null;
  reindex();
}

function setDescriptor(id: string) {
  draft.value.specifications.forEach((s) => (s.isDescriptor = s.id === id));
}

function finishEdit() {
  editingId.value = null;
}

function togglePreview() {
  if (editingId.value) {
    editingId.value = null;
    showPreview.value = true;
  } else {
    showPreview.value = !showPreview.value;
  }
}

// --- Save / cancel + data-loss confirmation -------------------------------

const showDataLossModal = ref(false);
const titleError = ref("");

function validate(): boolean {
  titleError.value = "";
  if (!draft.value.title.trim()) {
    titleError.value = "Введите название схемы";
    return false;
  }
  return true;
}

function requestSave() {
  if (!validate()) return;
  if (props.hasCharacters) {
    showDataLossModal.value = true;
    return;
  }
  commitSave();
}

function commitSave() {
  showDataLossModal.value = false;
  reindex();
  emit("save", cloneSchema(draft.value));
}

defineExpose({ validate, requestSave });
</script>

<template>
  <div class="schema-editor">
    <!-- Copy-existing-schema picker -->
    <div class="copy-row">
      <div class="copy-select">
        <Select
          v-model="selectedCopyId"
          :options="copyOptions"
          placeholder="Скопировать существующую схему"
        />
      </div>
      <Button type="button" :disabled="!selectedCopyId" @click="copySchema">
        Скопировать
      </Button>
    </div>

    <!-- Schema meta -->
    <form-field
      label="Название схемы *"
      name="schema-title"
      :errors="titleError ? [titleError] : []"
    >
      <input
        v-model="draft.title"
        type="text"
        placeholder="Например, D&D 5e"
        maxlength="100"
        @input="titleError = ''"
      />
    </form-field>

    <form-field label="Доступность">
      <Select
        :model-value="draft.type"
        :options="schemaTypeOptions"
        @update:model-value="draft.type = $event as AttributeSchemaType"
      />
    </form-field>

    <!-- Attribute table -->
    <block-title>Атрибуты</block-title>
    <div class="table-wrap">
      <table class="attr-table">
        <thead>
          <tr>
            <th>Название</th>
            <th>Тип</th>
            <th class="col-flag">Приватность</th>
            <th class="col-flag">Обязательность</th>
            <th class="col-flag">Дескриптор</th>
            <th class="col-actions" aria-label="Действия"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="draft.specifications.length === 0" class="empty-row">
            <td colspan="6" class="empty-cell">Атрибутов пока нет</td>
          </tr>
          <tr
            v-for="spec in draft.specifications"
            :key="spec.id"
            class="attr-row"
            :class="{ editing: spec.id === editingId }"
          >
            <td class="col-name">{{ spec.title || "Без названия" }}</td>
            <td class="col-type">{{ SPEC_TYPE_LABELS[spec.type] }}</td>
            <td class="col-flag">
              <input
                v-model="spec.isHidden"
                type="checkbox"
                :aria-label="`Приватность: ${spec.title || 'атрибут'}`"
              />
            </td>
            <td class="col-flag">
              <input
                v-model="spec.required"
                type="checkbox"
                :aria-label="`Обязательность: ${spec.title || 'атрибут'}`"
              />
            </td>
            <td class="col-flag">
              <input
                v-if="!isBbCode(spec.type)"
                type="radio"
                name="schema-descriptor"
                :checked="spec.isDescriptor"
                :aria-label="`Дескриптор: ${spec.title || 'атрибут'}`"
                @change="setDescriptor(spec.id)"
              />
              <span v-else class="dash" aria-hidden="true">{{
                VALUE_UNAVAILABLE
              }}</span>
            </td>
            <td class="col-actions">
              <button
                type="button"
                class="icon-btn"
                aria-label="Редактировать атрибут"
                @click="editAttribute(spec.id)"
              >
                <SvgIcon name="pencil" />
              </button>
              <button
                type="button"
                class="icon-btn danger"
                aria-label="Удалить атрибут"
                @click="removeAttribute(spec.id)"
              >
                <SvgIcon name="trash" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <button type="button" class="add-attribute" @click="addAttribute">
      + Новый атрибут
    </button>

    <!-- Region: inline edit OR character-creation preview -->
    <div class="editor-region">
      <AttributeEditForm
        v-if="editingSpec"
        v-model="editingModel"
        @done="finishEdit"
      />
      <CharacterFormPreview v-else-if="showPreview" :schema="draft" />
    </div>

    <!-- Action bar -->
    <div v-if="showActions" class="editor-actions">
      <Button type="button" :loading="saving" @click="requestSave">
        Сохранить
      </Button>
      <Button type="button" @click="emit('cancel')">Отмена</Button>
      <Button type="button" @click="togglePreview">Превью</Button>
    </div>

    <!-- Data-loss confirmation -->
    <ConfirmDialog
      v-model:show="showDataLossModal"
      title="Изменение схемы атрибутов"
      message="В игре уже есть персонажи. Изменение схемы может затронуть заполненные анкеты. Сохранить изменения?"
      confirm-label="Сохранить"
      @confirm="commitSave"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.schema-editor
  display: flex
  flex-direction: column
  gap: $small

.copy-row
  display: flex
  gap: $small
  align-items: stretch

  .copy-select
    flex: 1

// --- Table ---
.table-wrap
  overflow-x: auto

.attr-table
  width: 100%
  border-collapse: collapse

  th
    text-align: left
    padding: $small
    font-size: $secondary-font-size
    color: $text-muted
    font-weight: normal
    border-bottom: 1px solid $border
    white-space: nowrap

  td
    padding: $small
    border-bottom: 1px solid $border
    vertical-align: middle

  .col-flag
    text-align: center
    width: $grid-step * 24

  .col-actions
    width: $grid-step * 16
    white-space: nowrap
    text-align: right

.attr-row
  transition: background-color 0.15s ease

  &:hover
    background-color: $bg-element-hover

  &.editing
    background-color: $bg-element-accent

.col-name
  font-weight: bold

.col-type
  color: $text-muted
  font-size: $secondary-font-size

.dash
  color: $text-muted

input[type="checkbox"],
input[type="radio"]
  width: auto
  margin: 0
  cursor: pointer

.empty-cell
  padding: $big
  color: $text-muted

.icon-btn
  padding: $tiny $small
  background: none
  border: none
  color: $text-muted
  cursor: pointer
  font-size: 1.1em

  &:hover
    color: $text

  &.danger:hover
    color: $accent-red

.add-attribute
  width: 100%
  padding: $small
  background: none
  border: 1px dashed $border
  border-radius: $border-radius
  color: $link
  cursor: pointer
  transition: background-color 0.15s ease, color 0.15s ease

  &:hover
    background-color: $bg-element-hover
    color: $link-hover

.editor-region
  margin-top: $small

.editor-actions
  display: flex
  gap: $small
  margin-top: $small

// --- Modal ---
</style>

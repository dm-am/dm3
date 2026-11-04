<script setup lang="ts">
// One editor for game creation, game settings and site templates; persistence is the host's.
import { ref, computed, watch, nextTick } from "vue";
import type {
  AttributeSpecification,
  AttributeSchemaDraft,
} from "@/entities/game";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import AttributeEditForm from "./AttributeEditForm.vue";
import CharacterFormPreview from "./CharacterFormPreview.vue";
import type { SchemaSource } from "../model/schemaSources";
import {
  SPEC_TYPE_LABELS,
  createEmptySchema,
  createEmptySpec,
  cloneSchema,
  cloneSpecsWithNewIds,
  isBbCode,
} from "@/entities/game";
import type { RequestOptions } from "@/shared/api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH } from "@/shared/lib/constants/game";

const props = withDefaults(
  defineProps<{
    /** Null means "no schema yet". */
    modelValue: AttributeSchemaDraft | null;
    /** Absent, not empty, hides the list: a host reading its sources shows it from the first frame. */
    sources?: SchemaSource[];
    sourcesRefusal?: string;
    /** A template has a name; a game's sheet is named by its game. */
    showTitle?: boolean;
    /** Schema edits can invalidate filled sheets, so saving asks first. */
    hasCharacters?: boolean;
    /** Hidden when the host drives persistence, as in game creation. */
    showActions?: boolean;
    saving?: boolean;
    /** The host's word that the draft is saved: the only place a schema save is reported. */
    saved?: boolean;
    refusal?: string;
    /** The data-loss confirmation passes the refusal flags; the action bar does not. */
    save?: (
      draft: AttributeSchemaDraft,
      options?: RequestOptions,
    ) => Promise<DialogActionResult>;
  }>(),
  {
    sourcesRefusal: "",
    showTitle: false,
    hasCharacters: false,
    showActions: true,
    saving: false,
    refusal: "",
  },
);

const emit = defineEmits<{
  "update:modelValue": [AttributeSchemaDraft];
  cancel: [];
}>();

const draft = ref<AttributeSchemaDraft>(
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

const selectedSourceValue = ref("");
const copying = ref(false);
const showCopyConfirm = ref(false);

const sourceOptions = computed(() =>
  (props.sources ?? []).map((source) => ({
    value: source.value,
    label: source.label,
  })),
);

const selectedSource = computed(
  () =>
    (props.sources ?? []).find((s) => s.value === selectedSourceValue.value) ??
    null,
);

// The first entry arrives picked, again whenever the list is replaced.
watch(
  () => props.sources,
  (list) => {
    if (!list?.length) return;
    if (list.some((s) => s.value === selectedSourceValue.value)) return;
    selectedSourceValue.value = list[0].value;
  },
  { immediate: true },
);

function requestCopy() {
  if (!selectedSource.value || copying.value) return;
  // A copy replaces the whole set, so a table with anything in it asks first.
  if (draft.value.specifications.length > 0) {
    showCopyConfirm.value = true;
    return;
  }
  copyFromSource();
}

async function copyFromSource() {
  showCopyConfirm.value = false;
  const source = selectedSource.value;
  if (!source) return;

  copying.value = true;
  const specifications = await source.read();
  copying.value = false;
  if (!specifications) return;

  draft.value.specifications = cloneSpecsWithNewIds(specifications);
  editingId.value = null;
  showPreview.value = true;
}

const editingId = ref<string | null>(null);
const showPreview = ref(true);

const editingSpec = computed(
  () =>
    draft.value.specifications.find((s) => s.id === editingId.value) ?? null,
);

// The setter swaps the element in place; the form mutates nested fields, so it is rare.
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

function removeAttribute(id: string) {
  const index = draft.value.specifications.findIndex((s) => s.id === id);
  if (index !== -1) draft.value.specifications.splice(index, 1);
  if (editingId.value === id) editingId.value = null;
  reindex();
}

// Every reader of the game page sees the descriptor, so it is never hidden.
function setDescriptor(id: string) {
  draft.value.specifications.forEach((s) => {
    s.isDescriptor = s.id === id;
    if (s.isDescriptor) s.isHidden = false;
  });
}

function togglePreview() {
  if (editingId.value) {
    editingId.value = null;
    showPreview.value = true;
  } else {
    showPreview.value = !showPreview.value;
  }
}

const showDataLossModal = ref(false);
const titleError = ref("");

function validate(): boolean {
  titleError.value = "";
  if (props.showTitle && !draft.value.title.trim()) {
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
  void props.save?.(outgoing());
}

function outgoing(): AttributeSchemaDraft {
  reindex();
  return cloneSchema(draft.value);
}

function saveConfirmed() {
  return props.save?.(outgoing(), OWNS_REFUSAL);
}
</script>

<template>
  <div class="schema-editor">
    <form-field v-if="sources" label="Схема атрибутов" name="schema-source">
      <div class="copy-row">
        <Select
          id="schema-source"
          v-model="selectedSourceValue"
          class="copy-select"
          :options="sourceOptions"
        />
        <Button
          type="button"
          :disabled="!selectedSource"
          :loading="copying"
          @click="requestCopy"
        >
          Скопировать
        </Button>
      </div>
      <p v-if="sourcesRefusal" class="form-error" role="alert">
        {{ sourcesRefusal }}
      </p>
    </form-field>

    <form-field
      v-if="showTitle"
      label="Название схемы *"
      name="schema-title"
      :errors="titleError ? [titleError] : []"
    >
      <input
        id="schema-title"
        v-model="draft.title"
        type="text"
        placeholder="Например, D&D 5e"
        :maxlength="ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH"
        @input="titleError = ''"
      />
    </form-field>

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
                v-if="!spec.isDescriptor"
                v-model="spec.isHidden"
                type="checkbox"
                :aria-label="`Приватность: ${spec.title || 'атрибут'}`"
              />
              <span v-else class="dash" aria-hidden="true">{{
                VALUE_UNAVAILABLE
              }}</span>
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
              <RowIconButton
                icon="pencil"
                label="Редактировать атрибут"
                @click="editingId = spec.id"
              />
              <RowIconButton
                icon="trash"
                label="Удалить атрибут"
                @click="removeAttribute(spec.id)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <Button type="button" class="add-attribute" @click="addAttribute">
      + Новый атрибут
    </Button>

    <div class="editor-region">
      <AttributeEditForm
        v-if="editingSpec"
        v-model="editingModel"
        @done="editingId = null"
      />
      <CharacterFormPreview v-else-if="showPreview" :schema="draft" />
    </div>

    <div v-if="showActions" class="editor-actions">
      <Button
        type="button"
        variant="primary"
        :loading="saving"
        :disabled="saved"
        @click="requestSave"
      >
        {{ saved ? "Сохранено" : "Сохранить" }}
      </Button>
      <Button type="button" @click="emit('cancel')">Отмена</Button>
      <Button type="button" @click="togglePreview">Превью</Button>
    </div>

    <p v-if="refusal" class="form-error" role="alert">{{ refusal }}</p>

    <ConfirmDialog
      v-model:show="showCopyConfirm"
      title="Скопировать схему атрибутов?"
      message="Параметры выбранной схемы целиком заменят текущие"
      confirm-label="Скопировать"
      @confirm="copyFromSource"
    />

    <ConfirmDialog
      v-model:show="showDataLossModal"
      title="Сохранить схему атрибутов?"
      message="В игре уже есть персонажи, и правка схемы может затронуть их заполненные анкеты"
      confirm-label="Сохранить"
      failure="Не удалось сохранить схему атрибутов"
      :action="saveConfirmed"
    />
  </div>
</template>

<style scoped lang="sass">

.schema-editor
  display: flex
  flex-direction: column
  gap: $small

.copy-row
  display: flex
  gap: $small
  align-items: stretch
  width: 100%

  .copy-select
    flex: 1

// Cells 4 apart keep the six columns under the 600 of the narrowest box; narrower boxes scroll.
.table-wrap
  overflow-x: auto
  contain: inline-size

.attr-table
  width: 100%
  border-collapse: collapse

  th
    text-align: left
    padding: $small $grid-step
    font-size: $secondary-font-size
    color: $text-muted
    font-weight: normal
    border-bottom: 1px solid $border
    white-space: nowrap

  td
    padding: $small $grid-step
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
  transition: background-color $transition-normal

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

// No width: the checkbox side belongs to +checkbox, and auto would collapse it.
input[type="checkbox"],
input[type="radio"]
  margin: 0
  cursor: pointer

.empty-cell
  padding: $big
  color: $text-muted

.add-attribute
  align-self: flex-start

.editor-region
  margin-top: $small

.editor-actions
  display: flex
  gap: $small
  margin-top: $small

.form-error
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

.copy-row + .form-error
  margin-top: $small
</style>

<script setup lang="ts">
/**
 * AttributeSchemaEditor — reusable editor for a character sheet.
 * Embedded inline in game creation, in the game settings and in the
 * moderation section that keeps the site's templates.
 *
 * Composition (doc 4.2.2.10 / 4.2.3.5.12):
 *  - the "Схема атрибутов" list of sources with "Скопировать" beside it, over
 *    the table (a client-side copy of the picked sheet into the draft,
 *    persisted only on Save; copied specs receive fresh ids, so a save never
 *    re-uses the source's ids and never claims the source's stored values)
 *  - the attribute table (row hover, empty-state) with per-row pencil/trash,
 *    "Приватность" / "Обязательность" checkboxes and a single-select "Дескриптор"
 *    radio (hidden for BbCode rows)
 *  - "+ Новый атрибут" right under the table, which adds a row and opens its
 *    form
 *  - a region that shows either the inline attribute edit form OR the
 *    character-creation preview (the edit form replaces the preview)
 *  - "Сохранить" / "Отмена" / "Превью" actions, with a data-loss confirm modal
 *    when the game already has characters
 *
 * One editor, three hosts, and what differs between them arrives as props: the
 * list of sources (the moderation section has none, and a template is named by
 * a field instead), the title field, and whether the game behind it has
 * characters. Persistence is the host's: the editor hands its draft to `save`
 * and never learns what address it goes to.
 *
 * The data-loss confirmation runs that save as its action: it waits shut for
 * the answer, prints a refusal in itself and closes on success alone.
 */
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

/**
 * The flags the data-loss confirmation sends its save with: the window prints
 * a refusal in itself, so the interceptor keeps quiet about a 403, a 429 and a
 * 5xx. A structural twin of the api's own, as NotepadAdapter declares it.
 */
type RefusalFlags = { ownsRefusal?: boolean; ownsRateLimit?: boolean };

const props = withDefaults(
  defineProps<{
    /** The schema draft (v-model). Null means "no schema yet". */
    modelValue: AttributeSchemaDraft | null;
    /**
     * Entries of the "Схема атрибутов" list. Absent, not empty, is what hides
     * the list: the moderation section edits one template and has nothing to
     * copy from, while a host that reads its sources shows the control from the
     * first frame instead of after the answer.
     */
    sources?: SchemaSource[];
    /**
     * Why the list is short of its templates, or why the last copy brought
     * nothing: printed under the list, where the reader is looking. The
     * host owns it with the list (useSchemaSources), and the editor only
     * prints it.
     */
    sourcesRefusal?: string;
    /** Draw the "Название схемы" field. A template has a name; a game's sheet
     * is named by its game. */
    showTitle?: boolean;
    /**
     * Whether the game already has characters. When true, saving prompts a
     * data-loss confirmation because schema edits can invalidate sheets.
     */
    hasCharacters?: boolean;
    /** Show the "Сохранить" / "Отмена" / "Превью" action bar (hidden when the
     * host page drives persistence, e.g. inline in game creation). */
    showActions?: boolean;
    /** External save-in-flight indicator for the "Сохранить" button. */
    saving?: boolean;
    /**
     * The host's answer that the draft on screen is the one the server has.
     *
     * The editor cannot know it: the draft it holds is a copy, and whether a
     * save of it went through is the host's business. When it is true the
     * button reads "Сохранено" and takes no press, which is the only place a
     * schema save can be reported - the editor after a save shows the same
     * list it showed before, and the page does not move.
     */
    saved?: boolean;
    /** The host's refusal of the last save, shown under the actions. */
    refusal?: string;
    /**
     * Sends the draft, and answers with the client's `{ error }`.
     *
     * The data-loss confirmation calls it with the refusal flags: the host
     * hands them to the request and leaves the refusal to the window, which
     * prints it in itself. The action bar calls it without them, and the host
     * answers there as it always has: in `refusal`, or on a plate.
     */
    save?: (
      draft: AttributeSchemaDraft,
      options?: RefusalFlags,
    ) => Promise<DialogActionResult>;
  }>(),
  {
    sources: undefined,
    sourcesRefusal: "",
    showTitle: false,
    hasCharacters: false,
    showActions: true,
    saving: false,
    refusal: "",
    save: undefined,
  },
);

const emit = defineEmits<{
  "update:modelValue": [AttributeSchemaDraft];
  cancel: [];
}>();

// --- Draft state, synced with the v-model ---------------------------------

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

// --- The "Схема атрибутов" list and "Скопировать" --------------------------

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

// The first entry is the default template, and it arrives picked (doc
// 4.2.2.10). Re-picked whenever the list is replaced, because the value held
// while the list was empty names nothing in the new one.
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
  // A copy replaces the whole set, so a table with anything in it is asked
  // about first.
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
  // The read failed and said so; the draft the reader has is left alone.
  if (!specifications) return;

  draft.value.specifications = cloneSpecsWithNewIds(specifications);
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
  // Only a template carries a name, and only there is it required.
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

/** The draft as it leaves the editor: rows numbered in table order, copied. */
function outgoing(): AttributeSchemaDraft {
  reindex();
  return cloneSchema(draft.value);
}

/**
 * The data-loss confirmation's action. The window runs it and closes on
 * success alone, so a refused save leaves the question standing with the
 * refusal in it and the draft as typed under it.
 */
function saveConfirmed() {
  return props.save?.(outgoing(), { ownsRefusal: true, ownsRateLimit: true });
}

defineExpose({ validate, requestSave });
</script>

<template>
  <div class="schema-editor">
    <!-- The source list and its copy button -->
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
      <!-- Why the list is short, or why the copy brought nothing, under the
           control it is about. A plate in the corner used to say this. -->
      <p v-if="sourcesRefusal" class="form-error" role="alert">
        {{ sourcesRefusal }}
      </p>
    </form-field>

    <!-- Schema meta -->
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
        maxlength="100"
        @input="titleError = ''"
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
              <RowIconButton
                icon="pencil"
                label="Редактировать атрибут"
                @click="editAttribute(spec.id)"
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

    <!-- Replacing the whole set from another sheet -->
    <ConfirmDialog
      v-model:show="showCopyConfirm"
      title="Скопировать схему атрибутов?"
      message="Параметры выбранной схемы целиком заменят текущие"
      confirm-label="Скопировать"
      @confirm="copyFromSource"
    />

    <!-- Data-loss confirmation -->
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
@use "@/assets/styles/Inputs" as *

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

// --- Table ---
// Every column stands at every width: the cells are 4 apart at their sides,
// so the least the six columns take (the longest words of the headers and of
// the type, a bold name, the two signs) is under 580, and the narrowest box
// the table stands in at 1366, and on a phone, which lays out the same boxes,
// is 600, the form of a new game. With cells 8 apart it would be 626, wider
// than that box. The box asks the column for no width of its own
// (inline-size containment, as DataTable's wrapper does). Where the box is
// narrower than that least width, as the middle of three columns is in a
// window of 1024 (300), the table scrolls sideways in it as a DataTable does,
// and the page stays put.
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

// No width here: the checkbox is the site's own drawing and its side belongs
// to +checkbox. A width: auto on a box with appearance: none is its two
// border lines and nothing between them.
input[type="checkbox"],
input[type="radio"]
  margin: 0
  cursor: pointer

.empty-cell
  padding: $big
  color: $text-muted

// The shared secondary button, at the left edge like every other control of
// the page; the one primary of the editor is the save below.
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

// Under the source list the line has no column gap to stand it off the
// control, so it carries the step itself.
.copy-row + .form-error
  margin-top: $small

// --- Modal ---
</style>

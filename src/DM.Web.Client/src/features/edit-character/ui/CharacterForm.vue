<script setup lang="ts">
/**
 * CharacterForm — schema-driven character create / edit / view form.
 *
 * Two surfaces share the same field order:
 *  - edit (default): interactive controls (CharacterSheetFields) + a submit
 *    button; on submit it maps values to the character attribute input and
 *    calls createCharacter / updateCharacter.
 *  - view (`mode="view"`): read-only rendering of an existing character's
 *    attribute values in the same field order as edit.
 *
 * The per-specification controls live in entities/game CharacterSheetFields,
 * which is the single source of truth shared with the schema-editor preview.
 *
 * BbCode attribute values are rendered in view mode via the server-rendered
 * HTML (ContentText), never {{ }} or v-html on the raw string
 * (docs/architecture/BBCODE_RENDERING.md).
 */
import { computed, reactive, ref } from "vue";
import {
  gameApi,
  AttributeSpecificationType,
  CharacterSheetFields,
  type AttributeSchema,
  type AttributeSpecification,
  type Character,
  type CharacterInput,
  type CharacterPrivacySettings,
} from "@/entities/game";
import { Form, FormField } from "@/shared/ui/Form";
import { ContentText } from "@/shared/ui/Content";
import { SvgIcon } from "@/shared/ui/Icon";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { describeFailure } from "@/shared/lib/errors";
import { CHARACTER_NAME_MAX_LENGTH } from "@/shared/lib/constants/game";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";

const props = withDefaults(
  defineProps<{
    /** Schema whose specifications drive the rendered controls */
    schema: AttributeSchema;
    /** "edit" (default) renders inputs; "view" renders read-only values */
    mode?: "edit" | "view";
    /** Game id — required to create a new character */
    gameId?: string;
    /** Character id — when set, submit updates instead of creating */
    characterId?: string;
    /** Existing character; supplies view-mode values and edit initial state */
    character?: Character;
    /** Initial raw values keyed by specification id (for editing existing) */
    initialValues?: Record<string, string>;
    /** Initial character name (edit) */
    initialName?: string;
    /** Render the name field (edit/preview only). Default true. */
    showName?: boolean;
    /**
     * Create the character as an NPC (master-controlled). Only consulted when
     * no existing character is supplied — an existing character keeps its own
     * privacy.isNpc.
     */
    isNpc?: boolean;
  }>(),
  {
    mode: "edit",
    gameId: undefined,
    characterId: undefined,
    character: undefined,
    initialValues: undefined,
    initialName: undefined,
    showName: true,
    isNpc: false,
  },
);

const emit = defineEmits<{
  (e: "saved", character: Character): void;
  (e: "cancel"): void;
}>();

const Type = AttributeSpecificationType;

// Specifications rendered in schema order (defensive copy — never mutate props).
const specs = computed<AttributeSpecification[]>(() =>
  [...props.schema.specifications].sort((a, b) => a.order - b.order),
);

// --- Form state -------------------------------------------------------------

const name = ref(props.initialName ?? props.character?.name ?? "");

// Raw value per specification id.
const values = reactive<Record<string, string>>({});
for (const spec of props.schema.specifications) {
  const fromInitial = props.initialValues?.[spec.id];
  // BbCode display values are server-rendered HTML, not raw BBCode, so they
  // are never prefilled from character.attributes — only from initialValues.
  const fromCharacter =
    spec.type === Type.BbCode
      ? undefined
      : (props.character?.attributes?.find((a) => a.id === spec.id)?.value ??
        undefined);
  values[spec.id] = fromInitial ?? fromCharacter ?? "";
}

const privacy = computed<CharacterPrivacySettings>(
  () =>
    props.character?.privacy ?? {
      isNpc: props.isNpc,
      editByMaster: false,
      editPostByMaster: false,
    },
);

// The sheet as it was opened. Edit mode seeds itself from an existing
// character, so "there is something to lose" is a comparison, not an
// emptiness check.
const pristineName = name.value;
const pristineValues: Record<string, string> = { ...values };

const submitted = ref(false);
const saved = ref(false);
const saving = ref(false);
const errorMessage = ref<string | null>(null);

const dirty = computed(
  () =>
    !saved.value &&
    (name.value !== pristineName ||
      specs.value.some(
        (spec) => (values[spec.id] ?? "") !== (pristineValues[spec.id] ?? ""),
      )),
);

// --- Validation -------------------------------------------------------------

const nameMissing = computed(() => name.value.trim().length === 0);

function specMissing(spec: AttributeSpecification): boolean {
  return spec.required && (values[spec.id]?.trim() ?? "").length === 0;
}

const isValid = computed(
  () =>
    (!props.showName || !nameMissing.value) &&
    specs.value.every((s) => !specMissing(s)),
);

function nameErrors(): string[] {
  return submitted.value && nameMissing.value ? ["Empty"] : [];
}

function specErrors(spec: AttributeSpecification): string[] {
  return submitted.value && specMissing(spec) ? ["Empty"] : [];
}

// --- View-mode value lookup -------------------------------------------------

function viewAttribute(spec: AttributeSpecification) {
  // List-shape characters carry no attributes — guard the lookup.
  return props.character?.attributes?.find((a) => a.id === spec.id);
}

function viewPlainValue(spec: AttributeSpecification): string {
  const attr = viewAttribute(spec);
  if (!attr) return "";
  let text = attr.value ?? "";
  // List specs may carry a modifier alongside the chosen value.
  if (spec.type === Type.TextNumberList && attr.modifier) {
    text = `${text} (${attr.modifier})`;
  }
  return text;
}

// --- Submit -----------------------------------------------------------------

async function submit() {
  submitted.value = true;
  if (!isValid.value || saving.value) return;

  const payload: CharacterInput = {
    name: name.value.trim(),
    privacy: privacy.value,
    // Map each entered value to the character attribute input ({ id, value }).
    // Empty optional values are omitted; the server clears absent attributes.
    attributes: specs.value
      .map((s) => ({ id: s.id, value: (values[s.id] ?? "").trim() }))
      .filter((a) => a.value.length > 0),
  };

  saving.value = true;
  errorMessage.value = null;

  const { data, error } = props.characterId
    ? await gameApi.updateCharacter(props.characterId, payload)
    : await gameApi.createCharacter(props.gameId ?? "", payload);

  saving.value = false;

  if (error) {
    errorMessage.value = describeFailure(
      error,
      "Не удалось сохранить персонажа",
    );
    return;
  }
  if (data) {
    saved.value = true;
    emit("saved", data);
  }
}

const actionLabel = computed(() =>
  props.characterId ? "Сохранить" : "Создать персонажа",
);
</script>

<template>
  <!-- View mode: read-only values in schema order -->
  <div v-if="mode === 'view'" class="character-view">
    <div v-for="spec in specs" :key="spec.id" class="view-row">
      <div class="view-label">
        <span class="view-title">{{ spec.title }}</span>
        <span
          v-if="spec.isHidden"
          class="privacy-indicator"
          title="Приватность"
        >
          <SvgIcon name="locked" class="privacy-icon" />
          Приватность
        </span>
      </div>
      <ContentText
        v-if="spec.type === Type.BbCode && viewAttribute(spec)?.valueBbText"
        :html="viewAttribute(spec)!.valueBbText!"
        class="view-value view-value__bb"
      />
      <span v-else-if="viewPlainValue(spec)" class="view-value">{{
        viewPlainValue(spec)
      }}</span>
      <span v-else class="view-value view-value__empty">{{
        VALUE_UNAVAILABLE
      }}</span>
    </div>
  </div>

  <!-- Edit mode: schema-driven controls -->
  <Form
    v-else
    :valid="isValid"
    :loading="saving"
    :action="actionLabel"
    cancel="Отмена"
    @submit="submit"
    @cancel="emit('cancel')"
  >
    <FormField
      v-if="showName"
      label="Имя персонажа"
      name="character-name"
      :errors="nameErrors()"
    >
      <input
        id="character-name"
        v-model="name"
        type="text"
        :maxlength="CHARACTER_NAME_MAX_LENGTH"
        placeholder="Имя персонажа"
      />
    </FormField>

    <CharacterSheetFields
      :specs="specs"
      :values="values"
      :spec-errors="specErrors"
      @update="(id, val) => (values[id] = val)"
    />

    <p v-if="errorMessage" class="form-error" role="alert">
      {{ errorMessage }}
    </p>

    <UnsavedChangesGuard :dirty="dirty" />
  </Form>
</template>

<style scoped lang="sass">
.privacy-indicator
  display: inline-flex
  align-items: center
  gap: $tiny
  flex-shrink: 0
  color: $text-muted
  font-size: $secondary-font-size

.privacy-icon
  width: 12px
  height: 12px

.form-error
  margin-top: $small
  color: $accent-red
  font-size: $secondary-font-size

// --- View mode ---
.character-view
  display: flex
  flex-direction: column
  gap: $medium

.view-row
  display: flex
  flex-direction: column
  gap: $minor

.view-label
  display: flex
  align-items: center
  justify-content: space-between
  gap: $small
  color: $text-muted
  font-size: $secondary-font-size

.view-title
  font-weight: 600

.view-value
  color: $text

.view-value__empty
  color: $text-muted
</style>

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
  type AttributeSpecification,
  type Character,
  type CharacterInput,
  type CharacterPrivacySettings,
} from "@/entities/game";
import { unwrapResource } from "@/shared/api";
import { Form, FormField } from "@/shared/ui/Form";
import { ContentText } from "@/shared/ui/Content";
import { SvgIcon } from "@/shared/ui/Icon";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";
import { CHARACTER_NAME_MAX_LENGTH } from "@/shared/lib/constants/game";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";

const props = withDefaults(
  defineProps<{
    /** Schema whose specifications drive the rendered controls */
    schema: { specifications: AttributeSpecification[] };
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

const submitted = ref(false);
const saving = ref(false);
const errorMessage = ref<string | null>(null);

/** Everything a save would send, as the comparison below sees it. */
const asSent = () =>
  JSON.stringify({
    name: name.value,
    values: Object.fromEntries(
      specs.value.map((spec) => [spec.id, values[spec.id] ?? ""]),
    ),
  });

/**
 * The state the server has. It starts as what was loaded and moves forward on
 * every save, which is what makes both answers below true at once: the guard
 * knows there is unsaved work, and the button knows the sheet is saved.
 *
 * It used to be a latch - one boolean set on the first save and never cleared.
 * That made the guard stop warning for the rest of the visit: a sheet edited
 * again after a save could be navigated away from in silence, which is the
 * very loss the guard was added for.
 */
const stored = ref(asSent());
const everSaved = ref(false);

const dirty = computed(() => stored.value !== asSent());

/** The sheet on screen is the sheet the server has, and it got there by a save. */
const saved = computed(() => everSaved.value && !dirty.value);

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

/**
 * The save takes its own refusal over, because this form already answers one:
 * the strip under the fields holds the sentence the server sent, and the
 * interceptor's plate over it was the same refusal said a second time. What the
 * server names here is worth reading where the sheet is — the master forbids
 * edits, the character is not this player's, the name is taken — and the strip
 * stays until the reader corrects something, while the plate fades.
 *
 * An expired session and a dead connection are the interceptor's alone, and
 * the strip stays empty beside them. Neither flag reaches those two - the
 * plate is raised for them whatever the request says - and the strip used to
 * add its own sentence under the plate: "Сессия истекла, войдите снова" in the
 * corner and "Не удалось сохранить персонажа" under the fields, one event told
 * twice in two wordings. That the save did not happen is already on screen:
 * the button still reads "Сохранить" and the sheet is still marked unsaved.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

/**
 * The statuses the interceptor announces no matter what the request claimed
 * (client.ts: 401 and a response that never came are checked before the
 * flags). A strip under the fields for these is the second sentence above.
 */
const SAID_BY_INTERCEPTOR = [0, 401];

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
    ? await gameApi.updateCharacter(props.characterId, payload, OWNS_REFUSAL)
    : await gameApi.createCharacter(props.gameId ?? "", payload, OWNS_REFUSAL);

  saving.value = false;

  if (error) {
    if (SAID_BY_INTERCEPTOR.includes(error.status)) return;

    // A wait is said in the shared wording, the one the interceptor would have
    // raised: it is the only one that names the seconds the server asked for.
    errorMessage.value =
      error.status === 429
        ? describeRateLimit(error.retryAfterSeconds)
        : describeFailure(error, "Не удалось сохранить персонажа");
    return;
  }
  // The answer is a single-resource envelope, and the listener is handed the
  // character rather than the wrapper it arrived in.
  const character = unwrapResource<Character>(data);
  if (character) {
    stored.value = asSent();
    everSaved.value = true;
    emit("saved", character);
  }
}

/**
 * The button answers for the save, because nothing else on this screen can:
 * the sheet after a save holds the same values it held before, the form stays
 * open, and the page does not move. It said "Сохранено" in a toast in the
 * corner until 20 September 2026; see UI_STANDARDS, "Всплывающие сообщения".
 *
 * Only in edit mode. Creating a character leaves this form for the sheet that
 * was created, and that page is the answer.
 */
const actionLabel = computed(() => {
  if (!props.characterId) return "Создать персонажа";
  return saved.value ? "Сохранено" : "Сохранить";
});
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
    :valid="isValid && !saved"
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

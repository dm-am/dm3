<script setup lang="ts">
// BbCode values are shown from the server-rendered HTML, never the raw string.
import { computed, reactive, ref } from "vue";
import {
  characterApi,
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
import { describeFailureOrWait } from "@/shared/lib/errors";
import { CHARACTER_NAME_MAX_LENGTH } from "@/shared/lib/constants/game";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = withDefaults(
  defineProps<{
    schema: { specifications: AttributeSpecification[] };
    mode?: "edit" | "view";
    gameId?: string;
    /** When set, submit updates instead of creating. */
    characterId?: string;
    character?: Character;
    initialValues?: Record<string, string>;
    initialName?: string;
    /** Only for a new character: an existing one keeps its own privacy.isNpc. */
    isNpc?: boolean;
  }>(),
  {
    mode: "edit",
    isNpc: false,
  },
);

const emit = defineEmits<{
  (e: "saved", character: Character): void;
  (e: "cancel"): void;
}>();

const specs = computed<AttributeSpecification[]>(() =>
  [...props.schema.specifications].sort((a, b) => a.order - b.order),
);

const name = ref(props.initialName ?? props.character?.name ?? "");

const values = reactive<Record<string, string>>({});
for (const spec of props.schema.specifications) {
  const fromInitial = props.initialValues?.[spec.id];
  // BbCode display values are rendered HTML, so they prefill only from initialValues.
  const fromCharacter =
    spec.type === AttributeSpecificationType.BbCode
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

const asSent = () =>
  JSON.stringify({
    name: name.value,
    values: Object.fromEntries(
      specs.value.map((spec) => [spec.id, values[spec.id] ?? ""]),
    ),
  });

// The state the server has, moving forward on every save: the guard and the button both read it.
const stored = ref(asSent());
const everSaved = ref(false);

const dirty = computed(() => stored.value !== asSent());

const saved = computed(() => everSaved.value && !dirty.value);

const nameMissing = computed(() => name.value.trim().length === 0);

function specMissing(spec: AttributeSpecification): boolean {
  return spec.required && (values[spec.id]?.trim() ?? "").length === 0;
}

const isValid = computed(
  () => !nameMissing.value && specs.value.every((s) => !specMissing(s)),
);

function nameErrors(): string[] {
  return submitted.value && nameMissing.value ? ["Empty"] : [];
}

function specErrors(spec: AttributeSpecification): string[] {
  return submitted.value && specMissing(spec) ? ["Empty"] : [];
}

function viewAttribute(spec: AttributeSpecification) {
  return props.character?.attributes?.find((a) => a.id === spec.id);
}

function viewPlainValue(spec: AttributeSpecification): string {
  const attr = viewAttribute(spec);
  if (!attr) return "";
  let text = attr.value ?? "";
  if (
    spec.type === AttributeSpecificationType.TextNumberList &&
    attr.modifier
  ) {
    text = `${text} (${attr.modifier})`;
  }
  return text;
}

// Announced by the interceptor whatever the request claimed (interceptor.ts).
const SAID_BY_INTERCEPTOR = [0, 401];

async function submit() {
  submitted.value = true;
  if (!isValid.value || saving.value) return;

  const payload: CharacterInput = {
    name: name.value,
    privacy: privacy.value,
    // Empty optional values are omitted; the server clears absent attributes.
    attributes: specs.value
      .map((s) => ({ id: s.id, value: (values[s.id] ?? "").trim() }))
      .filter((a) => a.value.length > 0),
  };

  saving.value = true;
  errorMessage.value = null;

  const { data, error } = props.characterId
    ? await characterApi.updateCharacter(
        props.characterId,
        payload,
        OWNS_REFUSAL,
      )
    : await characterApi.createCharacter(
        props.gameId ?? "",
        payload,
        OWNS_REFUSAL,
      );

  saving.value = false;

  if (error) {
    if (SAID_BY_INTERCEPTOR.includes(error.status)) return;

    // The shared wording of a wait: the only one that names the seconds.
    errorMessage.value = describeFailureOrWait(
      error,
      "Не удалось сохранить персонажа",
    );
    return;
  }
  const character = unwrapResource<Character>(data);
  if (character) {
    stored.value = asSent();
    everSaved.value = true;
    emit("saved", character);
  }
}

// Nothing else on this screen can report the save: the button says it, in edit mode only.
const actionLabel = computed(() => {
  if (!props.characterId) return "Создать персонажа";
  return saved.value ? "Сохранено" : "Сохранить";
});
</script>

<template>
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
        v-if="
          spec.type === AttributeSpecificationType.BbCode &&
          viewAttribute(spec)?.valueBbText
        "
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
  width: $tertiary-font-size
  height: $tertiary-font-size

.form-error
  margin-top: $small
  color: $accent-red
  font-size: $secondary-font-size

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
  font-weight: 700

.view-value
  color: $text

.view-value__empty
  color: $text-muted
</style>

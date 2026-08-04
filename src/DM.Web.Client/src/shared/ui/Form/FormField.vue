<template>
  <div
    :class="[
      'form-field',
      label || $slots.label ? 'form-field__labeled' : null,
      displayErrors.length ? 'error' : null,
    ]"
  >
    <div v-if="label || $slots.label" ref="labelBox" class="form-field-label">
      <slot name="label">
        <label :id="labelId" :for="controlId">{{ label }}</label>
      </slot>
      <span v-if="optional" class="form-field-optional">необязательно</span>
    </div>
    <div
      ref="row"
      class="form-field-row"
      :role="groupLabelledBy ? 'group' : undefined"
      :aria-labelledby="groupLabelledBy"
      :aria-describedby="groupLabelledBy ? describedBy : undefined"
    >
      <slot />
    </div>
    <span
      v-for="(error, index) in displayErrors"
      :key="error"
      class="form-field-error"
      role="alert"
      :id="errorId(index)"
    >
      {{ translateError(error) }}
    </span>
    <div v-if="$slots.hint" :id="hintId" class="form-field-hint">
      <slot name="hint" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, onUpdated, ref, useSlots } from "vue";
import { VALIDATION_MESSAGES } from "@/shared/lib/errors/validationErrors";

const props = defineProps<{
  label?: string;
  name?: string;
  errors?: string[];
  optional?: boolean;
}>();

const slots = useSlots();

// `name` reads the ids in the inspector, the suffix keeps them apart: the same
// field can be on screen twice — a dialog over the page it covers — and `name`
// alone would put one id on both. Same shape as Tooltip and Tabs generate.
const uid = `${props.name || "form-field"}-${Math.random().toString(36).slice(2, 9)}`;
const labelId = `${uid}-label`;
const hintId = `${uid}-hint`;
const errorId = (index: number) => `${uid}-error-${index}`;

// Filter out empty errors (from .required("") validation)
const displayErrors = computed(
  () => props.errors?.filter((e) => e?.trim()) || [],
);

/** Everything that describes the field, in reading order: errors, then hint. */
const describedBy = computed(() => {
  const ids = displayErrors.value.map((_, index) => errorId(index));
  if (slots.hint) ids.push(hintId);
  return ids.length ? ids.join(" ") : undefined;
});

const labelBox = ref<HTMLElement | null>(null);
const row = ref<HTMLElement | null>(null);
/** Id of the control the label points at; unset while the row holds no single one. */
const controlId = ref<string>();
/** Id of the label, set when it names the row as a whole instead of one control. */
const groupLabelledBy = ref<string>();

/** What a `<label for>` may point at, among the controls these forms use. */
const LABELABLE = "input:not([type='hidden']), textarea, select";

/** v-show hides by inline style, and a hidden control is not the field's control. */
const isHidden = (el: HTMLElement, stop: HTMLElement): boolean => {
  for (let node: HTMLElement | null = el; node; node = node.parentElement) {
    if (node.hidden || node.style.display === "none") return true;
    if (node === stop) break;
  }
  return false;
};

/**
 * The single control the label belongs to, or null when the row holds none or
 * several. An option already inside its own `<label>` — the radio and checkbox
 * rows — is not a candidate: the field label names the set, and a `for` at the
 * first option would make a click on the label pick that option.
 */
const singleControl = (rowEl: HTMLElement): HTMLElement | null => {
  const controls = Array.from(
    rowEl.querySelectorAll<HTMLElement>(LABELABLE),
  ).filter((el) => !el.closest("label") && !isHidden(el, rowEl));
  return controls.length === 1 ? controls[0] : null;
};

const setAria = (el: HTMLElement, attribute: string, value?: string) =>
  value === undefined
    ? el.removeAttribute(attribute)
    : el.setAttribute(attribute, value);

/** The control the wiring below was last put on, so a swap can take it back. */
let bound: HTMLElement | null = null;

/**
 * Ties the label, the errors and the hint to the control. The control is the
 * caller's — it arrives through the slot — so this is the one binding that
 * cannot be written in the template: only the rendered row says which element
 * the caller put in it. An id or a `for` the caller wrote is left alone.
 */
const bindControl = () => {
  const rowEl = row.value;
  if (!rowEl) return;

  const control = singleControl(rowEl);
  if (control && !control.id) control.id = `${uid}-control`;
  controlId.value = control?.id;

  // A label from the slot is the caller's markup, out of this template's reach.
  const slotLabel = slots.label
    ? (labelBox.value?.querySelector("label") ?? null)
    : null;
  if (slotLabel) {
    if (!slotLabel.id) slotLabel.id = labelId;
    if (control && !slotLabel.hasAttribute("for"))
      slotLabel.setAttribute("for", control.id);
  }

  // Nothing single to point at: the label names the row, which is a group.
  const named = slotLabel ? slotLabel.id : props.label ? labelId : undefined;
  groupLabelledBy.value = control ? undefined : named;

  if (bound && bound !== control) {
    setAria(bound, "aria-describedby");
    setAria(bound, "aria-invalid");
  }
  bound = control;
  if (control) {
    setAria(control, "aria-describedby", describedBy.value);
    setAria(
      control,
      "aria-invalid",
      displayErrors.value.length ? "true" : undefined,
    );
  }
};

onMounted(bindControl);
onUpdated(bindControl);

// The vocabulary is the server's, so it is shared rather than declared here.
// This copy held six of the thirteen codes, which is why a password failing the
// digit rule showed the reader "RequiresDigit".
const injectedTranslations = inject<Record<string, string>>(
  "formFieldTranslations",
  {},
);
const errorMessages = { ...VALIDATION_MESSAGES, ...injectedTranslations };

const translateError = (error: string): string => {
  return errorMessages[error] || error;
};
</script>

<style scoped lang="sass">
.form-field__labeled
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

// A field that failed validation looks different from one that did not. The
// rule sits on `.form-field` and not on the labelled variant: a field rendered
// without a label could not show the state at all, and the state does not
// depend on whether the field has a label. All three control elements are
// named, because a textarea and a select fail validation the same way an input
// does and used to be left out even of the intent.
//
// No shake: the rule referenced @keyframes shake-error, which is defined
// nowhere in the project and never was, so the two references to it are gone
// rather than given a body.
//
// Reached through :deep(), because the control is not this component's markup:
// it arrives through the slot, carrying the scope id of the page that wrote it.
// Without this the selector matched nothing at all, on every form of the site,
// for as long as it has existed — the state was declared and never drawn.
//
// Colour is not the only carrier: .form-field-error prints the reason under the
// field, so a reader who cannot tell the border apart still gets the sentence.
.form-field.error
  :deep(input),
  :deep(textarea),
  :deep(select)
    border-color: $border-accent-red

    &:focus
      box-shadow: inset 0 0 $minor $border-accent-red

.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  color: $text
  font-size: $secondary-font-size

  label
    color: inherit

  & input, & textarea, & select
    box-sizing: border-box

.form-field-optional
  flex-shrink: 0
  color: $text-muted

.form-field-row
  :deep(input), :deep(textarea), :deep(select)
    width: 100%
    box-sizing: border-box

.form-field-error
  display: block
  margin-top: $minor
  color: $accent-red
  font-size: $secondary-font-size

.form-field-hint
  margin-top: $minor
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4
</style>

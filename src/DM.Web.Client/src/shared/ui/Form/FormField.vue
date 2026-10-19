<script setup lang="ts">
import {
  computed,
  onBeforeUnmount,
  onMounted,
  onUpdated,
  ref,
  useId,
  useSlots,
} from "vue";
import { VALIDATION_MESSAGES } from "@/shared/lib/errors/validationErrors";
import { useDialogForm, useInDialog } from "@/shared/ui/Layout/dialogContext";

const props = defineProps<{
  label?: string;
  name?: string;
  errors?: string[];
  optional?: boolean;
  /** The server's name for the field: its word about it stands under the field, not in the window's line. */
  field?: string;
}>();

const slots = useSlots();

/** A field in a window takes the window's recipe (see the styles). */
const inDialog = useInDialog();

// `name` reads in the inspector; the suffix keeps two copies apart (a dialog over its page).
const uid = `${props.name || "form-field"}-${useId()}`;
const labelId = `${uid}-label`;
const hintId = `${uid}-hint`;
const errorId = (index: number) => `${uid}-error-${index}`;

// Claims the field, so the window keeps the server's word about it out of its refusal line.
const dialog = useDialogForm();
let release: (() => void) | undefined;
onMounted(() => {
  if (props.field && dialog) release = dialog.claim(props.field);
});
onBeforeUnmount(() => release?.());

// Empty errors dropped (from .required("")), then the server's word unless the caller says it already.
const displayErrors = computed(() => {
  const own = props.errors?.filter((e) => e?.trim()) || [];
  const server = props.field ? dialog?.fieldError(props.field) : undefined;
  return server && !own.includes(server) ? [...own, server] : own;
});

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

/** The single control, or null. An option inside its own <label> is none: the field label names the set. */
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

/** The control arrives through the slot, so only the rendered row says which one to wire. */
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

// The vocabulary is the server's, so it is shared.
const translateError = (error: string): string =>
  VALIDATION_MESSAGES[error] || error;
</script>

<template>
  <div
    class="form-field"
    :class="{
      'form-field--dialog': inDialog,
      'form-field__labeled': label || $slots.label,
      error: displayErrors.length,
    }"
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

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/DialogSurface" as *

.form-field__labeled
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

// `.error` names the state for the markup alone: a refused field says so in its red line, the frame stays neutral.

// Set on the row, not the <label>, so a caption put into the label slot takes it too.
.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  +field-caption

  label
    color: inherit

.form-field-optional
  flex-shrink: 0
  color: $text-muted
  font-weight: 400

.form-field-row
  // Not a checkbox or a radio: the scoped selector outranks the site-wide checkbox rule and stretched the box.
  // :where() adds no specificity.
  :deep(input:where(:not([type="checkbox"]):not([type="radio"]))),
  :deep(textarea),
  :deep(select)
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

// In a window: the guest recipe (AuthSurface.sass) with the numbers of _DialogSurface.sass.
.form-field--dialog,
.form-field--dialog.form-field__labeled
  gap: 0
  margin: 0 0 $dialog-field-step

// "необязательно" keeps the secondary size and shares the caption's baseline.
.form-field--dialog .form-field-label
  align-items: baseline
  margin-bottom: $small
  font-size: $dialog-field-font-size
  font-weight: 700

.form-field--dialog .form-field-optional
  font-size: $secondary-font-size
  font-weight: 400

// A child combinator: the BBCode editor carries a textarea of its own deep inside.
.form-field--dialog .form-field-row
  > :deep(input:where(:not([type="checkbox"]):not([type="radio"])))
    height: $dialog-control-height
    padding: 0 $dialog-field-inset
    font-size: $dialog-field-font-size

  :deep(select)
    height: $dialog-control-height
    padding-top: 0
    padding-bottom: 0
    padding-left: $dialog-field-inset
    font-size: $dialog-field-font-size

  // A block: an inline textarea leaves the descent of the line under it.
  > :deep(textarea)
    display: block
    padding: $dialog-textarea-padding $dialog-field-inset
    font-size: $dialog-field-font-size

.form-field--dialog .form-field-error,
.form-field--dialog .form-field-hint
  margin-top: $dialog-message-gap
  line-height: 1.4
</style>

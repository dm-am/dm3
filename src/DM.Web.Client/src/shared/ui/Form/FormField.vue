<template>
  <div
    :class="[
      'form-field',
      inDialog ? 'form-field--dialog' : null,
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
import {
  computed,
  inject,
  onBeforeUnmount,
  onMounted,
  onUpdated,
  ref,
  useSlots,
} from "vue";
import { VALIDATION_MESSAGES } from "@/shared/lib/errors/validationErrors";
import { useDialogForm, useInDialog } from "@/shared/ui/Layout/dialogContext";

const props = defineProps<{
  label?: string;
  name?: string;
  errors?: string[];
  optional?: boolean;
  /**
   * The name the server gives this field in a validation answer ("reason").
   * In a window that runs an action, what the server says about it stands
   * under this field and not in the window's refusal line.
   */
  field?: string;
}>();

const slots = useSlots();

/** A field in a window takes the window's recipe (see the styles). */
const inDialog = useInDialog();

// `name` reads the ids in the inspector, the suffix keeps them apart: the same
// field can be on screen twice — a dialog over the page it covers — and `name`
// alone would put one id on both. Same shape as Tooltip and Tabs generate.
const uid = `${props.name || "form-field"}-${Math.random().toString(36).slice(2, 9)}`;
const labelId = `${uid}-label`;
const hintId = `${uid}-hint`;
const errorId = (index: number) => `${uid}-error-${index}`;

// The window's side of the field: it takes the server's word about the field
// only when told which field it is, and says so to the window, which then
// keeps that word out of its refusal line.
const dialog = useDialogForm();
let release: (() => void) | undefined;
onMounted(() => {
  if (props.field && dialog) release = dialog.claim(props.field);
});
onBeforeUnmount(() => release?.());

// Filter out empty errors (from .required("") validation), then the server's
// word about the field, when the window has one and the caller does not say
// the same thing already.
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
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/DialogSurface" as *

.form-field__labeled
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

// A field that failed validation says so in words and in nothing else: the
// red line of .form-field-error under it. The frame stays neutral in every
// state, on a page as in a window and on the guest surface (owner's standard,
// 27 September 2026). A frame that also turned red said the same thing twice,
// in a colour a third of readers cannot tell from the resting one, and it was
// drawn on a page and not in a window, so the same field refused looked two
// ways depending on where it stood. `.error` stays on the root as the state's
// name for the markup.

// The caption over a field of a page, +field-caption (Inputs.sass): 14 and
// bold, one look for every form of a page (owner's standard, 28 September
// 2026). A window sets its own 15 below, bold as well. It is set on the row
// and not on the <label>, so a caption the caller put into the label slot
// takes it too.
.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  +field-caption

  label
    color: inherit

  & input, & textarea, & select
    box-sizing: border-box

// "необязательно" beside a caption is a note and not the caption: the regular
// weight, on a page and in a window.
.form-field-optional
  flex-shrink: 0
  color: $text-muted
  font-weight: 400

.form-field-row
  // A field the reader types into takes the width of the row. A checkbox and a
  // radio do not: they are boxes of a fixed side, drawn by +checkbox() over
  // $checkbox-size. This selector carries the scope attribute, which outranks
  // the site-wide `input[type="checkbox"]` rule, so without the guard the box
  // won and stretched across the whole row, pushing the text of its own label
  // out of sight at every call site that puts a checkbox in a field. :where()
  // has no specificity of its own, so the guard costs the rule nothing; the
  // reset spares the same two types from the text-field focus ring in the same
  // shape.
  //
  // box-sizing rides along inside the same guard instead of getting a rule of
  // its own: for a checkbox and a radio the browser already sizes the box
  // border-box, so leaving those two out of it changes nothing they draw.
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

// ─────────────────────────────────────────────────────────── in a window ──
//
// A field in a window follows the guest recipe (AuthSurface.sass), with the
// numbers of _DialogSurface.sass: the caption bold over the field and 8px
// above it, the field at the window's control height, one field step to the
// next. A refusal or a hint stands 6px under the field and holds no place in
// advance, so a line that arrives moves what is under it. On a page none of
// this applies: the class comes from the window (IN_DIALOG), never from a
// caller.

.form-field--dialog,
.form-field--dialog.form-field__labeled
  gap: 0
  margin: 0 0 $dialog-field-step

// The caption is set on the row and not on the <label>, so a caption the
// caller put into the label slot takes it too. "необязательно" beside it
// keeps the secondary size and the regular weight, and the two share a
// baseline.
.form-field--dialog .form-field-label
  align-items: baseline
  margin-bottom: $small
  font-size: $dialog-field-font-size
  font-weight: 700

.form-field--dialog .form-field-optional
  font-size: $secondary-font-size
  font-weight: 400

// The controls the row holds as its own children, and the list inside Select.
// A child combinator for the field and the text area: the BBCode editor
// carries a textarea of its own deep inside, and the editor stays as it is.
// The list keeps the right padding Select gives it, which is the chevron's.
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

  // A block and not the inline box a textarea is by default: an inline one
  // leaves the descent of the line under it, and the line under the field
  // stood 11px below it instead of 6.
  > :deep(textarea)
    display: block
    padding: $dialog-textarea-padding $dialog-field-inset
    font-size: $dialog-field-font-size

.form-field--dialog .form-field-error,
.form-field--dialog .form-field-hint
  margin-top: $dialog-message-gap
  line-height: 1.4
</style>

/**
 * The window as its surface specs read it: the files that draw it, stand-in
 * scope ids, and a field built as FormField renders it (cascade.ts). Spec-only.
 */
import { join } from "node:path";
import { CLIENT_SRC } from "./sourceTree";

export const at = (path: string) => join(CLIENT_SRC, path);

export const DIALOG = at("shared/ui/Layout/Dialog.vue");
export const TITLE = at("shared/ui/Layout/DialogTitle.vue");
export const FORM = at("shared/ui/Form/Form.vue");
export const FIELD = at("shared/ui/Form/FormField.vue");
export const CONFIRM = at("shared/ui/ConfirmDialog/ConfirmDialog.vue");
export const BUTTON = at("shared/ui/Button/Button.vue");
export const AUTH = at("assets/styles/AuthSurface.sass");
export const RESET = at("assets/styles/Reset.sass");
export const BOLD = at("assets/fonts/PTSans-Bold.ttf");

/** Stand-ins for the build's scope ids; compileStyle writes them verbatim. */
export const SCOPE = {
  field: "data-v-field",
  form: "data-v-form",
  button: "data-v-button",
  confirm: "data-v-confirm",
};

/**
 * A field as FormField renders it: its own boxes carry its scope id, the
 * control in the row is the caller's and carries none.
 */
export const renderField = (options: {
  inDialog: boolean;
  control: string;
  error?: boolean;
  hint?: boolean;
}): HTMLElement => {
  const root = document.createElement("div");
  root.className = [
    "form-field",
    options.inDialog ? "form-field--dialog" : "",
    "form-field__labeled",
    options.error ? "error" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const own = (html: string) =>
    html.replace(/<(\w+) class=/g, `<$1 ${SCOPE.field} class=`);
  root.innerHTML = own(
    `<div class="form-field-label"><label>Причина</label><span class="form-field-optional">необязательно</span></div>` +
      `<div class="form-field-row">CONTROL</div>${
        options.error
          ? `<span class="form-field-error">Слишком длинное значение</span>`
          : ""
      }${options.hint ? `<div class="form-field-hint">Подсказка</div>` : ""}`,
  ).replace("CONTROL", options.control);
  root.setAttribute(SCOPE.field, "");
  document.body.appendChild(root);
  return root;
};

/** The first control of a rendered field. */
export const control = (
  field: HTMLElement,
  selector = "input, textarea, select",
) => field.querySelector(selector) as HTMLElement;

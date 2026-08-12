<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type {
  User,
  Contact,
  PersonalProfile,
  Birthday,
} from "@/shared/api/models/community";
import { Gender } from "@/shared/api/models/community";
import { BlockTitle } from "@/shared/ui/Layout";
import { EditableField } from "@/shared/ui/EditableField";
import { StatLine } from "@/shared/ui/StatLine";
import Button from "@/shared/ui/Button/Button.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { DateInput } from "@/shared/ui/DatePicker";
import dayjs from "dayjs";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const emit = defineEmits<{
  updateField: [field: string, value: string];
}>();

const genderLabels: Record<Gender, string> = {
  [Gender.Unknown]: "",
  [Gender.Male]: "мужской",
  [Gender.Female]: "женский",
};

const genderValue = computed(() =>
  props.user.gender ? genderLabels[props.user.gender as Gender] : "",
);

const genderOptions: SelectOption[] = [
  { value: Gender.Male, label: "мужской" },
  { value: Gender.Female, label: "женский" },
];

// Visibility settings live on the top-level PersonalProfile DTO
// (only present for the account owner), not under user.settings.
const showYear = computed(
  () =>
    (props.user as Partial<PersonalProfile>).visibility?.showBirthday ?? false,
);

// The public profile carries `birthday` as a {day, month, year} object —
// the backend already applied privacy (it is `null` when the user opted
// out, and `year` is only included when the owner allows showing it). The
// `User` prop type is the list-level DTO; the richer UserProfile shape
// adds `birthday`, so we read it through a narrow cast.
const birthday = computed<Birthday | undefined>(
  () => (props.user as Partial<PersonalProfile>).birthday,
);

// Format with "D MMMM" (no year) per CODE_STYLE and append the year only
// when the backend actually sent one.
const birthdayValue = computed(() => {
  const b = birthday.value;
  if (!b || !b.day || !b.month) return "";
  // dayjs months are 0-based; the API delivers 1-based months. Set date(1)
  // before the month to avoid day-overflow rollover (e.g. Jan 31 → Feb).
  const base = dayjs()
    .date(1)
    .month(b.month - 1)
    .date(b.day);
  return b.year ? `${base.format("D MMMM")} ${b.year}` : base.format("D MMMM");
});

// Edit input binds an ISO `YYYY-MM-DD` string. Year is required by the
// native date input, so we use the stored year or a neutral placeholder
// year (the toggle controls whether the year is shown publicly).
const birthdayInputValue = computed(() => {
  const b = birthday.value;
  if (!b || !b.day || !b.month) return "";
  const y = b.year ?? 2000;
  const mm = String(b.month).padStart(2, "0");
  const dd = String(b.day).padStart(2, "0");
  return `${y}-${mm}-${dd}`;
});

function onGenderChange(value: string) {
  emit("updateField", "gender", value);
}

function onBirthdayChange(value: string | null) {
  emit("updateField", "birthday", value ?? "");
}

function onShowYearToggle(event: Event) {
  emit(
    "updateField",
    "showBirthday",
    String((event.target as HTMLInputElement).checked),
  );
}

/**
 * A draft row with an identity of its own. Neither of the two fields is one:
 * `contactType` is free text and nothing stops two rows from both being
 * "Telegram", and the index moves under every row below the one removed. Keyed
 * by index, deleting a middle row left the caret on an input that now held the
 * NEXT contact's data, and the reader went on typing into someone else's row.
 */
interface ContactDraft extends Contact {
  uid: number;
}

let nextDraftUid = 0;

function cloneContacts(src: readonly Contact[] | undefined): ContactDraft[] {
  return (src ?? []).map((contact) => ({
    contactType: contact.contactType,
    value: contact.value,
    uid: (nextDraftUid += 1),
  }));
}

/** What the server is told: the draft's own identity is not part of it. */
function asContacts(drafts: readonly ContactDraft[]): Contact[] {
  return drafts.map(({ contactType, value }) => ({ contactType, value }));
}

const contactsDraft = ref<ContactDraft[]>(cloneContacts(props.user.contacts));
const contacts = computed(() => props.user.contacts ?? []);

watch(
  () => props.user.contacts,
  (next) => {
    contactsDraft.value = cloneContacts(next);
  },
  { deep: true },
);

watch(
  () => props.isEditMode,
  (editing) => {
    if (!editing) contactsDraft.value = cloneContacts(props.user.contacts);
  },
);

function commitContacts() {
  emit(
    "updateField",
    "contacts",
    JSON.stringify(asContacts(contactsDraft.value)),
  );
}

function addContact() {
  contactsDraft.value.push({
    contactType: "",
    value: "",
    uid: (nextDraftUid += 1),
  });
}

function removeContact(index: number) {
  contactsDraft.value.splice(index, 1);
  commitContacts();
}

function onContactChange(
  index: number,
  key: "contactType" | "value",
  value: string,
) {
  contactsDraft.value[index][key] = value;
  if (
    contactsDraft.value[index].contactType &&
    contactsDraft.value[index].value
  ) {
    commitContacts();
  }
}
</script>

<template>
  <section class="profile-personal-info">
    <BlockTitle>Контакты</BlockTitle>

    <!--
      Field order: name → gender → location → birthday. Name and
      gender are the stable identity pair (who this is). Location is
      the contextual "where they live" fact. Birthday is the least "solid"
      item (often `не указан` or hidden by a visibility setting), so it
      goes last: an empty plaque at the end breaks the block rhythm less.
    -->
    <div class="info-grid">
      <EditableField
        :modelValue="user.name"
        :editing="isEditMode"
        label="Имя"
        placeholder="Введите имя"
        empty="не указано"
        @update:modelValue="emit('updateField', 'name', $event)"
      />

      <template v-if="isEditMode">
        <FormField label="Пол" name="gender">
          <Select
            id="profile-gender"
            :model-value="user.gender ?? ''"
            :options="genderOptions"
            placeholder="не указан"
            @update:model-value="onGenderChange"
          />
        </FormField>
      </template>
      <StatLine
        v-else
        label="Пол"
        :value="genderValue"
        placeholder="не указан"
      />

      <EditableField
        :modelValue="user.location"
        :editing="isEditMode"
        label="Местоположение"
        placeholder="Город, страна"
        empty="не указано"
        @update:modelValue="emit('updateField', 'location', $event)"
      />

      <template v-if="isEditMode">
        <FormField label="День рождения" name="birthday">
          <DateInput
            :model-value="birthdayInputValue || null"
            aria-label="День рождения"
            @update:model-value="onBirthdayChange"
          />
        </FormField>
        <label class="checkbox-row">
          <input
            type="checkbox"
            :checked="showYear"
            @change="onShowYearToggle"
          />
          Показывать год рождения
        </label>
      </template>
      <StatLine
        v-else
        label="День рождения"
        :value="birthdayValue"
        placeholder="не указан"
      />

      <template v-if="isEditMode">
        <div v-if="contactsDraft.length" class="contacts-edit-list">
          <div
            v-for="(contact, idx) in contactsDraft"
            :key="contact.uid"
            class="contact-edit-row"
          >
            <input
              type="text"
              class="input contact-type"
              placeholder="Telegram, Discord, ..."
              :aria-label="`Тип контакта ${idx + 1}`"
              :value="contact.contactType"
              @input="
                onContactChange(
                  idx,
                  'contactType',
                  ($event.target as HTMLInputElement).value,
                )
              "
            />
            <input
              type="text"
              class="input contact-value"
              placeholder="значение"
              :aria-label="`Значение контакта ${idx + 1}`"
              :value="contact.value"
              @input="
                onContactChange(
                  idx,
                  'value',
                  ($event.target as HTMLInputElement).value,
                )
              "
            />
            <button
              type="button"
              class="contact-remove"
              aria-label="Удалить"
              @click="removeContact(idx)"
            >
              <SvgIcon name="trash" />
            </button>
          </div>
        </div>
        <Button class="add-contact" @click="addContact"
          >Добавить контакт</Button
        >
      </template>
      <!-- Blank-line separator between personal fields and contacts:
           the `\n` text node lives in a `white-space: pre` div so the
           Selection API picks it up as a real newline, while the div's
           fixed height handles the visible 8 px gap. Without this, copy
           would jam contacts directly under "Местоположение" without the
           visual break the user sees on screen. -->
      <div
        v-if="!isEditMode && contacts.length"
        class="info-grid-break"
        aria-hidden="true"
      >
        {{ "\n" }}
      </div>
      <div v-if="!isEditMode && contacts.length" class="contacts-subgroup">
        <!-- Keyed by position, and that is not the editor's mistake repeated:
             this list is a read-only projection of a prop with no state of its
             own, so its position IS its identity. contactType is not — the
             editor lets a reader add "Telegram" twice, and two rows under one
             key is a duplicate-key warning and a patch that can leave one of
             them stale. -->
        <StatLine
          v-for="(contact, idx) in contacts"
          :key="idx"
          :label="contact.contactType"
          :value="contact.value"
        />
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

// Within-group row-gap = $minor (4px), synchronized with .stats-group /
// .endorsement-stats / .contacts-subgroup in the profile. On top of line-height
// 1.25 it gives 7-8px of air between "label: value" lines.
.info-grid
  display: flex
  flex-direction: column
  gap: $minor

// Visible separator between personal fields and the contacts subgroup.
// `white-space: pre` keeps the `\n` text node for the Selection API
// (copy-paste gets a real empty line). The $small height +
// two $minor gaps from the parent .info-grid give a total $medium gap —
// the same as between .endorsement-stats and .violations-inline in ProfilePage.
.info-grid-break
  display: block
  height: $small
  font-size: 0
  line-height: 0
  white-space: pre

.contacts-subgroup
  display: flex
  flex-direction: column
  // Within-group row-gap = $minor, as in .stats-group / .info-grid.
  gap: $minor

.checkbox-row
  display: inline-flex
  align-items: center
  gap: $small
  font-size: $font-size
  color: $text
  cursor: pointer

  input
    cursor: pointer

.input
  font-family: inherit
  font-size: $font-size

.contacts-edit-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $small

.contact-edit-row
  display: flex
  gap: $small
  align-items: center

.contact-type
  flex: 0 0 160px

.contact-value
  flex: 1
  min-width: 0

.contact-remove
  color: $text-muted
  +icon-button(20px)

  &:hover
    color: $accent-red

  svg
    width: 14px
    height: 14px

.add-contact
  margin-top: $small
  align-self: flex-start
</style>

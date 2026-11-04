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
import { RowIconButton } from "@/shared/ui/RowIconButton";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { DateInput } from "@/shared/ui/DatePicker";
import { siteTime } from "@/shared/lib/utils/datetime";
import dayjs from "dayjs";

const props = defineProps<{
  /** The public profile, as everybody reads it. */
  user: User & Partial<Pick<PersonalProfile, "birthday">>;
  isEditMode: boolean;
  /**
   * The owner's own profile, read when the edit mode opens: the public one
   * hides a birthday kept private and carries no visibility at all.
   */
  own?: Pick<PersonalProfile, "visibility" | "birthday"> | null;
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
  props.user.gender ? genderLabels[props.user.gender] : "",
);

const genderOptions: SelectOption[] = [Gender.Male, Gender.Female].map(
  (value) => ({ value, label: genderLabels[value] }),
);

// What is stored, as the owner saved it.
const showYear = computed(() => props.own?.visibility?.showBirthday ?? false);
const showRating = computed(() => props.own?.visibility?.showRating ?? true);

// Read: the server applies the privacy, no birthday when hidden. Edited: the stored one.
const birthday = computed<Birthday | undefined>(() =>
  props.isEditMode ? props.own?.birthday : props.user.birthday,
);

// "D MMMM", the year only where the server sent one.
const birthdayValue = computed(() => {
  const b = birthday.value;
  if (!b || !b.day || !b.month) return "";
  // dayjs months are 0-based; date(1) first, so 31 January does not roll into February.
  const base = dayjs()
    .date(1)
    .month(b.month - 1)
    .date(b.day);
  return b.year ? `${base.format("D MMMM")} ${b.year}` : base.format("D MMMM");
});

/** The year the date input is given when the server sent none: a leap one, so 29 February stands. */
const PLACEHOLDER_YEAR = 2000;

// The date input needs a year: the stored one, else the placeholder.
const birthdayInputValue = computed(() => {
  const b = birthday.value;
  if (!b || !b.day || !b.month) return "";
  const y = b.year ?? PLACEHOLDER_YEAR;
  const mm = String(b.month).padStart(2, "0");
  const dd = String(b.day).padStart(2, "0");
  return `${y}-${mm}-${dd}`;
});

function onGenderChange(value: string) {
  emit("updateField", "gender", value);
}

// The date picked or typed while editing, else the saved one: the profile stays
// the saved one until "Завершить", and bound to it the field snapped back.
const pickedBirthday = ref<string | null | undefined>(undefined);
/** What the field says is wrong with the date typed, shown under it. */
const birthdayReason = ref<string | null>(null);
const birthdayModel = computed(() =>
  pickedBirthday.value !== undefined
    ? pickedBirthday.value
    : birthdayInputValue.value || null,
);
watch(
  () => props.isEditMode,
  () => {
    pickedBirthday.value = undefined;
    birthdayReason.value = null;
  },
);

// An empty birthday opens on the years round twelve years ago, not on the last twelve.
const birthdayOpensOn = siteTime().subtract(12, "year").format("YYYY-MM-DD");

function onBirthdayChange(value: string | null) {
  pickedBirthday.value = value;
  emit("updateField", "birthday", value ?? "");
}

function onShowYearToggle(event: Event) {
  emit(
    "updateField",
    "showBirthday",
    String((event.target as HTMLInputElement).checked),
  );
}

function onShowRatingToggle(event: Event) {
  emit(
    "updateField",
    "showRating",
    String((event.target as HTMLInputElement).checked),
  );
}

/**
 * A draft row with an identity of its own: neither field is one, and keyed by
 * index a deleted middle row left the caret in the next contact's input.
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

    <!-- Name, gender, location, then the birthday: the field most often empty goes last. -->
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
        <FormField
          label="День рождения"
          name="birthday"
          :errors="birthdayReason ? [birthdayReason] : []"
        >
          <DateInput
            :model-value="birthdayModel"
            :max="siteTime().format('YYYY-MM-DD')"
            narrow
            empty-level="years"
            :open-on="birthdayOpensOn"
            aria-label="День рождения"
            @update:model-value="onBirthdayChange"
            @reason="birthdayReason = $event"
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
        <!-- What others see of the owner, as the year of the birthday above. -->
        <FormField class="rating-field">
          <label class="checkbox-row">
            <input
              type="checkbox"
              :checked="showRating"
              @change="onShowRatingToggle"
            />
            Участие в рейтинге
          </label>
          <template #hint>
            Без участия рейтинг не виден в списках и карточках, в профиле он
            виден всегда
          </template>
        </FormField>
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
            <RowIconButton
              icon="trash"
              label="Удалить контакт"
              @click="removeContact(idx)"
            />
          </div>
        </div>
        <Button class="add-contact" @click="addContact"
          >Добавить контакт</Button
        >
      </template>
      <!-- A `\n` in a pre div, so a copy keeps the blank line between the fields and the contacts. -->
      <div
        v-if="!isEditMode && contacts.length"
        class="info-grid-break"
        aria-hidden="true"
      >
        {{ "\n" }}
      </div>
      <div v-if="!isEditMode && contacts.length" class="contacts-subgroup">
        <!-- Keyed by position: a read-only projection whose position is its identity, and two rows may share a type. -->
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
$contact-type-width: 160px

// $minor between the lines of a group, as every group of stat lines of the profile.
.info-grid,
.contacts-subgroup
  display: flex
  flex-direction: column
  gap: $minor

// Keeps the `\n` for a copy; $small plus two gaps is the $medium between subgroups.
.info-grid-break
  display: block
  height: $small
  font-size: 0
  line-height: 0
  white-space: pre

.checkbox-row
  display: inline-flex
  align-items: center
  gap: $small
  font-size: $font-size
  color: $text
  cursor: pointer

  input
    cursor: pointer

// The hint is the field's, the box stands at the step of the grid as the one above.
.rating-field
  margin: 0

// The contact fields take the page's field type (+input).
.input
  font-family: inherit

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
  flex: 0 0 $contact-type-width

.contact-value
  flex: 1
  min-width: 0

.add-contact
  margin-top: $small
  align-self: flex-start
</style>

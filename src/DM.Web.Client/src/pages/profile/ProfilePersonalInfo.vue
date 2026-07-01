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

// Format with «D MMMM» (no year) per CODE_STYLE and append the year only
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

function onGenderChange(event: Event) {
  emit("updateField", "gender", (event.target as HTMLSelectElement).value);
}

function onBirthdayChange(event: Event) {
  emit("updateField", "birthday", (event.target as HTMLInputElement).value);
}

function onShowYearToggle(event: Event) {
  emit(
    "updateField",
    "showBirthday",
    String((event.target as HTMLInputElement).checked),
  );
}

function cloneContacts(src: readonly Contact[] | undefined): Contact[] {
  return src ? JSON.parse(JSON.stringify(src)) : [];
}

const contactsDraft = ref<Contact[]>(cloneContacts(props.user.contacts));
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
  emit("updateField", "contacts", JSON.stringify(contactsDraft.value));
}

function addContact() {
  contactsDraft.value.push({ contactType: "", value: "" });
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
      Поле-порядок: имя → пол → местоположение → день рождения. Имя и
      пол — стабильная личностная пара (кто это). Местоположение —
      контекстный факт «где живет». День рождения — наименее «жесткий»
      пункт (часто `не указан` или скрыт визибилити-настройкой), поэтому
      идет последним: пустая плашка в конце меньше ломает ритм блока.
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
        <div class="form-row">
          <span class="label">Пол:</span>
          <select
            :value="user.gender ?? ''"
            class="select"
            @change="onGenderChange"
          >
            <option value="">не указан</option>
            <option :value="Gender.Male">мужской</option>
            <option :value="Gender.Female">женский</option>
          </select>
        </div>
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
        <div class="form-row">
          <span class="label">День рождения:</span>
          <input
            type="date"
            :value="birthdayInputValue"
            class="input"
            @change="onBirthdayChange"
          />
        </div>
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
            :key="idx"
            class="contact-edit-row"
          >
            <input
              type="text"
              class="input contact-type"
              placeholder="Telegram, Discord, …"
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
        <StatLine
          v-for="contact in contacts"
          :key="contact.contactType"
          :label="contact.contactType"
          :value="contact.value"
        />
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

// Within-group row-gap = $minor (4px), синхронизирован с .stats-group /
// .endorsement-stats / .contacts-subgroup в профиле. Поверх line-height
// 1.25 дает 7-8px воздуха между «label: value» строками.
.info-grid
  display: flex
  flex-direction: column
  gap: $minor

// Visible separator between personal fields и contacts-subgroup.
// `white-space: pre` сохраняет `\n` text-node для Selection API
// (копи-паст получает реальную пустую строку). Высота $small +
// два $minor gap'а от parent .info-grid дают итоговый $medium-зазор —
// тот же, что между .endorsement-stats и .violations-inline в ProfilePage.
.info-grid-break
  display: block
  height: $small
  font-size: 0
  line-height: 0
  white-space: pre

.contacts-subgroup
  display: flex
  flex-direction: column
  // Внутри-группового row-gap = $minor, как в .stats-group / .info-grid.
  gap: $minor

.form-row
  display: flex
  gap: $small
  align-items: center
  font-size: $font-size

.checkbox-row
  display: inline-flex
  align-items: center
  gap: $small
  font-size: $font-size
  color: $text
  cursor: pointer
  user-select: none

  input
    cursor: pointer

.label
  color: $text
  flex-shrink: 0

.select,
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
  display: inline-flex
  align-items: center
  justify-content: center
  width: 20px
  height: 20px
  padding: 0
  background: none
  border: none
  color: $text-muted
  cursor: pointer

  &:hover
    color: $accent-red

  svg
    width: 14px
    height: 14px

.add-contact
  margin-top: $small
  align-self: flex-start
</style>

<script setup lang="ts">
import { ref, computed } from "vue";
import ThePaging from "@/components/ThePaging.vue";
import ThePoll from "@/views/layout/sidebar/ThePoll.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import { useRoute } from "vue-router";
import { usePollsStore } from "@/stores/polls";
import { useUserStore } from "@/stores";
import { storeToRefs } from "pinia";
import { userIsHighAuthority } from "@/api/models/community/helpers";
import dayjs from "dayjs";

const route = useRoute();
const pollsStore = usePollsStore();
const userStore = useUserStore();
const { polls } = storeToRefs(pollsStore);

const canCreatePoll = computed(() => userIsHighAuthority(userStore.user));

// Form state
const formExpanded = ref(false);
const formHovered = ref(false);
const formContent = ref<HTMLElement | null>(null);

const pollTitle = ref("");
const pollEnds = ref(dayjs().add(7, "day").format("YYYY-MM-DDTHH:mm"));
const pollOptions = ref(["", ""]);
const isSubmitting = ref(false);
const errorMessage = ref("");

function toggleForm() {
  formExpanded.value = !formExpanded.value;
  if (formContent.value) {
    if (formExpanded.value) {
      formContent.value.style.height = "auto";
      const expectedHeight = formContent.value.clientHeight;
      formContent.value.style.height = "0";
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = `${expectedHeight}px`;
      }, 0);
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = "auto";
      }, 200);
    } else {
      formContent.value.style.height = `${formContent.value.clientHeight}px`;
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = "0";
      }, 0);
    }
  }
}

function addOption() {
  pollOptions.value.push("");
}

function removeOption(index: number) {
  if (pollOptions.value.length > 2) {
    pollOptions.value.splice(index, 1);
  }
}

async function submitPoll() {
  if (!pollTitle.value.trim()) {
    errorMessage.value = "Введите название опроса";
    return;
  }

  const validOptions = pollOptions.value.filter((o) => o.trim());
  if (validOptions.length < 2) {
    errorMessage.value = "Нужно минимум 2 варианта ответа";
    return;
  }

  isSubmitting.value = true;
  errorMessage.value = "";

  const { error } = await pollsStore.createPoll({
    title: pollTitle.value.trim(),
    ends: new Date(pollEnds.value).toISOString(),
    options: validOptions.map((text) => ({ text })) as any,
  });

  isSubmitting.value = false;

  if (error) {
    if (error.status === 400) {
      errorMessage.value = "Некорректные данные. Дата окончания должна быть минимум через сутки.";
    } else if (error.status === 403) {
      errorMessage.value = "Недостаточно прав";
    } else {
      errorMessage.value = "Не удалось создать опрос";
    }
  } else {
    pollTitle.value = "";
    pollEnds.value = dayjs().add(7, "day").format("YYYY-MM-DDTHH:mm");
    pollOptions.value = ["", ""];
    formExpanded.value = false;
  }
}
</script>

<template>
  <page-title>
    <span
      v-if="canCreatePoll"
      class="toggle"
      @click="toggleForm"
      @mouseenter="formHovered = true"
      @mouseleave="formHovered = false"
    >
      Опросы<span
        class="toggle-icon"
        :style="{
          transform: `rotate(${formExpanded ? 45 : 0}deg)`,
          opacity: formHovered ? 1 : 0,
        }"
      ></span>
    </span>
    <span v-else>Опросы</span>
  </page-title>

  <div
    v-if="canCreatePoll"
    ref="formContent"
    class="poll-form-wrapper"
    :class="{ collapsed: !formExpanded }"
  >
    <div class="poll-form">
      <div class="form-field">
        <label class="form-label"><strong>Название</strong></label>
        <input v-model="pollTitle" type="text" class="form-input" placeholder="Вопрос опроса" />
      </div>
      <div class="form-field">
        <label class="form-label"><strong>Окончание</strong></label>
        <input v-model="pollEnds" type="datetime-local" class="form-input" />
      </div>
      <div class="form-field">
        <label class="form-label"><strong>Варианты ответа</strong></label>
        <div v-for="(_, index) in pollOptions" :key="index" class="option-row">
          <input
            v-model="pollOptions[index]"
            type="text"
            class="form-input"
            :placeholder="`Вариант ${index + 1}`"
          />
          <button
            v-if="pollOptions.length > 2"
            type="button"
            class="remove-option-btn"
            @click="removeOption(index)"
          >
            ×
          </button>
        </div>
        <button type="button" class="add-option-btn" @click="addOption">+ Добавить вариант</button>
      </div>
      <div class="form-actions">
        <the-button :disabled="isSubmitting" @click="submitPoll">
          {{ isSubmitting ? "Создание..." : "Создать опрос" }}
        </the-button>
        <span v-if="errorMessage" class="error-message">{{ errorMessage }}</span>
      </div>
    </div>
  </div>

  <the-paging
    v-if="polls"
    :paging="polls.paging!"
    :to="{ name: 'polls', params: route.params }"
  />

  <template v-if="polls && polls.resources.length === 0">
    <secondary-text>Опросов пока нет</secondary-text>
  </template>
  <template v-else-if="polls">
    <the-poll
      v-for="poll in polls.resources"
    :key="poll.id"
    :poll="poll"
    :detailed="true"
  />

  <the-paging
    v-if="polls && polls.resources.length > 5"
    :paging="polls.paging!"
    :to="{ name: 'polls', params: route.params }"
  />
</template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.toggle
  cursor: pointer

.toggle-icon
  position: relative
  display: inline-block
  width: 12px
  height: 12px
  margin-left: $small
  opacity: 0
  transition: opacity 0.15s ease, transform 0.3s ease
  vertical-align: middle
  margin-top: -3px

  &::before,
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    background-color: $heading

  &::before
    width: 12px
    height: 2px
    transform: translate(-50%, -50%)

  &::after
    width: 2px
    height: 12px
    transform: translate(-50%, -50%)

.poll-form-wrapper
  overflow: hidden
  transition: height 0.2s ease

  &.collapsed
    height: 0

.poll-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px dashed $border

.form-field
  margin-bottom: $medium

.form-label
  display: block
  margin-bottom: $tiny
  color: $text
  font-size: $font-size

.form-input
  display: block
  width: 100%
  padding: $small
  border: 1px dashed $border
  background-color: $input-bg-overlay
  color: $text
  font-family: inherit
  font-size: inherit
  box-sizing: border-box

  &:focus
    outline: none
    border-style: solid
    border-color: $button-border-hover

.option-row
  display: flex
  gap: $small
  margin-bottom: $small

  .form-input
    flex: 1

.remove-option-btn
  padding: 0 $small
  border: 1px dashed $border
  background-color: $bg-element
  color: $text-muted
  cursor: pointer
  font-size: 1.2em

  &:hover
    color: $accent-red

.add-option-btn
  padding: $tiny $small
  border: none
  background: transparent
  color: $link
  cursor: pointer
  font-size: $secondary-font-size

  &:hover
    text-decoration: underline

.form-actions
  display: flex
  align-items: center
  gap: $medium

.error-message
  color: $accent-red
  font-size: $secondary-font-size
</style>

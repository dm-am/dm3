<script setup lang="ts">
import { ref, onMounted } from "vue";
import { fundraisingApi } from "@/entities/fundraising";
import { useToast } from "@/shared/lib/composables/useToast";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("Admin");
const loading = ref(true);
const loadError = ref<string | null>(null);
const saving = ref(false);

const goalAmount = ref<number>(0);
const collectedAmount = ref<number>(0);

const { success, error: showError } = useToast();

async function load() {
  loading.value = true;
  loadError.value = null;
  const { data, error } = await fundraisingApi.getFundraising();
  if (error || !data) {
    loadError.value = "Не удалось загрузить данные сбора средств";
  } else {
    goalAmount.value = data.resource.goalAmount;
    collectedAmount.value = data.resource.collectedAmount;
  }
  loading.value = false;
}

async function save() {
  saving.value = true;
  const { data, error } = await fundraisingApi.updateFundraising({
    goalAmount: goalAmount.value,
    collectedAmount: collectedAmount.value,
  });
  if (error || !data) {
    showError("Не удалось сохранить изменения");
  } else {
    goalAmount.value = data.resource.goalAmount;
    collectedAmount.value = data.resource.collectedAmount;
    success("Сбор средств обновлен");
  }
  saving.value = false;
}

onMounted(load);
</script>

<template>
  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <div v-else class="moderation-fundraising">
    <div v-if="loading" class="loading">Загрузка...</div>
    <div v-else-if="loadError" class="error">{{ loadError }}</div>
    <form v-else class="fundraising-form" @submit.prevent="save">
      <SecondaryText>
        Значения отображаются в блоке "Поддержка проекта" в правом сайдбаре.
      </SecondaryText>
      <div class="form-field">
        <label for="fundraising-goal">Цель (р.)</label>
        <input
          id="fundraising-goal"
          v-model.number="goalAmount"
          type="number"
          min="1"
          step="1"
          required
        />
      </div>
      <div class="form-field">
        <label for="fundraising-collected">Собрано (р.)</label>
        <input
          id="fundraising-collected"
          v-model.number="collectedAmount"
          type="number"
          min="0"
          step="1"
          required
        />
      </div>
      <div class="form-actions">
        <button type="submit" class="btn-save" :disabled="saving">
          {{ saving ? "Сохранение..." : "Сохранить" }}
        </button>
      </div>
    </form>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.loading,
.error
  padding: $large

.error
  color: $accent-red

.fundraising-form
  max-width: 450px
  display: flex
  flex-direction: column
  gap: $medium
  padding: $medium
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.form-field
  label
    display: block
    margin-bottom: $tiny
    font-size: $secondary-font-size
    color: $text-muted

  input
    width: 100%
    padding: $small
    font-size: $secondary-font-size
    font-family: inherit
    border: 1px solid $border
    border-radius: $border-radius
    background: $bg-element
    color: $text
    box-sizing: border-box

    &:focus:not(:focus-visible)
      outline: none
      border-color: $link

.form-actions
  display: flex
  justify-content: flex-end

.btn-save
  +button
</style>

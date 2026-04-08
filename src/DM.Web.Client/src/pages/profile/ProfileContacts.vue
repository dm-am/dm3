<script setup lang="ts">
import type { User, Contact } from "@/shared/api/models/community";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const contacts = props.user.contacts || [];
</script>

<template>
  <section v-if="contacts.length > 0" class="profile-contacts">
    <h3 class="section-title">Контакты</h3>

    <div class="contacts-list">
      <div
        v-for="contact in contacts"
        :key="contact.contactType"
        class="contact-item"
      >
        <span class="contact-title">{{ contact.contactType }}</span>
        <span class="contact-value">{{ contact.value }}</span>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-contacts
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $text
  margin: 0 0 $medium
  font-size: 1rem

.contacts-list
  display: flex
  flex-direction: column
  gap: $small

.contact-item
  display: flex
  justify-content: space-between
  padding: $small
  background: $bg-element-overlay
  border-radius: $border-radius

.contact-title
  color: $text-muted
  font-size: $secondary-font-size

.contact-value
  color: $text
</style>

export { default as UserLink } from "./UserLink.vue";
export { default as UserRating } from "./UserRating.vue";
export { default as UserAutocomplete } from "./UserAutocomplete.vue";
export { default as UserMultiSelect } from "./UserMultiSelect.vue";
// Availability-checking username field: a domain lookup, so it belongs to the
// entity that owns usernames rather than to the shared kit.
export { default as UsernameInput } from "./UsernameInput.vue";
// AvatarImg lives in shared/ui (used by shared components too); re-exported
// here so user-related consumers keep importing it from the entity barrel.
export { AvatarImg } from "@/shared/ui/AvatarImg";

import { computed, type Ref, type ComputedRef } from "vue";
import dayjs from "dayjs";
import { UserRole, type UserRef } from "@/shared/api/models/common";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";

type RoleBadge = { label: string; title: string } | null;

interface AuthorMetaOptions {
  author:
    | Ref<UserRef | null | undefined>
    | ComputedRef<UserRef | null | undefined>;
  createdUtc:
    | Ref<string | null | undefined>
    | ComputedRef<string | null | undefined>;
  modifiedUtc?:
    | Ref<string | null | undefined>
    | ComputedRef<string | null | undefined>;
}

interface AuthorMetaResult {
  formattedDate: ComputedRef<string>;
  formattedTime: ComputedRef<string>;
  formattedEditDate: ComputedRef<string>;
  isEdited: ComputedRef<boolean>;
  isAuthorOnline: ComputedRef<boolean>;
  roleBadge: ComputedRef<RoleBadge>;
  tooltipText: ComputedRef<string>;
}

/**
 * Composable for author metadata display logic.
 * Used by Topic, Comment, Post, Message, Publication, Testimonial, Poll, etc.
 */
export function useAuthorMeta(options: AuthorMetaOptions): AuthorMetaResult {
  const { author, createdUtc, modifiedUtc } = options;

  const formattedDate = computed(() => {
    const date = createdUtc.value;
    if (!date) return "";
    return dayjs(date).format("DD.MM.YYYY [в] HH:mm");
  });

  const formattedTime = computed(() => {
    const date = createdUtc.value;
    if (!date) return "";
    return dayjs(date).format("HH:mm");
  });

  const isEdited = computed(() => !!modifiedUtc?.value);

  const formattedEditDate = computed(() => {
    const date = modifiedUtc?.value;
    if (!date) return "";
    return dayjs(date).format("DD.MM.YYYY [в] HH:mm");
  });

  const isAuthorOnline = computed(() => {
    const lastActivityUtc = author.value?.lastActivityUtc;
    if (!lastActivityUtc) return false;
    const minutesSinceOnline = dayjs().diff(
      dayjs(lastActivityUtc),
      "minute",
      true,
    );
    return minutesSinceOnline <= ONLINE_THRESHOLD_MINUTES;
  });

  const roleBadge = computed((): RoleBadge => {
    const role = author.value?.role;
    switch (role) {
      case UserRole.Admin:
        return { label: "А", title: "Администратор" };
      case UserRole.SeniorModerator:
        return { label: "С", title: "Старший модератор" };
      case UserRole.Moderator:
        return { label: "М", title: "Модератор" };
      case UserRole.Mentor:
        return { label: "Н", title: "Наставник" };
      case UserRole.System:
        return { label: "Р", title: "Робот-администратор" };
      default:
        return null;
    }
  });

  const tooltipText = computed(() => {
    const date = createdUtc.value;
    if (!date) return "";
    return `Создано: ${dayjs(date).format("DD.MM.YYYY [в] HH:mm")}`;
  });

  return {
    formattedDate,
    formattedTime,
    formattedEditDate,
    isEdited,
    isAuthorOnline,
    roleBadge,
    tooltipText,
  };
}

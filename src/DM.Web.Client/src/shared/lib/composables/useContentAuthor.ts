import { computed, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/community";
import dayjs from "dayjs";

const ONLINE_THRESHOLD_MINUTES = 5;

interface Author {
  username: string;
  role?: UserRole;
  lastActivityUtc?: string;
  smallPictureUrl?: string;
}

interface ContentData {
  author?: Author | null;
  createdUtc?: string;
  modifiedUtc?: string;
}

export function useContentAuthor<T extends ContentData>(content: Ref<T>) {
  const { user: currentUser } = storeToRefs(useUserStore());

  const author = computed(() => content.value.author);

  const isAuthorOnline = computed(() => {
    const lastActivityUtc = author.value?.lastActivityUtc;
    if (!lastActivityUtc) return false;
    return dayjs().diff(dayjs(lastActivityUtc), "minute", true) <= ONLINE_THRESHOLD_MINUTES;
  });

  const roleBadge = computed(() => {
    switch (author.value?.role) {
      case UserRole.Admin:
        return { label: "A", title: "Администратор" };
      case UserRole.SeniorModerator:
        return { label: "C", title: "Старший модератор" };
      case UserRole.Moderator:
        return { label: "M", title: "Модератор" };
      case UserRole.Mentor:
        return { label: "H", title: "Наставник" };
      case UserRole.System:
        return { label: "P", title: "Робот-администратор" };
      default:
        return null;
    }
  });

  const formattedDate = computed(() => {
    if (!content.value.createdUtc) return "";
    return dayjs(content.value.createdUtc).format("DD.MM.YYYY HH:mm");
  });

  const formattedEditDate = computed(() => {
    if (!content.value.modifiedUtc) return null;
    const d = dayjs(content.value.modifiedUtc);
    return `${d.format("DD.MM.YYYY")} в ${d.format("HH:mm")}`;
  });

  const isEdited = computed(() => !!content.value.modifiedUtc);

  const isAuthor = computed(
    () => currentUser.value?.username === author.value?.username
  );

  const isModerator = computed(() => {
    if (!currentUser.value) return false;
    return (currentUser.value.roles ?? []).some((r) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r)
    );
  });

  const canLike = computed(() => {
    if (!currentUser.value) return false;
    return !isAuthor.value;
  });

  return {
    author,
    isAuthorOnline,
    roleBadge,
    formattedDate,
    formattedEditDate,
    isEdited,
    isAuthor,
    isModerator,
    canLike,
    currentUser,
  };
}

import dayjs from "dayjs";
import relativeTime from "dayjs/plugin/relativeTime";
import "dayjs/locale/ru";

export function setupDayjs() {
  dayjs.extend(relativeTime).locale("ru");
}

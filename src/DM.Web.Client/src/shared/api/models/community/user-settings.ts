// Re-export from personal for backwards compatibility
export { Theme, type Paging, type Preferences } from "../personal";
import { Theme, type Paging } from "../personal";

export type UserSettings = {
  mentorGreetingsMessage: string;
  theme: Theme;
  paging: Paging | null;
};

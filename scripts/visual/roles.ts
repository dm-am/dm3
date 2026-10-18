/**
 * The accounts the stand looks through. The seeder creates exactly these, one
 * per role (src/DM.Tools.Seeder/Seeding/DataSeeder.Users.cs), with the
 * development password every seeded account shares; nothing here is a secret.
 */
export interface Role {
  name: RoleName;
  email?: string;
  password?: string;
  username?: string;
}

export type RoleName =
  | "guest"
  | "user"
  | "mentor"
  | "moderator"
  | "senior"
  | "admin";

const PASSWORD = "Test123!";

export const ROLES: Role[] = [
  { name: "guest" },
  {
    name: "user",
    email: "user@test.local",
    password: PASSWORD,
    username: "TestUser",
  },
  {
    name: "mentor",
    email: "mentor@test.local",
    password: PASSWORD,
    username: "TestMentor",
  },
  {
    name: "moderator",
    email: "mod@test.local",
    password: PASSWORD,
    username: "TestModerator",
  },
  {
    name: "senior",
    email: "seniormod@test.local",
    password: PASSWORD,
    username: "TestSeniorMod",
  },
  {
    name: "admin",
    email: "admin@test.local",
    password: PASSWORD,
    username: "SolohinLex",
  },
];

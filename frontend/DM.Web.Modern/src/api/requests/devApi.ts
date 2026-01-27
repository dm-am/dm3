import Api from "@/api";
import { UserRole } from "@/api/models/community";

export interface TestAccountInfo {
  login: string;
  password: string;
  role: UserRole;
}

class DevApi {
  /**
   * Set role for current user
   */
  public async setRole(role: UserRole) {
    return Api.post(`dev/role/${role}`);
  }

  /**
   * Get all users from database
   */
  public async getAllUsers() {
    return Api.get<TestAccountInfo[]>("dev/accounts");
  }
}

export default new DevApi();

export type RegisterCredentials = {
  email: string;
  login: string;
  password: string;
  website?: string; // Honeypot field for bot protection
};

export type LoginCredentials = {
  login: string;
  password: string;
  rememberMe: boolean;
  website?: string; // Honeypot field for bot protection
};

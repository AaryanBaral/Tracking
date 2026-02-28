import { apiGet, apiPost } from "./client";

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  companyId: string;
  email: string;
  role: string;
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const raw = await apiPost<Record<string, string>>("/auth/login", { email, password });
  const token = raw.token ?? raw.Token;
  const role = raw.role ?? raw.Role;
  const expiresAtUtc = raw.expiresAtUtc ?? raw.ExpiresAtUtc;
  const companyId = raw.companyId ?? raw.CompanyId ?? "";
  const userEmail = raw.email ?? raw.Email ?? "";

  if (!token || !role) {
    throw new Error("Login failed: missing token or role.");
  }

  return {
    token,
    expiresAtUtc: expiresAtUtc ?? "",
    companyId,
    email: userEmail,
    role,
  };
}

export interface ProfileResponse {
  userId: string;
  email: string;
  role: string;
  companyId: string;
  companyName: string | null;
  companyIsActive: boolean | null;
}

export async function getProfile(): Promise<ProfileResponse> {
  return apiGet<ProfileResponse>("/auth/me");
}

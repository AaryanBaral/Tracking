const TOKEN_STORAGE_KEY = "employeeTrackerToken";
const ROLE_STORAGE_KEY = "employeeTrackerRole";

export function getAuthToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const token =
    window.localStorage.getItem(TOKEN_STORAGE_KEY) ||
    window.sessionStorage.getItem(TOKEN_STORAGE_KEY);
  if (!token || token === "undefined" || token === "null") {
    return null;
  }
  return token;
}

export function setAuthToken(token: string) {
  if (typeof window === "undefined") {
    return;
  }
  if (!token || token === "undefined" || token === "null") {
    return;
  }
  window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
  window.sessionStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function getAuthRole(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const stored =
    window.localStorage.getItem(ROLE_STORAGE_KEY) ||
    window.sessionStorage.getItem(ROLE_STORAGE_KEY);
  if (stored) {
    return stored;
  }

  const token = window.localStorage.getItem(TOKEN_STORAGE_KEY);
  const decoded = decodeRoleFromToken(token);
  if (decoded) {
    window.localStorage.setItem(ROLE_STORAGE_KEY, decoded);
  }
  return decoded;
}

export function setAuthRole(role: string) {
  if (typeof window === "undefined") {
    return;
  }
  if (!role) {
    return;
  }
  window.localStorage.setItem(ROLE_STORAGE_KEY, role);
  window.sessionStorage.setItem(ROLE_STORAGE_KEY, role);
}

export function setAuthSession(token: string, role: string) {
  setAuthToken(token);
  setAuthRole(role);
}

export function clearAuthToken() {
  if (typeof window === "undefined") {
    return;
  }
  window.localStorage.removeItem(TOKEN_STORAGE_KEY);
  window.localStorage.removeItem(ROLE_STORAGE_KEY);
  window.sessionStorage.removeItem(TOKEN_STORAGE_KEY);
  window.sessionStorage.removeItem(ROLE_STORAGE_KEY);
}

function decodeRoleFromToken(token: string | null): string | null {
  if (!token) {
    return null;
  }

  const parts = token.split(".");
  if (parts.length < 2) {
    return null;
  }

  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = window.atob(payload);
    const data = JSON.parse(json) as Record<string, unknown>;
    const role =
      (data.role as string | undefined) ||
      (data["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] as
        | string
        | undefined);
    return role ?? null;
  } catch {
    return null;
  }
}

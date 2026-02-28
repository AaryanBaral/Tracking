import { apiGet, apiPatch } from "./client";

// Types
export interface Device {
  deviceId: string;
  hostname: string | null;
  displayName: string | null;
  lastSeenAt: string;
}

export interface SummaryItem {
  name: string;
  seconds: number;
}

export interface DeviceSummary {
  date: string;
  device: Device;
  deviceOnSeconds: number;
  deviceOnStartAtUtc: string | null;
  deviceOnEndAtUtc: string | null;
  idleSeconds: number;
  topDomains: SummaryItem[];
  topUrls: SummaryItem[];
  topApps: SummaryItem[];
}

export interface UpdateDeviceResponse {
  updated: boolean;
}

// API Functions
export async function getDevices(): Promise<Device[]> {
  return apiGet<Device[]>("/devices");
}

export async function updateDeviceDisplayName(
  deviceId: string,
  displayName: string | null
): Promise<UpdateDeviceResponse> {
  return apiPatch<UpdateDeviceResponse>(`/devices/${encodeURIComponent(deviceId)}`, {
    displayName,
  });
}

export async function getDeviceSummary(
  deviceId: string,
  date: string
): Promise<DeviceSummary> {
  return apiGet<DeviceSummary>(
    `/devices/${encodeURIComponent(deviceId)}/summary?date=${encodeURIComponent(date)}`
  );
}

// Session Detail Types
export interface AppSession {
  sessionId: string;
  processName: string;
  windowTitle: string | null;
  startAt: string;
  endAt: string;
}

export interface WebSession {
  sessionId: string;
  domain: string;
  title: string | null;
  url: string | null;
  startAt: string;
  endAt: string;
}

export interface IdleSession {
  sessionId: string;
  startAt: string;
  endAt: string;
}

// Session Detail API Functions
export async function getDeviceAppSessions(
  deviceId: string,
  page: number = 1,
  pageSize: number = 50,
  start?: string,
  end?: string,
  search?: string
): Promise<AppSession[]> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  if (start) params.append('start', start);
  if (end) params.append('end', end);
  if (search) params.append('search', search);

  return apiGet<AppSession[]>(
    `/devices/${encodeURIComponent(deviceId)}/app-sessions?${params}`
  );
}

export async function getDeviceWebSessions(
  deviceId: string,
  page: number = 1,
  pageSize: number = 50,
  start?: string,
  end?: string,
  search?: string
): Promise<WebSession[]> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  if (start) params.append('start', start);
  if (end) params.append('end', end);
  if (search) params.append('search', search);

  return apiGet<WebSession[]>(
    `/devices/${encodeURIComponent(deviceId)}/web-sessions?${params}`
  );
}

export async function getDeviceIdleSessions(
  deviceId: string,
  page: number = 1,
  pageSize: number = 50,
  start?: string,
  end?: string
): Promise<IdleSession[]> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  if (start) params.append('start', start);
  if (end) params.append('end', end);

  return apiGet<IdleSession[]>(
    `/devices/${encodeURIComponent(deviceId)}/idle-sessions?${params}`
  );
}

